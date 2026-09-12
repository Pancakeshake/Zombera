using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum RenderPipeline
{
	Unsupported,
	BuiltIn,
	URP,
	HDRP
}

public partial class MeshyBridgeWindow : EditorWindow
{
	static string _tempCachePath;

	static readonly Queue<MeshTransfer> importQueue = new();
	static readonly object importQueueLock = new();

	static MeshyBridgeTransportServer transportServer;
	static MeshyBridgeHttpRequestRouter requestRouter;
	static MeshyBridgeImportIntake importIntake;
	static MeshyBridgeImportOrchestrator importOrchestrator;
	static bool servicesInitialized;

	public static bool IsRunning { get; set; }

	GUIContent runButtonContent;
	GUIContent stopButtonContent;

	static bool _standOnGround = true;

	[Serializable]
	public class MeshTransfer
	{
		public string file_format;
		public string path;
		public string name;
		public int frameRate;
	}

	[MenuItem("Meshy/Bridge")]
	public static void ShowWindow()
	{
		MeshyBridgeWindow window = GetWindow<MeshyBridgeWindow>("Meshy Bridge");
		window.minSize = new(250, 120);
		window.maxSize = new(400, 170);
	}

	void OnEnable()
	{
		runButtonContent = new("Run Bridge");
		stopButtonContent = new("Bridge ON");

		_tempCachePath = Application.temporaryCachePath;
		EnsureServicesInitialized();
		EditorApplication.update += Update;
		StartServer();
	}

	void OnDisable()
	{
		EditorApplication.update -= Update;
		StopServer(true);
	}

	void OnGUI()
	{
		EditorGUILayout.BeginVertical();
		GUILayout.Space(10);
		GUIStyle buttonStyle = new(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 40 };
		Color originalColor = GUI.backgroundColor;
		if (IsRunning) GUI.backgroundColor = new Color(0.4f, 0.6f, 1.0f);
		GUIContent currentContent = IsRunning ? stopButtonContent : runButtonContent;
		if (GUILayout.Button(currentContent, buttonStyle)) ToggleBridgeState();
		GUI.backgroundColor = originalColor;
		GUILayout.Space(5);
		_standOnGround = EditorGUILayout.Toggle(
			new GUIContent("Stand on Ground", "If enabled, imported models will be placed on the Y=0 plane."),
			_standOnGround);
		EditorGUILayout.EndVertical();
		Repaint();
	}

	void ToggleBridgeState()
	{
		if (IsRunning) StopServer();
		else StartServer();
	}

	static void EnsureServicesInitialized()
	{
		if (servicesInitialized) return;

		importOrchestrator = new MeshyBridgeImportOrchestrator(new MeshyBridgeMaterialPolicy());
		importIntake = new MeshyBridgeImportIntake(EnqueueTransfer);
		requestRouter = new MeshyBridgeHttpRequestRouter(HandleImportRequest);
		transportServer = new MeshyBridgeTransportServer(requestRouter.HandleClientStream,
			running => { IsRunning = running; });

		servicesInitialized = true;
	}

	public static RenderPipeline GetActiveRenderPipeline()
	{
		return MeshyBridgeMaterialPolicy.GetActiveRenderPipeline();
	}
}
