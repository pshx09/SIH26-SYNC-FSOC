using UnityEngine;
using FSOC.Contracts;
using FSC.Core;
using FSOC.AI;

namespace FSOC.Dashboard
{
    [DefaultExecutionOrder(60)] // Run after tracking logic
    public class TelemetryCollector : MonoBehaviour
    {
        [Header("Dependencies")]
        public TrackingSupervisor supervisor;
        public PanTiltTracker panTiltTracker;
        public TrackingMetrics metrics;
        public Camera virtualCamera;
        public UAVTrajectoryController trajectoryController;
        
        [Tooltip("The detector router used to identify active AI/Classical state.")]
        public DetectorRouter detectorRouter;

        [Header("Outputs")]
        public TelemetryBus telemetryBus;
        public DashboardEventLog eventLog;

        public float refreshRateHz = 15f;
        private float _lastRefreshTime = 0f;

        // For event detection
        private FSOC.Contracts.TrackingState _lastState = FSOC.Contracts.TrackingState.Searching;

        private void Awake()
        {
            Application.runInBackground = true;

            // Auto-wire dependencies in case they are missing from the scene inspector
            if (supervisor == null) supervisor = FindFirstObjectByType<TrackingSupervisor>();
            
            if (supervisor != null)
            {
                if (panTiltTracker == null) panTiltTracker = supervisor.panTiltTracker;
                if (metrics == null) metrics = supervisor.trackingMetrics;
            }

            if (panTiltTracker == null) panTiltTracker = FindFirstObjectByType<PanTiltTracker>();
            if (metrics == null) metrics = FindFirstObjectByType<TrackingMetrics>();
            if (trajectoryController == null) trajectoryController = FindFirstObjectByType<UAVTrajectoryController>();
            
            if (virtualCamera == null)
            {
                foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (c.name == "VirtualCamera")
                    {
                        virtualCamera = c;
                        break;
                    }
                }
            }

            // Auto-setup Command Client and Runtime Controller
            if (GetComponent<UnityCommandWebSocketClient>() == null)
            {
                var cmdClient = gameObject.AddComponent<UnityCommandWebSocketClient>();
                cmdClient.enableCommands = true;
                cmdClient.serverUrl = "ws://127.0.0.1:8000/ws/commands/unity";
            }
            if (GetComponent<FSC.Core.SimulationRuntimeController>() == null)
            {
                gameObject.AddComponent<FSC.Core.SimulationRuntimeController>();
            }
        }

        private void Update()
        {
            if (telemetryBus == null) return;

            // Enforce refresh rate (e.g. 15 Hz for dashboard updates)
            if (Time.unscaledTime - _lastRefreshTime < (1f / refreshRateHz)) return;
            _lastRefreshTime = Time.unscaledTime;

            TelemetrySnapshot snapshot = new TelemetrySnapshot();
            snapshot.Timestamp = Time.time;
            
            var runtime = FSC.Core.SimulationRuntimeController.Instance;
            if (runtime != null)
            {
                snapshot.RunId = runtime.RunId;
                snapshot.RunState = runtime.CurrentState.ToString();
            }

            if (VideoInputAdapter.Instance != null && VideoInputAdapter.Instance.isVideoModeActive)
            {
                snapshot.InputMode = InputMode.MP4;
            }
            else
            {
                snapshot.InputMode = InputMode.VirtualSensor;
            }

            if (snapshot.InputMode == InputMode.MP4 && VideoInputAdapter.Instance.videoPlayer != null)
            {
                var videoPlayer = VideoInputAdapter.Instance.videoPlayer;
                snapshot.VideoFrame = videoPlayer.frame > int.MaxValue ? int.MaxValue : (int)videoPlayer.frame;
                snapshot.VideoTime = (float)videoPlayer.time;
                snapshot.VideoDuration = (float)videoPlayer.length;
                snapshot.VideoFrameRate = (float)videoPlayer.frameRate;
                snapshot.VideoFrameCount = videoPlayer.frameCount > int.MaxValue ? int.MaxValue : (int)videoPlayer.frameCount;
                snapshot.VideoIsPlaying = videoPlayer.isPlaying;
            }

            if (metrics != null)
            {
                // Map Tracking State
                snapshot.TrackingState = MapState(metrics.CurrentState);

                // Check for state change events
                if (snapshot.TrackingState != _lastState)
                {
                    eventLog?.LogEvent($"STATE CHANGED: {snapshot.TrackingState.ToString().ToUpper()}");
                    
                    if (snapshot.TrackingState == FSOC.Contracts.TrackingState.Locked)
                        eventLog?.LogEvent("TRACKING LOCKED");
                    else if (snapshot.TrackingState == FSOC.Contracts.TrackingState.Lost)
                        eventLog?.LogEvent("TARGET LOST");
                    else if (snapshot.TrackingState == FSOC.Contracts.TrackingState.Acquiring)
                        eventLog?.LogEvent("TRACKING ACQUIRED");
                    else if (snapshot.TrackingState == FSOC.Contracts.TrackingState.Reacquiring)
                        eventLog?.LogEvent("RE-ACQUISITION STARTED");

                    _lastState = snapshot.TrackingState;
                }

                // Spatial Error
                snapshot.ErrorX = metrics.ErrorX;
                snapshot.ErrorY = metrics.ErrorY;
                snapshot.RadialError = metrics.RadialError;
                snapshot.MeanError = metrics.MeanError;
                snapshot.RMSE = metrics.RMSE;
                snapshot.MaxError = metrics.MaxError;
                
                // Detection/Tracking Stats
                snapshot.FramesWithin10px = (int)(metrics.PercentFramesWithin10px); 
                snapshot.TargetLossRate = metrics.LossRate * 100f; 
                snapshot.LockRetention = metrics.LockRetentionPercentage;
                snapshot.AcquisitionTime = metrics.AcquisitionTimeSec;
                snapshot.ReacquisitionTime = metrics.WorstReacquisitionTimeSec;

                // System Performance
                snapshot.FPS = metrics.ProcessingFPS;
                snapshot.PTZUpdateRate = metrics.PTZUpdateRate;
                snapshot.FrameTimeMs = Time.unscaledDeltaTime * 1000f;
                
                // Fallback: If metrics has the detector, use it
                if (metrics.beaconDetector != null)
                {
                    snapshot.IsDetected = metrics.beaconDetector.isDetected;
                    snapshot.CentroidX = metrics.beaconDetector.centroid.x;
                    snapshot.CentroidY = metrics.beaconDetector.centroid.y;
                    snapshot.BoundingBoxWidth = metrics.beaconDetector.boundingBox.width;
                    snapshot.BoundingBoxHeight = metrics.beaconDetector.boundingBox.height;
                    snapshot.DetectionConfidence = metrics.beaconDetector.isDetected ? metrics.beaconDetector.confidence : 0f;
                    if (snapshot.IsDetected && snapshot.DetectionConfidence == 0f) snapshot.DetectionConfidence = 1.0f;
                }
            }
            
            if (supervisor != null)
            {
                if (supervisor.beaconDetector != null)
                {
                    snapshot.IsDetected = supervisor.beaconDetector.isDetected;
                    snapshot.CentroidX = supervisor.beaconDetector.centroid.x;
                    snapshot.CentroidY = supervisor.beaconDetector.centroid.y;
                    snapshot.BoundingBoxWidth = supervisor.beaconDetector.boundingBox.width;
                    snapshot.BoundingBoxHeight = supervisor.beaconDetector.boundingBox.height;
                    
                    snapshot.DetectionConfidence = supervisor.beaconDetector.isDetected ? supervisor.beaconDetector.confidence : 0f;
                    
                    if (supervisor.beaconDetector.isDetected && snapshot.DetectionConfidence == 0f)
                    {
                        snapshot.DetectionConfidence = 1.0f;
                    }
                }
                
                // Fallback Tracking State if metrics is missing
                if (metrics == null)
                {
                    snapshot.TrackingState = supervisor.beaconDetector != null && supervisor.beaconDetector.isDetected ? FSOC.Contracts.TrackingState.Locked : FSOC.Contracts.TrackingState.Searching;
                }
            }

            // Detector Routing logic for Dashboard Status
            snapshot.DetectorType = DetectorType.Unknown;
            if (detectorRouter != null)
            {
                if (detectorRouter.primaryAIDetector != null)
                {
                    var aiStatus = detectorRouter.primaryAIDetector.GetStatus();
                    if (aiStatus == AIStatus.InferenceActive || aiStatus == AIStatus.InferenceAvailable)
                    {
                        snapshot.DetectorType = DetectorType.AI;
                    }
                    else if (detectorRouter.fallbackEnabled)
                    {
                        snapshot.DetectorType = DetectorType.Classical;
                    }
                }
            }
            else
            {
                // If no router, but we have a classical detector, default to Classical
                if (supervisor != null && supervisor.beaconDetector != null)
                    snapshot.DetectorType = DetectorType.Classical;
            }

            // PTZ
            if (panTiltTracker != null)
            {
                snapshot.PanAngle = panTiltTracker.CurrentPanState;
                snapshot.TiltAngle = panTiltTracker.CurrentTiltState;
                // Velocities aren't directly public as raw fields, we approximate from internal if needed or rely on metrics.
                // PanTiltTracker has feedforward velocities, or we can use numerical diff. We'll use feed forward for now if possible, else 0.
                snapshot.PanVelocity = panTiltTracker.feedForwardPanVelocity;
                snapshot.TiltVelocity = panTiltTracker.feedForwardTiltVelocity;

                if (PSConfiguration.Instance != null)
                {
                    snapshot.MaxPanLimit = PSConfiguration.Instance.MaximumAllowedPTZSpeed;
                    snapshot.MinPanLimit = -PSConfiguration.Instance.MaximumAllowedPTZSpeed;
                    snapshot.MaxTiltLimit = PSConfiguration.Instance.MaximumAllowedPTZSpeed;
                    snapshot.MinTiltLimit = -PSConfiguration.Instance.MaximumAllowedPTZSpeed;
                }
                else
                {
                    snapshot.MaxPanLimit = 10f;
                    snapshot.MinPanLimit = -10f;
                    snapshot.MaxTiltLimit = 10f;
                    snapshot.MinTiltLimit = -10f;
                }
            }

            // Scenario configuration & Authoritative Snapshot Population
            SimulationConfigSnapshot cfg = new SimulationConfigSnapshot();
            
            var simRuntime = FindFirstObjectByType<FSC.Core.SimulationRuntimeController>();
            if (simRuntime != null) {
                cfg.runState = simRuntime.CurrentState;
            } else {
                cfg.runState = RunState.RUNNING;
            }

            if (trajectoryController != null)
            {
                snapshot.TrajectoryScenario = trajectoryController.currentMode.ToString();
                
                cfg.trajectoryMode = trajectoryController.currentMode.ToString();
                
                cfg.straightLineStartX = trajectoryController.straightLineStart.x;
                cfg.straightLineStartY = trajectoryController.straightLineStart.y;
                cfg.straightLineStartZ = trajectoryController.straightLineStart.z;
                cfg.straightLineDirX = trajectoryController.straightLineDirection.x;
                cfg.straightLineDirY = trajectoryController.straightLineDirection.y;
                cfg.straightLineDirZ = trajectoryController.straightLineDirection.z;
                cfg.straightLineSpeed = trajectoryController.straightLineSpeed;
                cfg.straightLineRange = trajectoryController.straightLineRange;

                cfg.orbitCenterX = trajectoryController.orbitCenter.x;
                cfg.orbitCenterY = trajectoryController.orbitCenter.y;
                cfg.orbitCenterZ = trajectoryController.orbitCenter.z;
                cfg.orbitRadius = trajectoryController.orbitRadius;
                cfg.orbitAltitude = trajectoryController.altitude;
                cfg.orbitAngularSpeed = trajectoryController.angularSpeed;
                cfg.orbitDirection = (int)trajectoryController.direction;

                cfg.figure8AmplitudeX = trajectoryController.figure8Amplitude.x;
                cfg.figure8AmplitudeY = trajectoryController.figure8Amplitude.y;
                cfg.figure8Period = trajectoryController.figure8Period;
                cfg.figure8MaxSpeed = trajectoryController.figure8MaxSpeed;

                cfg.randomBoundsX = trajectoryController.randomBounds.x;
                cfg.randomBoundsY = trajectoryController.randomBounds.y;
                cfg.randomSpeed = trajectoryController.randomSpeed;
                cfg.randomSeed = trajectoryController.randomSeed;
            }

            if (PSConfiguration.Instance != null)
            {
                snapshot.BeaconSize = (float)PSConfiguration.Instance.DefaultPixelWidth;
                cfg.targetSize = (float)PSConfiguration.Instance.DefaultPixelWidth;
                cfg.targetCount = PSConfiguration.Instance.TargetCount;
                cfg.cameraUpdateRate = PSConfiguration.Instance.CameraUpdateRateHz;
                cfg.ptzUpdateRate = PSConfiguration.Instance.ControlUpdateRateHz;
            }
            
            cfg.targetType = "Beacon Spot";
            cfg.targetShape = "Square"; // Or dynamic if supported later
            if (trajectoryController != null) {
                cfg.initialPositionX = trajectoryController.orbitCenter.x;
                cfg.initialPositionY = trajectoryController.orbitCenter.y + trajectoryController.altitude;
                cfg.initialPositionZ = trajectoryController.orbitCenter.z;
            }

            // Environment
            var atmosphere = FindFirstObjectByType<AtmosphereProcessor>();
            if (atmosphere != null)
            {
                if (atmosphere.enableAtmosphere && atmosphere.atmosphereMode != AtmosphereMode.Clear)
                {
                    snapshot.Atmosphere = atmosphere.atmosphereMode.ToString();
                }
                else
                {
                    snapshot.Atmosphere = "Clear";
                }
                
                cfg.atmosphereEnabled = atmosphere.enableAtmosphere;
                cfg.atmosphereMode = atmosphere.atmosphereMode.ToString();
                cfg.atmosphereIntensity = atmosphere.intensity;
                cfg.atmosphereContrast = atmosphere.contrast;
                cfg.atmosphereBrightness = atmosphere.brightness;
                cfg.hazeAmount = atmosphere.hazeAmount;
                cfg.fogAmount = atmosphere.fogAmount;
                cfg.rainAmount = atmosphere.rainAmount;
                cfg.atmosphereSeed = atmosphere.randomSeed;
            }

            var disturbance = FindFirstObjectByType<DisturbanceProcessor>();
            if (disturbance != null)
            {
                if (disturbance.enableDisturbances && disturbance.disturbanceType != DisturbanceType.None)
                {
                    snapshot.NoiseType = disturbance.disturbanceType.ToString();
                    
                    if (disturbance.disturbanceType == DisturbanceType.Gaussian)
                        snapshot.NoiseStrength = disturbance.gaussianStandardDeviation;
                    else if (disturbance.disturbanceType == DisturbanceType.SaltAndPepper)
                        snapshot.NoiseStrength = disturbance.saltAndPepperIntensity;
                    else if (disturbance.disturbanceType == DisturbanceType.Poisson)
                        snapshot.NoiseStrength = disturbance.poissonStrength;
                }
                else
                {
                    snapshot.NoiseType = "None";
                    snapshot.NoiseStrength = 0f;
                }
                
                cfg.disturbanceEnabled = disturbance.enableDisturbances;
                cfg.noiseType = disturbance.disturbanceType.ToString();
                if (disturbance.disturbanceType == DisturbanceType.Gaussian) cfg.noiseStrength = disturbance.gaussianStandardDeviation;
                else if (disturbance.disturbanceType == DisturbanceType.SaltAndPepper) cfg.noiseStrength = disturbance.saltAndPepperIntensity;
                else if (disturbance.disturbanceType == DisturbanceType.Poisson) cfg.noiseStrength = disturbance.poissonStrength;
                else cfg.noiseStrength = 0f;
            }

            var platformMotion = FindFirstObjectByType<FSC.Core.PlatformMotionController>();
            if (platformMotion != null)
            {
                if (platformMotion.enabled && platformMotion.enablePlatformMotion)
                {
                    snapshot.PlatformMotion = platformMotion.motionMode.ToString();
                }
                else
                {
                    snapshot.PlatformMotion = "Static";
                }
                cfg.platformMotionMode = snapshot.PlatformMotion;
                cfg.platformMotionMagnitude = platformMotion.CurrentPlatformMotionMagnitude;
            }

            var jitter = FindFirstObjectByType<FSC.Core.CameraJitterController>();
            if (jitter != null)
            {
                if (jitter.enabled && jitter.enableCameraJitter)
                {
                    snapshot.CameraJitter = "Active";
                }
                else
                {
                    snapshot.CameraJitter = "None";
                }
                cfg.cameraJitterEnabled = (jitter.enabled && jitter.enableCameraJitter);
                cfg.cameraJitterMagnitude = jitter.CurrentJitterMagnitude;
            }

            if (virtualCamera != null)
            {
                snapshot.VFOV = virtualCamera.fieldOfView;
                snapshot.HFOV = Camera.VerticalToHorizontalFieldOfView(virtualCamera.fieldOfView, virtualCamera.aspect);
                if (virtualCamera.targetTexture != null)
                {
                    snapshot.Resolution = $"{virtualCamera.targetTexture.width}x{virtualCamera.targetTexture.height}";
                }
                cfg.cameraResolution = snapshot.Resolution;
                cfg.hfov = snapshot.HFOV;
                cfg.vfov = snapshot.VFOV;
            }
            
            if (panTiltTracker != null)
            {
                cfg.maxPanSpeed = panTiltTracker.panSpeed;
                cfg.maxTiltSpeed = panTiltTracker.tiltSpeed;
            }
            
            snapshot.ConfigSnapshot = cfg;

            if (eventLog != null)
            {
                var eventsList = eventLog.GetEvents();
                if (eventsList != null)
                {
                    // Copy to array for serialization
                    string[] evts = new string[eventsList.Count];
                    for (int i = 0; i < eventsList.Count; i++)
                    {
                        evts[i] = eventsList[i];
                    }
                    snapshot.RecentEvents = evts;
                }
            }

            // Send to UI
            telemetryBus.Publish(snapshot);
        }

        private FSOC.Contracts.TrackingState MapState(FSC.Core.TrackingState internalState)
        {
            switch (internalState)
            {
                case FSC.Core.TrackingState.SEARCHING: return FSOC.Contracts.TrackingState.Searching;
                case FSC.Core.TrackingState.ACQUIRING: return FSOC.Contracts.TrackingState.Acquiring;
                case FSC.Core.TrackingState.LOCKED: return FSOC.Contracts.TrackingState.Locked;
                case FSC.Core.TrackingState.PREDICTING: return FSOC.Contracts.TrackingState.Predicting;
                case FSC.Core.TrackingState.LOST: return FSOC.Contracts.TrackingState.Lost;
                case FSC.Core.TrackingState.REACQUIRING: return FSOC.Contracts.TrackingState.Reacquiring;
                default: return FSOC.Contracts.TrackingState.Lost;
            }
        }
    }
}
