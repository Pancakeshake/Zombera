using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[Serializable]
internal sealed class MeshyBridgeImportRequestData
{
	public string url;
	public string format;
	public string name;
	public int frameRate = 30;
}

[Serializable]
internal sealed class MeshyBridgeImportResponseData
{
	public string status;
	public string message;
	public string path;
}

[Serializable]
internal sealed class MeshyBridgeStatusResponseData
{
	public string status = "ok";
	public string dcc = "unity";
	public string version;
}

[Serializable]
internal sealed class MeshyBridgePathResponseData
{
	public string status;
}

[Serializable]
internal sealed class MeshyBridgeErrorResponseData
{
	public string status = "error";
	public string message;
}

internal sealed class MeshyBridgeHttpRequest
{
	internal string Method;
	internal string Path;
	internal string Body;
	internal readonly Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);

	internal string GetHeaderValue(string key)
	{
		if (string.IsNullOrWhiteSpace(key)) return string.Empty;
		return Headers.TryGetValue(key, out string value) ? value : string.Empty;
	}
}

internal sealed class MeshyBridgeHttpResponse
{
	internal int StatusCode;
	internal string ReasonPhrase;
	internal string Body;
	internal readonly Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);
}

internal static class MeshyBridgeHttpProtocol
{
	internal static string ReadRequest(NetworkStream stream)
	{
		byte[] buffer = new byte[1024 * 16];
		int bytesRead = stream.Read(buffer, 0, buffer.Length);
		return Encoding.UTF8.GetString(buffer, 0, bytesRead);
	}

	internal static bool TryParseRequest(string rawRequest, out MeshyBridgeHttpRequest request, out string error)
	{
		request = null;
		error = string.Empty;

		if (string.IsNullOrWhiteSpace(rawRequest))
		{
			error = "Empty request";
			return false;
		}

		int bodyIndex = rawRequest.IndexOf("\r\n\r\n", StringComparison.Ordinal);
		string headerSection = bodyIndex >= 0 ? rawRequest[..bodyIndex] : rawRequest;
		string bodySection = bodyIndex >= 0 ? rawRequest[(bodyIndex + 4)..] : string.Empty;

		string[] lines = headerSection.Split(new[] { "\r\n" }, StringSplitOptions.None);
		if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
		{
			error = "Invalid request line";
			return false;
		}

		string[] requestParts = lines[0].Split(' ');
		if (requestParts.Length < 2)
		{
			error = "Invalid request format";
			return false;
		}

		request = new MeshyBridgeHttpRequest
		{
			Method = requestParts[0],
			Path = requestParts[1],
			Body = bodySection
		};

		for (int i = 1; i < lines.Length; i++)
		{
			string line = lines[i];
			if (string.IsNullOrWhiteSpace(line)) continue;

			int separatorIndex = line.IndexOf(':');
			if (separatorIndex <= 0) continue;

			string key = line[..separatorIndex].Trim();
			string value = line[(separatorIndex + 1)..].Trim();
			if (string.IsNullOrWhiteSpace(key)) continue;

			request.Headers[key] = value;
		}

		return true;
	}

	internal static void WriteResponse(NetworkStream stream, MeshyBridgeHttpResponse response)
	{
		string reasonPhrase = string.IsNullOrWhiteSpace(response.ReasonPhrase) ? "OK" : response.ReasonPhrase;
		string body = response.Body ?? string.Empty;
		byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

		var sb = new StringBuilder();
		sb.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(reasonPhrase).Append("\r\n");

		if (!response.Headers.ContainsKey("Connection"))
			response.Headers["Connection"] = "close";
		if (!response.Headers.ContainsKey("Content-Length"))
			response.Headers["Content-Length"] = bodyBytes.Length.ToString();

		foreach (KeyValuePair<string, string> header in response.Headers)
			sb.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");

		sb.Append("\r\n");

		byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
		stream.Write(headerBytes, 0, headerBytes.Length);
		if (bodyBytes.Length > 0)
			stream.Write(bodyBytes, 0, bodyBytes.Length);
		stream.Flush();
	}
}

internal sealed class MeshyBridgeHttpRequestRouter
{
	static readonly string[] allowedOrigins =
	{
		"https://www.meshy.ai",
		"http://localhost:3700"
	};

	readonly Func<MeshyBridgeImportRequestData, MeshyBridgeImportIntakeResult> importRequestHandler;

	internal MeshyBridgeHttpRequestRouter(Func<MeshyBridgeImportRequestData, MeshyBridgeImportIntakeResult> importRequestHandler)
	{
		this.importRequestHandler = importRequestHandler;
	}

	internal void HandleClientStream(NetworkStream stream)
	{
		try
		{
			string rawRequest = MeshyBridgeHttpProtocol.ReadRequest(stream);
			MeshyBridgeDiagnostics.LogInfo("transport", $"Received request ({rawRequest.Length} bytes)");

			if (!MeshyBridgeHttpProtocol.TryParseRequest(rawRequest, out MeshyBridgeHttpRequest request, out string parseError))
			{
				MeshyBridgeDiagnostics.LogWarning("parse", parseError);
				SendErrorResponse(stream, parseError);
				return;
			}

			string origin = request.GetHeaderValue("Origin");

			if (request.Method == "OPTIONS")
			{
				SendOptionsResponse(stream, origin);
				return;
			}

			if (request.Method == "GET" && (request.Path == "/status" || request.Path == "/ping"))
			{
				SendStatusResponse(stream, origin);
				return;
			}

			if (request.Method == "POST" && request.Path == "/import")
			{
				ProcessImportRequest(stream, request, origin);
				return;
			}

			SendNotFoundResponse(stream, origin);
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError("transport", "Error processing request", e);
			SendErrorResponse(stream, e.Message);
		}
	}

	void ProcessImportRequest(NetworkStream stream, MeshyBridgeHttpRequest request, string origin)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(request.Body))
				throw new Exception("Invalid request format: JSON body missing");

			MeshyBridgeImportRequestData requestData = JsonUtility.FromJson<MeshyBridgeImportRequestData>(request.Body);
			if (requestData == null)
				throw new Exception("Invalid JSON format");

			MeshyBridgeImportIntakeResult intakeResult = importRequestHandler(requestData);
			if (!intakeResult.Success)
			{
				SendErrorResponse(stream, intakeResult.ErrorMessage);
				return;
			}

			MeshyBridgeImportResponseData responseData = new()
			{
				status = "ok",
				message = intakeResult.Message,
				path = intakeResult.FilePath
			};

			SendJsonResponse(stream, 200, "OK", responseData, origin);
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError("parse", "Error processing import request", e);
			SendErrorResponse(stream, e.Message);
		}
	}

	static string GetAllowedOrigin(string origin)
	{
		return Array.Exists(allowedOrigins, o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase))
			? origin
			: "https://www.meshy.ai";
	}

	static void SendOptionsResponse(NetworkStream stream, string origin)
	{
		var response = new MeshyBridgeHttpResponse
		{
			StatusCode = 200,
			ReasonPhrase = "OK",
			Body = string.Empty
		};
		response.Headers["Access-Control-Allow-Origin"] = GetAllowedOrigin(origin);
		response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS";
		response.Headers["Access-Control-Allow-Headers"] = "*";
		response.Headers["Access-Control-Max-Age"] = "86400";

		MeshyBridgeHttpProtocol.WriteResponse(stream, response);
	}

	static void SendStatusResponse(NetworkStream stream, string origin)
	{
		MeshyBridgeStatusResponseData responseData = new()
		{
			status = "ok",
			dcc = "unity",
			version = Application.unityVersion
		};

		SendJsonResponse(stream, 200, "OK", responseData, origin);
	}

	static void SendNotFoundResponse(NetworkStream stream, string origin)
	{
		MeshyBridgePathResponseData responseData = new() { status = "path not found" };
		SendJsonResponse(stream, 404, "Not Found", responseData, origin);
	}

	static void SendErrorResponse(NetworkStream stream, string message)
	{
		MeshyBridgeErrorResponseData responseData = new() { message = message };
		string jsonBody = JsonUtility.ToJson(responseData);

		var response = new MeshyBridgeHttpResponse
		{
			StatusCode = 500,
			ReasonPhrase = "Internal Server Error",
			Body = jsonBody
		};
		response.Headers["Access-Control-Allow-Origin"] = "*";
		response.Headers["Content-Type"] = "application/json; charset=utf-8";
		MeshyBridgeHttpProtocol.WriteResponse(stream, response);
	}

	static void SendJsonResponse(NetworkStream stream, int statusCode, string reasonPhrase, object payload, string origin)
	{
		string jsonBody = JsonUtility.ToJson(payload);
		var response = new MeshyBridgeHttpResponse
		{
			StatusCode = statusCode,
			ReasonPhrase = reasonPhrase,
			Body = jsonBody
		};
		response.Headers["Access-Control-Allow-Origin"] = GetAllowedOrigin(origin);
		response.Headers["Content-Type"] = "application/json; charset=utf-8";

		MeshyBridgeHttpProtocol.WriteResponse(stream, response);
	}
}
