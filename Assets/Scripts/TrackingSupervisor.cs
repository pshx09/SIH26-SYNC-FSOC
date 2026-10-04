using UnityEngine;
using FSC.Core;

[DefaultExecutionOrder(45)]
public class TrackingSupervisor : MonoBehaviour
{
    [Header("Dependencies")]
    public BeaconDetector beaconDetector;
    public TrackingMetrics trackingMetrics;
    public PanTiltTracker panTiltTracker;
    public Camera virtualCamera;

    [Header("Configuration")]
    public float predictionTimeout = 0.1f;
    public float velocityTimeConstant = 0.1f;
    
    [Header("Reacquisition Search")]
    public float searchPanAmplitude = 5f;
    public float searchTiltAmplitude = 3f;
    public float searchSpeed = 2f; // cycles per second for search

    [Header("Diagnostics (Read Only)")]
    [SerializeField] private Vector2 _pixelVelocity = Vector2.zero;
    [SerializeField] private float _timeInCurrentState = 0f;
    [SerializeField] private float _lastPanVelocity = 0f;
    [SerializeField] private float _lastTiltVelocity = 0f;

    private Vector2 _lastCentroid;
    private bool _hasLastCentroid;
    
    private float _reacquireBasePan;
    private float _reacquireBaseTilt;
    private float _reacquireTime;

    private int _consecutiveValidFrames = 0;
    private int _consecutiveMissedFrames = 0;

    private void Start()
    {
        if (beaconDetector == null) beaconDetector = FindFirstObjectByType<BeaconDetector>();
        if (trackingMetrics == null) trackingMetrics = FindFirstObjectByType<TrackingMetrics>();
        if (panTiltTracker == null) panTiltTracker = FindFirstObjectByType<PanTiltTracker>();
        
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

        if (trackingMetrics != null)
        {
            trackingMetrics.managedExternally = true;
        }
    }

    public void ResetState()
    {
        trackingMetrics.CurrentState = TrackingState.ACQUIRING;
        _timeInCurrentState = 0f;
        _pixelVelocity = Vector2.zero;
        _lastPanVelocity = 0f;
        _lastTiltVelocity = 0f;
        _hasLastCentroid = false;
        _consecutiveValidFrames = 0;
        _consecutiveMissedFrames = 0;
    }

    private void LateUpdate()
    {
        if (beaconDetector == null || trackingMetrics == null || panTiltTracker == null) return;

        UpdateVelocityEstimate();
        UpdateStateMachine();
        ApplyControlLogic();
        
        LogDiagnostics();
    }

    private void UpdateVelocityEstimate()
    {
        if (beaconDetector.isDetected)
        {
            if (_hasLastCentroid && Time.deltaTime > 0)
            {
                Vector2 rawVelocity = (beaconDetector.centroid - _lastCentroid) / Time.deltaTime;
                float alpha = velocityTimeConstant > 0f ? (1f - Mathf.Exp(-Time.deltaTime / velocityTimeConstant)) : 1f;
                _pixelVelocity = Vector2.Lerp(_pixelVelocity, rawVelocity, alpha);
            }
            _lastCentroid = beaconDetector.centroid;
            _hasLastCentroid = true;
        }
        else
        {
            _hasLastCentroid = false;
        }
    }

    private void UpdateStateMachine()
    {
        TrackingState prevState = trackingMetrics.CurrentState;
        TrackingState nextState = prevState;
        _timeInCurrentState += Time.deltaTime;

        if (beaconDetector.isDetected)
        {
            _consecutiveValidFrames++;
            _consecutiveMissedFrames = 0;
        }
        else
        {
            _consecutiveMissedFrames++;
            _consecutiveValidFrames = 0;
        }

        if (beaconDetector.isDetected)
        {
            float maxError = PSConfiguration.Instance != null ? PSConfiguration.Instance.MaxTrackingErrorPixels : 10f;
            
            if (trackingMetrics.RadialError <= maxError)
            {
                if (prevState != TrackingState.LOCKED)
                {
                    if (_consecutiveValidFrames >= 5)
                        nextState = TrackingState.LOCKED;
                    else
                        nextState = TrackingState.ACQUIRING;
                }
                else
                {
                    nextState = TrackingState.LOCKED; // Stay locked
                }
            }
            else
            {
                nextState = TrackingState.ACQUIRING;
            }
        }
        else
        {
            if (prevState == TrackingState.LOCKED || prevState == TrackingState.ACQUIRING)
            {
                if (_consecutiveMissedFrames >= 3)
                    nextState = TrackingState.PREDICTING;
            }
            else if (prevState == TrackingState.PREDICTING)
            {
                if (_timeInCurrentState > predictionTimeout)
                {
                    nextState = TrackingState.REACQUIRING;
                }
            }
            else if (prevState == TrackingState.REACQUIRING)
            {
                if (_timeInCurrentState > 10f)
                {
                    nextState = TrackingState.SEARCHING;
                }
            }
            else
            {
                nextState = TrackingState.SEARCHING;
            }
        }

        if (nextState != prevState)
        {
            if (nextState == TrackingState.REACQUIRING && prevState == TrackingState.PREDICTING)
            {
                _reacquireBasePan = panTiltTracker.CurrentPanState;
                _reacquireBaseTilt = panTiltTracker.CurrentTiltState;
                _reacquireTime = 0f;
            }
            trackingMetrics.CurrentState = nextState;
            _timeInCurrentState = 0f;
        }
    }

    private void ApplyControlLogic()
    {
        float hFov = 4f, vFov = 3f;
        if (virtualCamera != null)
        {
            vFov = virtualCamera.fieldOfView;
            hFov = Camera.VerticalToHorizontalFieldOfView(vFov, virtualCamera.aspect);
        }

        float degPerPixelX = hFov / 640f;
        float degPerPixelY = vFov / 480f;

        TrackingState state = trackingMetrics.CurrentState;

        if (state == TrackingState.LOCKED || state == TrackingState.ACQUIRING)
        {
            panTiltTracker.overrideControllerState = false;
            
            // Check FOV Guard band (Outer 25%)
            float extentsX = 640f * 0.25f; // 160px from edges
            float extentsY = 480f * 0.25f; // 120px from edges
            
            bool inGuardBandX = beaconDetector.centroid.x < extentsX || beaconDetector.centroid.x > (640f - extentsX);
            bool inGuardBandY = beaconDetector.centroid.y < extentsY || beaconDetector.centroid.y > (480f - extentsY);

            // Calculate Target Angular Velocity
            float targetVelX = _pixelVelocity.x * degPerPixelX;
            float targetVelY = _pixelVelocity.y * degPerPixelY;

            // Apply FeedForward if in guard band or moving fast
            if (inGuardBandX)
                panTiltTracker.feedForwardPanVelocity = targetVelX * panTiltTracker.panDirection;
            else
                panTiltTracker.feedForwardPanVelocity = 0f;

            if (inGuardBandY)
                panTiltTracker.feedForwardTiltVelocity = targetVelY * panTiltTracker.tiltDirection;
            else
                panTiltTracker.feedForwardTiltVelocity = 0f;

            _lastPanVelocity = targetVelX * panTiltTracker.panDirection;
            _lastTiltVelocity = targetVelY * panTiltTracker.tiltDirection;
        }
        else if (state == TrackingState.PREDICTING)
        {
            panTiltTracker.overrideControllerState = false;
            // Maintain last known velocity
            panTiltTracker.feedForwardPanVelocity = _lastPanVelocity;
            panTiltTracker.feedForwardTiltVelocity = _lastTiltVelocity;
        }
        else if (state == TrackingState.REACQUIRING)
        {
            panTiltTracker.overrideControllerState = true;
            panTiltTracker.feedForwardPanVelocity = 0f;
            panTiltTracker.feedForwardTiltVelocity = 0f;

            _reacquireTime += Time.deltaTime;
            
            // Expanding Lissajous search pattern
            float expandFactor = Mathf.Clamp01(_reacquireTime / 5f); // Expand over 5 seconds
            
            float panOffset = Mathf.Sin(_reacquireTime * searchSpeed * Mathf.PI * 2f) * searchPanAmplitude * expandFactor;
            float tiltOffset = Mathf.Cos(_reacquireTime * searchSpeed * 0.73f * Mathf.PI * 2f) * searchTiltAmplitude * expandFactor;

            panTiltTracker.overridePanAngle = _reacquireBasePan + panOffset;
            panTiltTracker.overrideTiltAngle = _reacquireBaseTilt + tiltOffset;
        }
        else if (state == TrackingState.SEARCHING)
        {
            panTiltTracker.overrideControllerState = true;
            panTiltTracker.feedForwardPanVelocity = 0f;
            panTiltTracker.feedForwardTiltVelocity = 0f;
            
            // Simple slow sweep or return to home
            panTiltTracker.overridePanAngle = 0f;
            panTiltTracker.overrideTiltAngle = 0f;
        }
    }

    private float _nextLogTime = 0f;
    private void LogDiagnostics()
    {
        if (Time.time > _nextLogTime)
        {
            _nextLogTime = Time.time + 1.0f; // 1 Hz for cleaner logs

            Debug.Log($"[PTZState]\nPan: {panTiltTracker.CurrentPanState:F2}\nTilt: {panTiltTracker.CurrentTiltState:F2}\nPanRate: {panTiltTracker.feedForwardPanVelocity:F2}\nTiltRate: {panTiltTracker.feedForwardTiltVelocity:F2}");

            string centroidStr = beaconDetector.isDetected ? $"{beaconDetector.centroid.x:F1}, {beaconDetector.centroid.y:F1}" : "NONE";
            Debug.Log($"[Tracking]\nState: {trackingMetrics.CurrentState}\nCentroid: {centroidStr}\nErrorX: {trackingMetrics.ErrorX:F1}\nErrorY: {trackingMetrics.ErrorY:F1}\nRadialError: {trackingMetrics.RadialError:F1}");

            Debug.Log($"[TargetMotion]\nPixelVelocityX: {_pixelVelocity.x:F1}\nPixelVelocityY: {_pixelVelocity.y:F1}\nAngularVelocityPan: {_lastPanVelocity:F2}\nAngularVelocityTilt: {_lastTiltVelocity:F2}");

            if (trackingMetrics.CurrentState == TrackingState.REACQUIRING)
            {
                Debug.Log($"[Reacquisition]\nState: REACQUIRING\nLastKnownDirection: {_reacquireBasePan:F1}, {_reacquireBaseTilt:F1}\nSearchPhase: {Mathf.Clamp01(_reacquireTime / 5f) * 100f:F0}%\nElapsed: {_reacquireTime:F1}\nDetected: False");
            }
            else if (trackingMetrics.CurrentState == TrackingState.PREDICTING)
            {
                Debug.Log($"[Reacquisition]\nState: PREDICTING\nLastKnownDirection: {_reacquireBasePan:F1}, {_reacquireBaseTilt:F1}\nSearchPhase: COASTING\nElapsed: {_timeInCurrentState:F1}\nDetected: False");
            }

            UAVTrajectoryController uav = FindAnyObjectByType<UAVTrajectoryController>();
            string currentMode = uav != null ? uav.currentMode.ToString() : "Unknown";
            
            bool isPass = trackingMetrics.AcquisitionTimeSec <= 2f && 
                          trackingMetrics.MeanError <= 10f && 
                          (trackingMetrics.LossRate * 100f) < 5f && 
                          trackingMetrics.WorstReacquisitionTimeSec <= 1f && 
                          trackingMetrics.ProcessingFPS >= 20f;

            Debug.Log($"[PSAcceptance]\n" +
                      $"Mode: {currentMode}\n" +
                      $"ClosedLoopMeasurementSource: IMAGE_DETECTOR\n" +
                      $"AcquisitionTime: {trackingMetrics.AcquisitionTimeSec:F2}\n" +
                      $"MeanError: {trackingMetrics.MeanError:F2}\n" +
                      $"RMSE: {trackingMetrics.RMSE:F2}\n" +
                      $"MaxError: {trackingMetrics.MaxError:F2}\n" +
                      $"FramesWithin10px: {trackingMetrics.PercentFramesWithin10px:F1}%\n" +
                      $"LossRate: {trackingMetrics.LossRate * 100f:F1}%\n" +
                      $"ReacquisitionTime: {trackingMetrics.WorstReacquisitionTimeSec:F2}\n" +
                      $"LockRetention: {trackingMetrics.LockRetentionPercentage:F1}%\n" +
                      $"FPS: {trackingMetrics.ProcessingFPS:F1}\n" +
                      $"PTZUpdateRate: {trackingMetrics.PTZUpdateRate:F1}\n" +
                      $"ReacquisitionCount: {trackingMetrics.ReacquisitionCount}\n" +
                      $"PASS/FAIL: {(isPass ? "PASS" : "FAIL")}");
        }
    }
}
