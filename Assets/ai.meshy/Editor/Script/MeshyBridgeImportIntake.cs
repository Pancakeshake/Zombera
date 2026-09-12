using System;
using System.IO;
using System.Net;

internal readonly struct MeshyBridgeImportIntakeResult
{
	internal readonly bool Success;
	internal readonly string FilePath;
	internal readonly string Message;
	internal readonly string ErrorMessage;

	MeshyBridgeImportIntakeResult(bool success, string filePath, string message, string errorMessage)
	{
		Success = success;
		FilePath = filePath;
		Message = message;
		ErrorMessage = errorMessage;
	}

	internal static MeshyBridgeImportIntakeResult Ok(string filePath, string message)
	{
		return new MeshyBridgeImportIntakeResult(true, filePath, message, string.Empty);
	}

	internal static MeshyBridgeImportIntakeResult Fail(string error)
	{
		return new MeshyBridgeImportIntakeResult(false, string.Empty, string.Empty, error);
	}
}

internal sealed class MeshyBridgeImportIntake
{
	readonly Action<MeshyBridgeWindow.MeshTransfer> enqueueTransfer;

	internal MeshyBridgeImportIntake(Action<MeshyBridgeWindow.MeshTransfer> enqueueTransfer)
	{
		this.enqueueTransfer = enqueueTransfer;
	}

	internal MeshyBridgeImportIntakeResult HandleImportRequest(MeshyBridgeImportRequestData data)
	{
		const string stage = "download";
		try
		{
			if (data == null)
				return MeshyBridgeImportIntakeResult.Fail("Import payload was null");
			if (string.IsNullOrWhiteSpace(data.url))
				return MeshyBridgeImportIntakeResult.Fail("Missing import URL");
			if (string.IsNullOrWhiteSpace(data.format))
				return MeshyBridgeImportIntakeResult.Fail("Missing import format");

			string fileName = $"bridge_model_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
			string basePath = Path.Combine(Path.GetTempPath(), "Meshy", fileName);

			Directory.CreateDirectory(Path.GetDirectoryName(basePath) ?? string.Empty);
			if (File.Exists(basePath))
				File.Delete(basePath);

			MeshyBridgeDiagnostics.LogInfo(stage, "Downloading model from " + data.url);
			using (WebClient client = new())
				client.DownloadFile(data.url, basePath);

			string extension = ResolveDownloadedFileExtension(basePath, data.format);
			string finalPath = basePath + extension;
			if (File.Exists(finalPath))
				File.Delete(finalPath);

			File.Move(basePath, finalPath);

			enqueueTransfer(
				new MeshyBridgeWindow.MeshTransfer
				{
					file_format = data.format,
					path = finalPath,
					name = data.name ?? string.Empty,
					frameRate = data.frameRate
				});

			MeshyBridgeDiagnostics.LogInfo(stage, "Queued downloaded file for import: " + finalPath);
			return MeshyBridgeImportIntakeResult.Ok(finalPath, "File queued for import");
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError(stage, "Failed to intake import request", e);
			return MeshyBridgeImportIntakeResult.Fail(e.Message);
		}
	}

	static string ResolveDownloadedFileExtension(string downloadedPath, string format)
	{
		string fileExtension = ".glb";
		byte[] header = new byte[4];

		using (FileStream fs = new(downloadedPath, FileMode.Open, FileAccess.Read))
		{
			_ = fs.Read(header, 0, header.Length);

			string normalizedFormat = (format ?? string.Empty).ToLowerInvariant();
			if (normalizedFormat == "glb")
			{
				if (header[0] == 'P' && header[1] == 'K' && header[2] == 0x03 && header[3] == 0x04)
					fileExtension = ".zip";
				else if (header[0] == 'g' && header[1] == 'l' && header[2] == 'T' && header[3] == 'F')
					fileExtension = ".glb";
			}
			else if (normalizedFormat == "fbx")
			{
				if (header[0] == 'P' && header[1] == 'K' && header[2] == 0x03 && header[3] == 0x04)
					fileExtension = ".zip";
				else
					fileExtension = ".fbx";
			}
		}

		return fileExtension;
	}
}
