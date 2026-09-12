using UnityEditor;

public partial class MeshyBridgeWindow
{
	const int MaxTransfersPerFrame = 1;
	const double MaxQueueProcessingMilliseconds = 8.0;

	static void Update()
	{
		if (!servicesInitialized || importOrchestrator == null) return;

		int processed = 0;
		double frameStart = EditorApplication.timeSinceStartup;
		while (processed < MaxTransfersPerFrame)
		{
			if (!TryDequeueTransfer(out MeshTransfer transfer))
				break;

			importOrchestrator.ProcessTransfer(transfer, _standOnGround, _tempCachePath);
			processed++;

			double elapsedMs = (EditorApplication.timeSinceStartup - frameStart) * 1000.0;
			if (elapsedMs >= MaxQueueProcessingMilliseconds)
				break;
		}
	}

	static void EnqueueTransfer(MeshTransfer transfer)
	{
		if (transfer == null) return;

		lock (importQueueLock)
		{
			importQueue.Enqueue(transfer);
		}
	}

	static bool TryDequeueTransfer(out MeshTransfer transfer)
	{
		lock (importQueueLock)
		{
			if (importQueue.Count > 0)
			{
				transfer = importQueue.Dequeue();
				return true;
			}
		}

		transfer = null;
		return false;
	}
}
