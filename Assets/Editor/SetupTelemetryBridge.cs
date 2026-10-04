using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FSOC.Dashboard;
using FSOC.Telemetry;
using FSC.Core;
using FSOC.AI;

public static class SetupTelemetryBridge
{
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FSOC_Main.unity");

        // Remove old if exists
        var old = GameObject.Find("TelemetryBridge");
        if (old != null) Object.DestroyImmediate(old);

        var bridge = new GameObject("TelemetryBridge");

        var bus = bridge.AddComponent<TelemetryBus>();
        var eventLog = bridge.AddComponent<DashboardEventLog>();
        
        var collector = bridge.AddComponent<TelemetryCollector>();
        collector.telemetryBus = bus;
        collector.eventLog = eventLog;
        
        // Find dependencies in scene
        collector.supervisor = Object.FindAnyObjectByType<TrackingSupervisor>();
        collector.panTiltTracker = Object.FindAnyObjectByType<PanTiltTracker>();
        collector.metrics = Object.FindAnyObjectByType<TrackingMetrics>();
        collector.trajectoryController = Object.FindAnyObjectByType<UAVTrajectoryController>();
        collector.detectorRouter = Object.FindAnyObjectByType<DetectorRouter>();
        
        // Find VirtualCamera
        var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in cams)
        {
            if (c.name == "VirtualCamera") collector.virtualCamera = c;
        }

        var publisher = bridge.AddComponent<UnityTelemetryWebSocketPublisher>();
        publisher.telemetryBus = bus;
        publisher.serverUrl = "ws://127.0.0.1:8000/ws/telemetry";
        publisher.publishRateHz = 15f;
        publisher.enablePublishing = true;

        var commandClient = bridge.AddComponent<UnityCommandWebSocketClient>();
        commandClient.enableCommands = true;
        commandClient.serverUrl = "ws://127.0.0.1:8000/ws/commands/unity";

        var runtime = bridge.AddComponent<SimulationRuntimeController>();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("SetupTelemetryBridge: Telemetry components configured and scene saved.");
    }
}
