using UnityEngine;

public partial class MeshyBridgeWindow
{
	public static void StartServer()
	{
		EnsureServicesInitialized();
		if (IsRunning) return;

		MeshyBridgeDiagnostics.LogInfo("server", "Starting server");
		transportServer.Start();
	}

	public static void StopServer(bool blocking = false)
	{
		if (!servicesInitialized || transportServer == null) return;

		if (!transportServer.IsRunning && !IsRunning) return;

		MeshyBridgeDiagnostics.LogInfo("server", "Stopping server");
		transportServer.Stop(blocking);
	}

	static MeshyBridgeImportIntakeResult HandleImportRequest(MeshyBridgeImportRequestData requestData)
	{
		EnsureServicesInitialized();
		return importIntake.HandleImportRequest(requestData);
	}
}
