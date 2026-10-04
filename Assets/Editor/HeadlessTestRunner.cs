using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using FSC.Core;
using System.IO;

public static class HeadlessTestRunner
{
    private static float _startTime;
    private static int _testState = 0;
    private static UAVTrajectoryController _uav;
    private static string _logPath;

    public static void RunTests()
    {
        Debug.Log("Starting Headless PS 26169 Phase 2 Acceptance Tests...");
        
        _logPath = Path.Combine(Application.dataPath, "../HeadlessTestResults.log");
        if (File.Exists(_logPath)) File.Delete(_logPath);
        Application.logMessageReceived += HandleLog;

        EditorSceneManager.OpenScene("Assets/Scenes/FSOC_Main.unity");
        EditorApplication.isPlaying = true;
        
        EditorApplication.update += OnUpdate;
    }

    private static void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (logString.StartsWith("[PTZState]") || logString.StartsWith("[Tracking]") || 
            logString.StartsWith("[TargetMotion]") || logString.StartsWith("[TrajectoryFeasibility]") || 
            logString.StartsWith("[Reacquisition]") || logString.StartsWith("[PSAcceptance]"))
        {
            File.AppendAllText(_logPath, logString + "\n\n");
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying) return;

        if (_testState == 0)
        {
            _startTime = Time.time;
            _uav = Object.FindAnyObjectByType<UAVTrajectoryController>();
            if (_uav != null)
            {
                _uav.currentMode = UAVTrajectoryController.TrajectoryMode.Circular;
                _uav.EstablishInitialAcquisitionGeometry();
                _uav.ApplyValidatedPhase2Parameters(UAVTrajectoryController.TrajectoryMode.Circular);
                _uav.ResetTrajectoryTiming();
                _testState = 1;
            }
            PanTiltTracker tracker = Object.FindAnyObjectByType<PanTiltTracker>();
            if (tracker != null)
            {
                tracker.ForceState(0f, 0f); // Face forward, Level
                tracker.runDiagnosticPlantTest = false;
            }

            if (_uav != null)
            {
                _uav.ForcePositionUpdate(); // Eliminate 1-frame latency ghost targets
            }

            TrackingSupervisor supervisor = Object.FindAnyObjectByType<TrackingSupervisor>();
            if (supervisor != null) supervisor.ResetState();
        }

        if (_uav == null) return;

        TrackingMetrics metrics = Object.FindAnyObjectByType<TrackingMetrics>();

        float elapsed = Time.time - _startTime;

        if (elapsed > 20f && _testState == 1)
        {
            Debug.Log("--- SWITCHING TO STRAIGHT LINE ---");
            _uav.currentMode = UAVTrajectoryController.TrajectoryMode.StraightLine;
            _uav.EstablishInitialAcquisitionGeometry();
            _uav.ApplyValidatedPhase2Parameters(UAVTrajectoryController.TrajectoryMode.StraightLine);
            _uav.ResetTrajectoryTiming();
            if (metrics != null) metrics.ResetAll();
            PanTiltTracker tracker = Object.FindAnyObjectByType<PanTiltTracker>();
            if (tracker != null) tracker.ForceState(0f, 0f);
            TrackingSupervisor supervisor = Object.FindAnyObjectByType<TrackingSupervisor>();
            if (supervisor != null) supervisor.ResetState();
            _uav.ForcePositionUpdate();
            _testState = 2;
        }
        else if (elapsed > 40f && _testState == 2)
        {
            Debug.Log("--- SWITCHING TO FIGURE-8 ---");
            _uav.currentMode = UAVTrajectoryController.TrajectoryMode.Figure8;
            _uav.EstablishInitialAcquisitionGeometry();
            _uav.ApplyValidatedPhase2Parameters(UAVTrajectoryController.TrajectoryMode.Figure8);
            _uav.ResetTrajectoryTiming();
            if (metrics != null) metrics.ResetAll();
            PanTiltTracker tracker = Object.FindAnyObjectByType<PanTiltTracker>();
            if (tracker != null) tracker.ForceState(0f, 0f);
            TrackingSupervisor supervisor = Object.FindAnyObjectByType<TrackingSupervisor>();
            if (supervisor != null) supervisor.ResetState();
            _uav.ForcePositionUpdate();
            _testState = 3;
        }
        else if (elapsed > 60f && _testState == 3)
        {
            Debug.Log("--- SWITCHING TO RANDOM ---");
            _uav.currentMode = UAVTrajectoryController.TrajectoryMode.Random;
            _uav.EstablishInitialAcquisitionGeometry();
            _uav.ApplyValidatedPhase2Parameters(UAVTrajectoryController.TrajectoryMode.Random);
            _uav.ResetTrajectoryTiming();
            if (metrics != null) metrics.ResetAll();
            PanTiltTracker tracker = Object.FindAnyObjectByType<PanTiltTracker>();
            if (tracker != null) tracker.ForceState(0f, 0f);
            TrackingSupervisor supervisor = Object.FindAnyObjectByType<TrackingSupervisor>();
            if (supervisor != null) supervisor.ResetState();
            _uav.ForcePositionUpdate();
            _testState = 4;
        }
        else if (elapsed > 80f && _testState == 4)
        {
            Debug.Log("--- FORCING TARGET MISS (LOCKED -> PREDICTING -> REACQUIRING) ---");
            // Teleport target extremely far out of FOV momentarily
            _uav.transform.position = new Vector3(10000, 10000, 10000);
            _testState = 5;
        }
        else if (elapsed > 100f && _testState == 5)
        {
            Debug.Log("--- TESTS COMPLETE ---");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
