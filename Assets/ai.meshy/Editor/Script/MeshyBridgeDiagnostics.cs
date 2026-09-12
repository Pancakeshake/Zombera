using System;
using UnityEngine;

internal static class MeshyBridgeDiagnostics
{
	internal static void LogInfo(string stage, string message)
	{
		Debug.Log($"[Meshy Bridge][stage:{stage}] {message}");
	}

	internal static void LogWarning(string stage, string message)
	{
		Debug.LogWarning($"[Meshy Bridge][stage:{stage}] {message}");
	}

	internal static void LogError(string stage, string message, Exception exception = null)
	{
		if (exception == null)
		{
			Debug.LogError($"[Meshy Bridge][stage:{stage}] {message}");
			return;
		}

		Debug.LogError($"[Meshy Bridge][stage:{stage}] {message}\n{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
	}
}
