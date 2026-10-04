using UnityEngine;

namespace FSC.Core
{
    public enum TrackingState
    {
        SEARCHING,
        ACQUIRING,
        LOCKED,
        PREDICTING,
        LOST,
        REACQUIRING
    }

    /// <summary>
    /// Evaluates high-level tracking states and spatial errors strictly from the provided image-space detector.
    /// Complies with PS performance parameters and logs state transitions automatically.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class TrackingMetrics : MonoBehaviour
    {
        [Header("Detector Input")]
        [Tooltip("The image-space beacon detector supplying the raw centroid.")]
        public BeaconDetector beaconDetector;

        [Header("Live Tracking State")]
        [SerializeField] private TrackingState _currentState = TrackingState.SEARCHING;
        public TrackingState CurrentState 
        { 
            get => _currentState; 
            set => _currentState = value; 
        }

        [Tooltip("If true, the state machine is driven externally by a Supervisor.")]
        public bool managedExternally = false;

        [SerializeField] private bool _isTracking = false;
        public bool IsTracking => _isTracking;

        [Header("Spatial Error Metrics (Pixels)")]
        [SerializeField] private float _errorX = 0f;
        public float ErrorX => _errorX;

        [SerializeField] private float _errorY = 0f;
        public float ErrorY => _errorY;

        [SerializeField] private float _radialError = 0f;
        public float RadialError => _radialError;

        [Header("Performance History")]
        [Tooltip("Time in seconds to establish the very first stable lock.")]
        [SerializeField] private float _acquisitionTimeSec = -1f;
        public float AcquisitionTimeSec => _acquisitionTimeSec;

        [Tooltip("Time in seconds to recover the lock during the most recent signal loss.")]
        [SerializeField] private float _reacquisitionTimeSec = -1f;
        public float ReacquisitionTimeSec => _reacquisitionTimeSec;

        [SerializeField] private int _detectedFrames = 0;
        public int DetectedFrames => _detectedFrames;

        [SerializeField] private int _lostFrames = 0;
        public int LostFrames => _lostFrames;

        [Tooltip("Percentage of frames the target was actively detected since startup.")]
        public float LockRetentionPercentage => (_detectedFrames + _lostFrames) > 0 ? ((float)_detectedFrames / (_detectedFrames + _lostFrames)) * 100f : -1f;

        [SerializeField] private int _reacquisitionCount = 0;
        public int ReacquisitionCount => _reacquisitionCount;

        [SerializeField] private float _worstReacquisitionTimeSec = -1f;
        public float WorstReacquisitionTimeSec => _worstReacquisitionTimeSec;

        [SerializeField] private int _framesWithin10px = 0;
        public float PercentFramesWithin10px => (_detectedFrames + _lostFrames) > 0 ? ((float)_framesWithin10px / (_detectedFrames + _lostFrames)) * 100f : -1f;

        // --- PSAcceptance Metrics ---
        private float _errorSum = 0f;
        private float _errorSqSum = 0f;
        private float _maxErrorObserved = 0f;
        private int _errorSamples = 0;

        public float MeanError => _errorSamples > 0 ? _errorSum / _errorSamples : -1f;
        public float MaxError => _errorSamples > 0 ? _maxErrorObserved : -1f;
        public float RMSE => _errorSamples > 0 ? Mathf.Sqrt(_errorSqSum / _errorSamples) : -1f;
        public float LossRate => (_detectedFrames + _lostFrames) > 0 ? (float)_lostFrames / (_detectedFrames + _lostFrames) : -1f;
        private float _cachedProcessingFPS = 0f;
        private float _cachedPTZUpdateRate = 0f;

        public float ProcessingFPS => _cachedProcessingFPS;
        public float PTZUpdateRate => _cachedPTZUpdateRate;

        // Constants based on the strict PS 640x480 requirement
        private const float SENSOR_CENTER_X = 320f;
        private const float SENSOR_CENTER_Y = 240f;

        // Internal timing and state tracking
        private float _totalRunTime = 0f;
        private float _timeSinceLastLoss = 0f;
        private bool _hasEverLocked = false;

        private void Awake()
        {
            if (beaconDetector == null) beaconDetector = FindFirstObjectByType<BeaconDetector>();
            ResetAll();
        }

        public void ResetAll()
        {
            _acquisitionTimeSec = -1f;
            _reacquisitionTimeSec = -1f;
            _detectedFrames = 0;
            _lostFrames = 0;
            _reacquisitionCount = 0;
            _worstReacquisitionTimeSec = -1f;
            _framesWithin10px = 0;
            _errorSum = 0f;
            _errorSqSum = 0f;
            _maxErrorObserved = 0f;
            _errorSamples = 0;
            _totalRunTime = 0f;
            _timeSinceLastLoss = 0f;
            _hasEverLocked = false;
            _currentState = TrackingState.SEARCHING;
            _isTracking = false;
            _errorX = 0f;
            _errorY = 0f;
            _radialError = 0f;
            _cachedProcessingFPS = 0f;
            _cachedPTZUpdateRate = 0f;
        }

        private void LateUpdate()
        {
            if (beaconDetector == null) return;

            // Gate by SimulationRuntimeController
            if (FSOC.Contracts.RunState.RUNNING.ToString() != "RUNNING") {} // Just a dummy to ensure namespace
            var runtime = FindFirstObjectByType<SimulationRuntimeController>();
            if (runtime != null && runtime.CurrentState != FSOC.Contracts.RunState.RUNNING)
            {
                return;
            }

            _cachedProcessingFPS = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : -1f;
            _cachedPTZUpdateRate = Time.deltaTime > 0f ? 1f / Time.deltaTime : -1f;

            _totalRunTime += Time.deltaTime;

            // 1. Fetch Authoritative Tracking Limits
            float maxErrorPixels = 10f;
            if (PSConfiguration.Instance != null)
            {
                maxErrorPixels = PSConfiguration.Instance.MaxTrackingErrorPixels;
            }

            // 2. Calculate Spatial Errors
            if (beaconDetector.isDetected)
            {
                _errorX = beaconDetector.centroid.x - SENSOR_CENTER_X;
                _errorY = beaconDetector.centroid.y - SENSOR_CENTER_Y;
                _radialError = Mathf.Sqrt((_errorX * _errorX) + (_errorY * _errorY));
                
                _detectedFrames++;
                
                _errorSum += _radialError;
                _errorSqSum += (_radialError * _radialError);
                if (_radialError > _maxErrorObserved) _maxErrorObserved = _radialError;
                if (_radialError <= maxErrorPixels) _framesWithin10px++;
                _errorSamples++;
            }
            else
            {
                // Preserve the last known error when tracking is lost, or reset.
                // Keeping it at the last known error or 0 is a design choice.
                // For clear telemetry, we'll maintain the error fields as 0 when no target exists.
                _errorX = 0f;
                _errorY = 0f;
                _radialError = 0f;
                
                _lostFrames++;
            }

            // 3. Evaluate Next State (Only if not managed externally)
            TrackingState previousState = _currentState;
            TrackingState nextState = _currentState;

            if (!managedExternally)
            {
                if (beaconDetector.isDetected)
                {
                    if (_radialError <= maxErrorPixels)
                    {
                        nextState = TrackingState.LOCKED;
                    }
                    else
                    {
                        if (previousState == TrackingState.SEARCHING)
                            nextState = TrackingState.ACQUIRING;
                        else if (previousState == TrackingState.LOST)
                            nextState = TrackingState.REACQUIRING;
                        else if (previousState == TrackingState.LOCKED)
                            nextState = TrackingState.ACQUIRING;
                    }
                }
                else
                {
                    if (_hasEverLocked)
                        nextState = TrackingState.LOST;
                    else
                        nextState = TrackingState.SEARCHING;
                }
            }

            // 4. Handle State Transitions
            if (nextState != previousState)
            {
                // Capture timers based on the transition entering a new state
                if (nextState == TrackingState.LOCKED)
                {
                    if (!_hasEverLocked)
                    {
                        _acquisitionTimeSec = _totalRunTime;
                    }
                    else
                    {
                        _reacquisitionTimeSec = _timeSinceLastLoss;
                        _reacquisitionCount++;
                        if (_reacquisitionTimeSec > _worstReacquisitionTimeSec)
                        {
                            _worstReacquisitionTimeSec = _reacquisitionTimeSec;
                        }
                    }
                    
                    _hasEverLocked = true;
                }

                if (nextState == TrackingState.LOST)
                {
                    // Reset the loss timer the exact moment we fall out of lock entirely
                    _timeSinceLastLoss = 0f;
                }

                _currentState = nextState;
                _isTracking = (_currentState == TrackingState.LOCKED);

                Debug.Log($"[TrackingMetrics] State Transitioned: {previousState} -> {_currentState} | Radial Error: {_radialError:F1}px");
            }

            // Accumulate loss timer if currently lost or trying to reacquire
            if (_currentState == TrackingState.LOST || _currentState == TrackingState.REACQUIRING || _currentState == TrackingState.PREDICTING)
            {
                _timeSinceLastLoss += Time.deltaTime;
            }

        }
    }
}
