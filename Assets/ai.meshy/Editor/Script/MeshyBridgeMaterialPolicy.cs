using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class MeshyBridgeMaterialPolicy
{
	internal void ApplyDefaultMaterial(GameObject obj)
	{
		Renderer renderer = obj.GetComponent<Renderer>();
		if (renderer == null || Array.Exists(renderer.sharedMaterials, material => material != null))
			return;

		RenderPipeline pipeline = GetActiveRenderPipeline();
		Shader shader = ResolvePipelineShader(pipeline);
		if (shader == null)
		{
			MeshyBridgeDiagnostics.LogWarning("material-policy",
				"Could not find a pipeline shader. Falling back to Standard.");
			shader = Shader.Find("Standard");
		}

		Material material = new(shader) { name = "Meshy_Material" };

		if (renderer.sharedMaterials.Length == 0)
			renderer.sharedMaterial = material;
		else
		{
			Material[] materials = renderer.sharedMaterials;
			for (int i = 0; i < materials.Length; i++)
				if (materials[i] == null)
					materials[i] = material;

			renderer.sharedMaterials = materials;
		}
	}

	internal void FixMaterialTextureReferences(GameObject fbxObject, string modelDir)
	{
		const string stage = "texture-bind";
		try
		{
			RenderPipeline pipeline = GetActiveRenderPipeline();
			Renderer[] renderers = fbxObject.GetComponentsInChildren<Renderer>();

			foreach (Renderer renderer in renderers)
			{
				Material[] sharedMaterials = renderer.sharedMaterials;
				Material[] newMaterials = new Material[sharedMaterials.Length];
				for (int i = 0; i < sharedMaterials.Length; i++)
				{
					Material originalMaterial = sharedMaterials[i];
					if (originalMaterial == null)
					{
						newMaterials[i] = null;
						continue;
					}

					Material material = new(originalMaterial);
					Shader newShader = ResolvePipelineShader(pipeline);
					if (newShader != null) material.shader = newShader;

					string shaderName = material.shader.name;
					bool isURP = shaderName.Contains("Universal Render Pipeline", StringComparison.Ordinal);
					bool isHDRP = shaderName.Contains("HDRP", StringComparison.Ordinal);
					string albedoPropertyName = isURP ? "_BaseMap" : isHDRP ? "_BaseColorMap" : "_MainTex";
					if (material.HasProperty(albedoPropertyName) && material.GetTexture(albedoPropertyName) == null)
					{
						Texture2D albedoTexture =
							FindTextureInDirectory(modelDir, originalMaterial.name) ??
							FindTextureInDirectory(modelDir, "albedo") ??
							FindTextureInDirectory(modelDir, "diffuse") ??
							FindTextureInDirectory(modelDir, "basecolor") ??
							FindTextureInDirectory(modelDir, "base_color") ??
							FindFirstTextureInDirectory(modelDir);
						if (albedoTexture != null)
						{
							material.SetTexture(albedoPropertyName, albedoTexture);
							MeshyBridgeDiagnostics.LogInfo(stage,
								"Set " + albedoPropertyName + " for material " + material.name + ": " + albedoTexture.name);
						}
					}

					if (isURP)
					{
						if (CheckAndAssignTexture(material, "_BumpMap", modelDir, "normal", "Normal"))
							material.SetFloat("_BumpScale", 0.5f);
						EnsureNormalMapTextureType(material, "_BumpMap");

						CheckAndAssignTexture(material, "_MetallicGlossMap", modelDir, "metallic", "Metallic");
						CheckAndAssignTexture(material, "_OcclusionMap", modelDir, "occlusion", "AO", "ambient_occlusion");
						CheckAndAssignTexture(material, "_EmissionMap", modelDir, "emission", "Emissive");

						if (material.GetTexture("_MetallicGlossMap") == null && material.HasProperty("_Smoothness"))
							material.SetFloat("_Smoothness", 0.5f);
					}
					else if (isHDRP)
					{
						CheckAndAssignTexture(material, "_NormalMap", modelDir, "normal", "Normal");
						EnsureNormalMapTextureType(material, "_NormalMap");
						CheckAndAssignTexture(material, "_EmissiveColorMap", modelDir, "emission", "Emissive");
					}
					else
					{
						CheckAndAssignTexture(material, "_BumpMap", modelDir, "normal", "Normal");
						EnsureNormalMapTextureType(material, "_BumpMap");

						CheckAndAssignTexture(material, "_MetallicGlossMap", modelDir, "metallic", "Metallic");
						CheckAndAssignTexture(material, "_OcclusionMap", modelDir, "occlusion", "AO", "ambient_occlusion");
						CheckAndAssignTexture(material, "_EmissionMap", modelDir, "emission", "Emissive");
						if (material.GetTexture("_MetallicGlossMap") == null && material.HasProperty("_Glossiness"))
							material.SetFloat("_Glossiness", 0.5f);
					}

					string materialPath = Path.Combine(modelDir, material.name.Replace("(Instance)", string.Empty).Trim() + ".mat")
						.Replace('\\', '/');
					AssetDatabase.CreateAsset(material, materialPath);
					newMaterials[i] = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
					MeshyBridgeDiagnostics.LogInfo(stage, "Created and saved material asset at: " + materialPath);
				}

				renderer.sharedMaterials = newMaterials;
			}
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError(stage, "Failed to fix material texture references", e);
		}
	}

	static bool CheckAndAssignTexture(Material material, string propertyName, string modelDir, params string[] nameKeywords)
	{
		if (!material.HasProperty(propertyName) || material.GetTexture(propertyName) != null) return false;

		foreach (string keyword in nameKeywords)
		{
			Texture2D texture = FindTextureInDirectory(modelDir, keyword);
			if (texture == null)
				continue;

			bool isNormalMapProperty = propertyName == "_BumpMap" || propertyName == "_NormalMap";
			if (isNormalMapProperty)
			{
				string texturePath = AssetDatabase.GetAssetPath(texture);
				if (AssetImporter.GetAtPath(texturePath) is TextureImporter textureImporter &&
				    textureImporter.textureType != TextureImporterType.NormalMap)
				{
					textureImporter.textureType = TextureImporterType.NormalMap;
					textureImporter.SaveAndReimport();
					MeshyBridgeDiagnostics.LogInfo("texture-bind",
						"Set texture type to Normal Map for: " + texture.name);
					texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
				}
			}

			material.SetTexture(propertyName, texture);
			MeshyBridgeDiagnostics.LogInfo("texture-bind",
				"Set " + propertyName + " texture for material " + material.name + ": " + texture.name);
			return true;
		}

		return false;
	}

	static void EnsureNormalMapTextureType(Material material, string propertyName)
	{
		if (!material.HasProperty(propertyName)) return;

		Texture texture = material.GetTexture(propertyName);
		if (texture == null) return;

		string texturePath = AssetDatabase.GetAssetPath(texture);
		if (string.IsNullOrEmpty(texturePath)) return;

		if (AssetImporter.GetAtPath(texturePath) is TextureImporter textureImporter &&
		    textureImporter.textureType != TextureImporterType.NormalMap)
		{
			textureImporter.textureType = TextureImporterType.NormalMap;
			textureImporter.SaveAndReimport();
			MeshyBridgeDiagnostics.LogInfo("texture-bind", "Fixed texture type to Normal Map for: " + texture.name);
		}
	}

	static Texture2D FindTextureInDirectory(string directory, string nameKeyword)
	{
		string[] textureExtensions = { "*.jpg", "*.jpeg", "*.png", "*.tga", "*.bmp", "*.tiff", "*.tif" };

		foreach (string pattern in textureExtensions)
		{
			string[] textureFiles = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
			foreach (string textureFile in textureFiles)
			{
				string fileName = Path.GetFileNameWithoutExtension(textureFile);
				if (!fileName.Contains(nameKeyword, StringComparison.OrdinalIgnoreCase))
					continue;

				string relativePath = textureFile.Replace('\\', '/');
				return AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
			}
		}

		return null;
	}

	static Texture2D FindFirstTextureInDirectory(string directory)
	{
		string[] textureExtensions = { "*.jpg", "*.jpeg", "*.png", "*.tga", "*.bmp", "*.tiff", "*.tif" };

		foreach (string pattern in textureExtensions)
		{
			string[] textureFiles = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
			if (textureFiles.Length == 0)
				continue;

			string relativePath = textureFiles[0].Replace('\\', '/');
			return AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
		}

		return null;
	}

	static Shader ResolvePipelineShader(RenderPipeline pipeline)
	{
		switch (pipeline)
		{
			case RenderPipeline.URP:
				return Shader.Find("Universal Render Pipeline/Lit");
			case RenderPipeline.HDRP:
				return Shader.Find("HDRP/Lit");
			default:
				return Shader.Find("Standard");
		}
	}

	internal static RenderPipeline GetActiveRenderPipeline()
	{
		if (GraphicsSettings.currentRenderPipeline == null)
			return RenderPipeline.BuiltIn;

		string pipelineAssetName = GraphicsSettings.currentRenderPipeline.GetType().Name;
		if (pipelineAssetName.Contains("UniversalRenderPipelineAsset", StringComparison.Ordinal))
			return RenderPipeline.URP;
		if (pipelineAssetName.Contains("HDRenderPipelineAsset", StringComparison.Ordinal))
			return RenderPipeline.HDRP;

		return RenderPipeline.Unsupported;
	}
}
