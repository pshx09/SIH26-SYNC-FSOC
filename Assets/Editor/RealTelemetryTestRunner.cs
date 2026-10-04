using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RealTelemetryTestRunner
{
    private static double targetDuration = 35.0;

    public static void Run35Seconds()
    {
        RunForDuration(35.0);
    }

    public static void Run120Seconds()
    {
        RunForDuration(120.0);
    }

    public static void RunIndefinitely()
    {
        targetDuration = double.MaxValue;
        EditorSceneManager.OpenScene("Assets/Scenes/FSOC_Main.unity");
        EditorApplication.isPlaying = true;
        EditorApplication.update += CheckTime;
    }

    private static void RunForDuration(double duration)
    {
        targetDuration = duration;
        EditorSceneManager.OpenScene("Assets/Scenes/FSOC_Main.unity");
        EditorApplication.isPlaying = true;
        EditorApplication.update += CheckTime;
    }

    private static double startTime;
    private static bool initialized = false;

    private static void CheckTime()
    {
        if (!initialized && EditorApplication.isPlaying)
        {
            startTime = EditorApplication.timeSinceStartup;
            initialized = true;
            Debug.Log("RealTelemetryTestRunner: Play Mode started, waiting 35 seconds...");
            
            var uav = Object.FindAnyObjectByType<UAVTrajectoryController>();
            if (uav != null)
            {
                uav.currentMode = UAVTrajectoryController.TrajectoryMode.StraightLine;
                uav.EstablishInitialAcquisitionGeometry();
                uav.ResetTrajectoryTiming();
                Debug.Log("Forced StraightLine mode.");
            }
        }

        if (initialized && EditorApplication.timeSinceStartup - startTime > targetDuration)
        {
            Debug.Log($"RealTelemetryTestRunner: {targetDuration} seconds elapsed. Quitting.");
            EditorApplication.isPlaying = false;
            EditorApplication.update -= CheckTime;
            EditorApplication.Exit(0);
        }
    }
}
