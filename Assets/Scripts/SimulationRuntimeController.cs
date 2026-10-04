using UnityEngine;
using FSOC.Contracts;

namespace FSC.Core
{
    [DefaultExecutionOrder(-50)]
    public class SimulationRuntimeController : MonoBehaviour
    {
        public static SimulationRuntimeController Instance { get; private set; }

        public RunState CurrentState { get; private set; } = RunState.RUNNING;
        public int RunId { get; private set; } = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartSimulation()
        {
            if (CurrentState == RunState.WAITING || CurrentState == RunState.STOPPED)
            {
                RunId++;
                TrackingMetrics metrics = FindFirstObjectByType<TrackingMetrics>();
                if (metrics != null) metrics.ResetAll();
            }
            Time.timeScale = 1f;
            CurrentState = RunState.RUNNING;
            Debug.Log($"[SimulationRuntimeController] STARTED. RunId: {RunId}");
        }

        public void PauseSimulation()
        {
            // Set timeScale to 0 to pause dynamics (physics, animations, movement).
            // We keep the application running so commands/telemetry still flow.
            Time.timeScale = 0f;
            CurrentState = RunState.PAUSED;
            Debug.Log("[SimulationRuntimeController] PAUSED");
        }

        public void ResumeSimulation()
        {
            Time.timeScale = 1f;
            CurrentState = RunState.RUNNING;
            Debug.Log("[SimulationRuntimeController] RESUMED");
        }

        public void StopSimulation()
        {
            // Stops dynamics and resets state if necessary, but keep the scene loaded
            Time.timeScale = 0f;
            CurrentState = RunState.STOPPED;
            Debug.Log("[SimulationRuntimeController] STOPPED");
        }

        public void ResetSimulation()
        {
            // Resume timescale
            Time.timeScale = 1f;
            
            // Re-establish trajectory geometry 
            UAVTrajectoryController uav = FindFirstObjectByType<UAVTrajectoryController>();
            if (uav != null)
            {
                uav.EstablishInitialAcquisitionGeometry();
                uav.ApplyValidatedPhase2Parameters(uav.currentMode);
                // Snap to initial position immediately
                uav.transform.position = new Vector3(uav.orbitCenter.x, uav.orbitCenter.y + uav.altitude, uav.orbitCenter.z);
            }
            
            // Re-center PTZ
            PanTiltTracker ptz = FindFirstObjectByType<PanTiltTracker>();
            if (ptz != null)
            {
                ptz.ForceState(0f, 0f);
            }

            // Reset Metrics
            TrackingMetrics metrics = FindFirstObjectByType<TrackingMetrics>();
            if (metrics != null)
            {
                metrics.ResetAll();
            }

            CurrentState = RunState.WAITING; // Changed to WAITING until explicitly RUN
            Debug.Log($"[SimulationRuntimeController] RESET to initial state.");
        }
    }
}
