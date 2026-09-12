using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

internal sealed class MeshyBridgeImportOrchestrator
{
	readonly MeshyBridgeMaterialPolicy materialPolicy;

	internal MeshyBridgeImportOrchestrator(MeshyBridgeMaterialPolicy materialPolicy)
	{
		this.materialPolicy = materialPolicy;
	}

	internal void ProcessTransfer(MeshyBridgeWindow.MeshTransfer transfer, bool standOnGround, string tempCachePath)
	{
		if (transfer == null) return;

		const string stage = "queue-process";
		try
		{
			string fileExtension = Path.GetExtension(transfer.path)?.ToLowerInvariant();
			switch (fileExtension)
			{
				case ".glb":
					ImportModelWithMaterial(transfer, standOnGround);
					break;
				case ".zip":
					ProcessZipFile(transfer, standOnGround, tempCachePath);
					break;
				case ".fbx":
					ImportFBXWithTextures(transfer, standOnGround);
					break;
				default:
					MeshyBridgeDiagnostics.LogWarning(stage, "Unsupported transfer extension: " + fileExtension);
					break;
			}
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError(stage, "Error processing queued transfer", e);
		}
		finally
		{
			CleanupTempFile(transfer.path);
		}
	}

	void ImportModelWithMaterial(MeshyBridgeWindow.MeshTransfer transfer, bool standOnGround)
	{
		const string stage = "import-glb";
		try
		{
			string importDir = EnsureImportRoot();
			string modelName = BuildSanitizedModelName(transfer.name);

			string extension = Path.GetExtension(transfer.path);
			if (string.IsNullOrEmpty(extension))
				extension = "." + transfer.file_format;

			string uniqueFileName = modelName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + extension;
			string relativePath = NormalizeAssetPath(Path.Combine(importDir, uniqueFileName));
			CopySourceToAssetPath(transfer.path, relativePath);

			AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
			ConfigureImportedModelAnimation(relativePath, transfer.file_format);

			GameObject importedObject = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
			if (importedObject == null)
			{
				MeshyBridgeDiagnostics.LogWarning(stage, "Imported object not found at " + relativePath);
				return;
			}

			importedObject.name = uniqueFileName;
			materialPolicy.ApplyDefaultMaterial(importedObject);
			EditorUtility.SetDirty(importedObject);
			AssetDatabase.SaveAssets();

			InstantiateInScene(importedObject, relativePath, standOnGround, ensureAnimatorComponent: true,
				createAnimatorController: true,
				successStageLabel: "instantiate-glb");

			MeshyBridgeDiagnostics.LogInfo(stage, "Model imported successfully: " + relativePath);
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError(stage, "Model import failed", e);
		}
	}

	void ProcessZipFile(MeshyBridgeWindow.MeshTransfer transfer, bool standOnGround, string tempCachePath)
	{
		const string stage = "import-zip";
		try
		{
			string extractPath = Path.Combine(tempCachePath, "extracted");
			if (Directory.Exists(extractPath))
				Directory.Delete(extractPath, true);

			ZipFile.ExtractToDirectory(transfer.path, extractPath);

			foreach (string file in Directory.GetFiles(extractPath, "*.glb", SearchOption.AllDirectories))
			{
				ImportModelWithMaterial(
					new MeshyBridgeWindow.MeshTransfer
					{
						file_format = "glb",
						path = file,
						name = transfer.name,
						frameRate = transfer.frameRate
					},
					standOnGround);
			}

			foreach (string file in Directory.GetFiles(extractPath, "*.fbx", SearchOption.AllDirectories))
			{
				ImportFBXWithTextures(
					new MeshyBridgeWindow.MeshTransfer
					{
						file_format = "fbx",
						path = file,
						name = transfer.name,
						frameRate = transfer.frameRate
					},
					standOnGround);
			}

			Directory.Delete(extractPath, true);
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError(stage, "ZIP processing failed", e);
		}
	}

	void ImportFBXWithTextures(MeshyBridgeWindow.MeshTransfer transfer, bool standOnGround)
	{
		const string stage = "import-fbx";
		try
		{
			string importDir = EnsureImportRoot();
			string modelName = BuildSanitizedModelName(transfer.name);

			string modelFolderName = modelName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
			string modelDir = NormalizeAssetPath(Path.Combine(importDir, modelFolderName));
			Directory.CreateDirectory(modelDir);

			string fbxFileName = Path.GetFileName(transfer.path);
			string fbxRelativePath = NormalizeAssetPath(Path.Combine(modelDir, fbxFileName));
			CopySourceToAssetPath(transfer.path, fbxRelativePath);

			string sourceDir = Path.GetDirectoryName(transfer.path) ?? string.Empty;
			ImportTextureFiles(sourceDir, modelDir);

			AssetDatabase.Refresh();
			AssetDatabase.ImportAsset(fbxRelativePath, ImportAssetOptions.ForceUpdate);

			GameObject importedObject = AssetDatabase.LoadAssetAtPath<GameObject>(fbxRelativePath);
			if (!importedObject)
			{
				MeshyBridgeDiagnostics.LogWarning(stage, "Imported FBX object missing: " + fbxRelativePath);
				return;
			}

			importedObject.name = modelName;
			materialPolicy.FixMaterialTextureReferences(importedObject, modelDir);
			EditorUtility.SetDirty(importedObject);
			AssetDatabase.SaveAssets();

			InstantiateInScene(importedObject, fbxRelativePath, standOnGround, ensureAnimatorComponent: false,
				createAnimatorController: false,
				successStageLabel: "instantiate-fbx");

			MeshyBridgeDiagnostics.LogInfo(stage, "FBX model imported successfully: " + fbxRelativePath);
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError(stage, "FBX import failed", e);
		}
	}

	void ImportTextureFiles(string sourceDir, string targetDir)
	{
		const string stage = "texture-import";
		string[] textureExtensions = { "*.jpg", "*.jpeg", "*.png", "*.tga", "*.bmp", "*.tiff", "*.tif", "*.exr", "*.hdr" };
		const string normalMapKeyword = "texture_normal";

		var allTextureFiles = textureExtensions.SelectMany(
			extension => Directory.GetFiles(sourceDir, extension, SearchOption.AllDirectories));

		foreach (string sourcePath in allTextureFiles)
		{
			string relativePath = Path.GetRelativePath(sourceDir, sourcePath);
			string targetPath = NormalizeAssetPath(Path.Combine(targetDir, relativePath));

			Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? targetDir);
			File.Copy(sourcePath, targetPath, true);

			AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
			if (AssetImporter.GetAtPath(targetPath) is not TextureImporter textureImporter) continue;

			string lowerFileName = Path.GetFileName(targetPath).ToLowerInvariant();
			if (!lowerFileName.Contains(normalMapKeyword))
			{
				MeshyBridgeDiagnostics.LogInfo(stage, "Copied texture file: " + Path.GetFileName(targetPath));
				continue;
			}

			if (textureImporter.textureType == TextureImporterType.NormalMap)
				continue;

			textureImporter.textureType = TextureImporterType.NormalMap;
			textureImporter.SaveAndReimport();
			MeshyBridgeDiagnostics.LogInfo(stage,
				"Set texture type to Normal Map for: " + Path.GetFileName(targetPath));
		}
	}

	void InstantiateInScene(
		GameObject importedPrefab,
		string modelPath,
		bool standOnGround,
		bool ensureAnimatorComponent,
		bool createAnimatorController,
		string successStageLabel)
	{
		EditorApplication.delayCall += () =>
		{
			if (PrefabUtility.InstantiatePrefab(importedPrefab) is not GameObject sceneObject) return;

			sceneObject.transform.position = Vector3.zero;
			sceneObject.transform.localScale = Vector3.one;
			ApplyStandOnGroundPlacement(sceneObject, standOnGround);

			if (ensureAnimatorComponent && sceneObject.GetComponent<Animator>() == null)
				sceneObject.AddComponent<Animator>();

			if (createAnimatorController)
				CreateAnimatorControllerForMultipleClips(sceneObject, modelPath);

			Selection.activeGameObject = sceneObject;
			EditorSceneManager.MarkSceneDirty(sceneObject.scene);
			MeshyBridgeDiagnostics.LogInfo(successStageLabel,
				"Model successfully added to scene: " + sceneObject.name);
		};
	}

	static void ApplyStandOnGroundPlacement(GameObject sceneObject, bool standOnGround)
	{
		if (!standOnGround)
		{
			sceneObject.transform.rotation = Quaternion.identity;
			return;
		}

		Renderer[] renderers = sceneObject.GetComponentsInChildren<Renderer>();
		if (renderers.Length == 0)
			return;

		Bounds bounds = renderers[0].bounds;
		for (int i = 1; i < renderers.Length; i++)
			bounds.Encapsulate(renderers[i].bounds);

		if (bounds.size == Vector3.zero)
			return;

		sceneObject.transform.position = new Vector3(0, -bounds.min.y, 0);
	}

	void ConfigureImportedModelAnimation(string assetPath, string fileFormat)
	{
		if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
			return;

		importer.animationType = ModelImporterAnimationType.Generic;
		importer.importAnimation = true;

		if (!string.Equals(fileFormat, "glb", StringComparison.OrdinalIgnoreCase))
			return;

		importer.SaveAndReimport();
		AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().ToArray();
		if (clips.Length <= 1) return;

		MeshyBridgeDiagnostics.LogInfo("import-glb", "Found " + clips.Length + " animation clips in GLB file");
		foreach (AnimationClip clip in clips)
			MeshyBridgeDiagnostics.LogInfo("import-glb", "Animation clip: " + clip.name);
	}

	static void CreateAnimatorControllerForMultipleClips(GameObject sceneObject, string modelPath)
	{
		AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().ToArray();
		if (clips.Length <= 1) return;

		string controllerPath = modelPath.Replace(Path.GetExtension(modelPath), "_Controller.controller");
		AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

		for (int i = 0; i < clips.Length; i++)
		{
			AnimationClip clip = clips[i];
			AnimatorState state = controller.layers[0].stateMachine.AddState(clip.name);
			state.motion = clip;

			if (i == 0)
				controller.layers[0].stateMachine.defaultState = state;
		}

		if (sceneObject.GetComponent<Animator>() is { } animator)
			animator.runtimeAnimatorController = controller;

		MeshyBridgeDiagnostics.LogInfo("import-glb", "Created AnimatorController with " + clips.Length + " animation clips");
	}

	static string EnsureImportRoot()
	{
		const string importDir = "Assets/MeshyImports";
		if (!Directory.Exists(importDir))
		{
			Directory.CreateDirectory(importDir);
			AssetDatabase.Refresh();
		}

		return importDir;
	}

	static string BuildSanitizedModelName(string name)
	{
		string modelName = string.IsNullOrEmpty(name) ? "Meshy_Model" : name;
		return string.Join("_", modelName.Split(Path.GetInvalidFileNameChars()));
	}

	static string NormalizeAssetPath(string path)
	{
		return (path ?? string.Empty).Replace('\\', '/');
	}

	static void CopySourceToAssetPath(string sourcePath, string assetPath)
	{
		if (!File.Exists(sourcePath))
			throw new FileNotFoundException("Source file not found", sourcePath);

		File.Copy(sourcePath, assetPath, true);
	}

	static void CleanupTempFile(string path)
	{
		const string stage = "cleanup";
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError(stage, "Error cleaning up temp file: " + path, e);
		}
	}
}
