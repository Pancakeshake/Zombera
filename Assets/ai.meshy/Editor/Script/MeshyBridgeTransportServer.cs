using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

internal sealed class MeshyBridgeTransportServer
{
	readonly Action<NetworkStream> requestHandler;
	readonly Action<bool> runningStateChanged;

	Thread serverThread;
	TcpListener listener;
	volatile bool stopRequested;

	internal bool IsRunning { get; private set; }

	internal MeshyBridgeTransportServer(Action<NetworkStream> requestHandler, Action<bool> runningStateChanged)
	{
		this.requestHandler = requestHandler;
		this.runningStateChanged = runningStateChanged;
	}

	internal void Start()
	{
		if (IsRunning || serverThread is { IsAlive: true })
			return;

		stopRequested = false;
		serverThread = new Thread(RunServerLoop) { IsBackground = true };
		serverThread.Start();
	}

	internal void Stop(bool blocking)
	{
		stopRequested = true;
		try
		{
			listener?.Stop();
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogWarning("server", "Listener stop warning: " + e.Message);
		}

		if (!blocking) return;

		serverThread?.Join();
	}

	void RunServerLoop()
	{
		try
		{
			listener = new TcpListener(IPAddress.Any, 5326);
			listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			listener.Start();

			IsRunning = true;
			runningStateChanged?.Invoke(true);
			MeshyBridgeDiagnostics.LogInfo("server", "Listening on port 5326");

			while (!stopRequested)
			{
				if (!listener.Pending())
				{
					Thread.Sleep(25);
					continue;
				}

				using TcpClient client = listener.AcceptTcpClient();
				using NetworkStream stream = client.GetStream();
				requestHandler?.Invoke(stream);
			}
		}
		catch (SocketException e) when (stopRequested || e.SocketErrorCode == SocketError.Interrupted)
		{
			// Expected when listener is stopped.
		}
		catch (ObjectDisposedException) when (stopRequested)
		{
			// Expected when listener is disposed while stopping.
		}
		catch (Exception e)
		{
			MeshyBridgeDiagnostics.LogError("server", "Server run error", e);
		}
		finally
		{
			try
			{
				listener?.Stop();
			}
			catch (Exception e)
			{
				MeshyBridgeDiagnostics.LogWarning("server", "Listener shutdown warning: " + e.Message);
			}

			IsRunning = false;
			runningStateChanged?.Invoke(false);
			MeshyBridgeDiagnostics.LogInfo("server", "Server stopped");
		}
	}
}
