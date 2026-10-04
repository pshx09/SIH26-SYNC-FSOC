using UnityEngine;
using FSC.Core; // Image-space evaluation logic

/// <summary>
/// A closed-loop image-based tracking controller.
/// Converts 2D pixel errors from TrackingMetrics into smooth physical pan (yaw) and tilt (pitch) velocities.
/// Attach this script to the PanBase GameObject.
/// </summary>
[DefaultExecutionOrder(50)]
public class PanTiltTracker : MonoBehaviour
{
    [Header("Input Dependencies")]
    [Tooltip("The image-space metrics evaluator providing 2D pixel error signals.")]
    public TrackingMetrics trackingMetrics;

    [Tooltip("The Transform that controls the vertical tilt (pitch). Must be a child or descendant of this GameObject.")]
    public Transform tiltBase;

    [Header("Tracking Settings")]
    [Tooltip("Toggle physical tracking hardware on or off.")]
    public bool trackingEnabled = true;

    [Tooltip("If true, runs a 1-time stationary target calibration test to measure plant Jacobian signs.")]
    public bool runDiagnosticPlantTest = false;

    [Tooltip("Dwell time in seconds required in LOCKED state before disabling freeze.")]
    public float stationaryLockDwellTime = 3.0f;

    [Tooltip("Maximum allowed pan speed (degrees per second). Constrained by PS limits.")]
    public float panSpeed = 5f;

    [Tooltip("Maximum allowed tilt speed (degrees per second). Constrained by PS limits.")]
    public float tiltSpeed = 5f;

    [Header("Angular PID Control (Pan)")]
    [Tooltip("Proportional gain (P) in (deg/s) per degree of error.")]
    public float panKp = 15f;
    [Tooltip("Integral gain (I) in (deg/s^2) per degree of error.")]
    public float panKi = 10f;
    [Tooltip("Derivative/Damping gain (D).")]
    public float panKd = 0f;

    [Header("Angular PID Control (Tilt)")]
    [Tooltip("Proportional gain (P) in (deg/s) per degree of error.")]
    public float tiltKp = 15f;
    [Tooltip("Integral gain (I) in (deg/s^2) per degree of error.")]
    public float tiltKi = 10f;
    [Tooltip("Derivative/Damping gain (D).")]
    public float tiltKd = 0f;

    [Header("PID Settings")]
    [Tooltip("Deadband in pixels to prevent chatter near center.")]
    public float deadband = 0.5f;

    [Tooltip("Maximum allowed accumulated integral velocity (deg/s) to prevent windup.")]
    public float maxIntegralVelocity = 2.5f;

    [Tooltip("Time constant for error velocity smoothing. 0 = instant.")]
    public float errorVelocityTimeConstant = 0.05f;

    [Header("Output Smoothing & Limits")]
    [Tooltip("Time constant in seconds for velocity command smoothing. 0 = instant response.")]
    public float commandTimeConstant = 0.02f;

    [Tooltip("Maximum angular acceleration (deg/s^2).")]
    public float maxAngularAcceleration = 60f;

    [Header("Direction Conventions")]
    [Tooltip("Sign multiplier for Pan direction (+1 or -1). Flips left/right control.")]
    [Range(-1f, 1f)]
    public float panDirection = 1f;

    [Tooltip("Sign multiplier for Tilt direction (+1 or -1). Flips up/down control.")]
    [Range(-1f, 1f)]
    public float tiltDirection = -1f;

    [Header("Live Debug (Read Only)")]
    [SerializeField] private float _currentErrorX = 0f;
    [SerializeField] private float _currentErrorY = 0f;
    [SerializeField] private float _integralX = 0f;
    [SerializeField] private float _integralY = 0f;
    [SerializeField] private float _filteredErrorVelX = 0f;
    [SerializeField] private float _filteredErrorVelY = 0f;
    [SerializeField] private float _pContributionX = 0f;
    [SerializeField] private float _iContributionX = 0f;
    [SerializeField] private float _dContributionX = 0f;
    [SerializeField] private float _pContributionY = 0f;
    [SerializeField] private float _iContributionY = 0f;
    [SerializeField] private float _dContributionY = 0f;
    [SerializeField] private float _rawCommandPan = 0f;
    [SerializeField] private float _rawCommandTilt = 0f;
    [SerializeField] private float _commandedPanVelocity = 0f;
    [SerializeField] private float _commandedTiltVelocity = 0f;
    [SerializeField] private float _appliedCommandDeltaPan = 0f;
    [SerializeField] private float _appliedCommandDeltaTilt = 0f;

    public enum RotationAxis { X, Y, Z }

    [Header("Axis Configuration")]
    [Tooltip("Which local axis on PanBase controls Yaw? Default is Y.")]
    public RotationAxis panAxis = RotationAxis.Y;
    
    [Tooltip("Which local axis on TiltBase controls Pitch? Default is X. If your 3D model tilts around Z, change this.")]
    public RotationAxis tiltAxis = RotationAxis.X;

    [Header("Pan Limits (Yaw)")]
    [Tooltip("If true, pan angle is not clamped or wrapped, allowing continuous 360 degree tracking.")]
    public bool allowContinuousPan = true;
    [Range(-180f, 180f)]
    public float panMin = -180f;
    [Range(-180f, 180f)]
    public float panMax = 180f;

    [Header("Tilt Limits (Pitch)")]
    [Range(-180f, 90f)]
    public float tiltMin = -90f; // Expanded to allow Zenith tracking
    [Range(-90f, 90f)]
    public float tiltMax = 60f;

    [Header("Tilt Diagnosis (Read Only)")]
    [SerializeField] private float _tiltBaseLocalPitchAxis = 0f;
    [SerializeField] private float _virtualCameraLocalX = 0f;
    [SerializeField] private float _appliedTiltDelta = 0f;
    [SerializeField] private bool _wasOverwrittenByOtherScript = false;
    
    private float _lastSetTilt = float.NaN;
    private Camera _cachedVirtualCamera;
    
    private float _previousErrorX = 0f;
    private float _previousErrorY = 0f;
    private bool _hasPreviousError = false;

    private float _currentPanState = 0f;
    private float _currentTiltState = 0f;

    private float _nextDebugLogTime = 0f;
    private bool _diagnosticMeasurementsComplete = false;
    private bool _diagnosticFullyComplete = false;

    // Supervisor Control Interface
    [HideInInspector] public float feedForwardPanVelocity = 0f;
    [HideInInspector] public float feedForwardTiltVelocity = 0f;
    [HideInInspector] public bool overrideControllerState = false;
    [HideInInspector] public float overridePanAngle = 0f;
    [HideInInspector] public float overrideTiltAngle = 0f;
    
    [Header("Benchmark Mode")]
    [Tooltip("If true, the tracker calculates commands but does not apply them to the physical Transforms.")]
    public bool bypassActuator = false;

    public float CurrentPanState => _currentPanState;
    public float CurrentTiltState => _currentTiltState;

    public void ForceState(float pan, float tilt)
    {
        _currentPanState = pan;
        _currentTiltState = tilt;
        _lastSetTilt = tilt;
        
        // Wipe all PID and momentum state to guarantee a clean snap
        _commandedPanVelocity = 0f;
        _commandedTiltVelocity = 0f;
        _rawCommandPan = 0f;
        _rawCommandTilt = 0f;
        _appliedCommandDeltaPan = 0f;
        _appliedCommandDeltaTilt = 0f;
        _integralX = 0f;
        _integralY = 0f;
        _filteredErrorVelX = 0f;
        _filteredErrorVelY = 0f;

        transform.localEulerAngles = SetAxisValue(transform.localEulerAngles, panAxis, _currentPanState);
        if (tiltBase != null)
            tiltBase.localEulerAngles = SetAxisValue(tiltBase.localEulerAngles, tiltAxis, _currentTiltState);
    }

    private void Start()
    {
        // Add supervisor automatically if not present
        TrackingSupervisor supervisor = GetComponent<TrackingSupervisor>();
        if (supervisor == null)
        {
            supervisor = gameObject.AddComponent<TrackingSupervisor>();
            supervisor.panTiltTracker = this;
            supervisor.trackingMetrics = this.trackingMetrics;
            supervisor.virtualCamera = this.tiltBase != null ? this.tiltBase.GetComponentInChildren<Camera>() : null;
            supervisor.beaconDetector = FindFirstObjectByType<BeaconDetector>();
        }

        // Basic validation for missing references
        if (tiltBase == null)
        {
            Debug.LogError("[PanTiltTracker] Tilt Base is missing. Please assign it in the Inspector.", this);
        }
        if (trackingMetrics == null)
        {
            Debug.LogError("[PanTiltTracker] Tracking Metrics is missing. Please assign it in the Inspector.", this);
        }

        // Try to automatically find the virtual camera down the hierarchy for diagnostic reporting
        _cachedVirtualCamera = tiltBase != null ? tiltBase.GetComponentInChildren<Camera>() : null;

        // Ensure tilt limits allow for Zenith pointing, overriding Inspector if necessary
        if (tiltMin > -90f)
        {
            Debug.LogWarning($"[PanTiltTracker] tiltMin was {tiltMin} in Inspector. Overriding to -90f to allow Zenith tracking.");
            tiltMin = -90f;
        }

        // Initialize controller's internal state FROM the actual scene rotation
        float initialPanValue = GetAxisValue(transform.localEulerAngles, panAxis);
        _currentPanState = NormalizeAngle(initialPanValue);
        Debug.Log("[PanTiltTracker] Initial scene pan: " + _currentPanState);

        if (tiltBase != null)
        {
            float initialTiltValue = GetAxisValue(tiltBase.localEulerAngles, tiltAxis);
            _currentTiltState = NormalizeAngle(initialTiltValue);
            _lastSetTilt = _currentTiltState;
            Debug.Log("[PanTiltTracker] Initial scene tilt: " + _currentTiltState);
        }
    }

    private void LateUpdate()
    {
        if (!trackingEnabled || trackingMetrics == null || tiltBase == null)
            return;

        if (runDiagnosticPlantTest && !_diagnosticMeasurementsComplete)
        {
            if (RunDiagnosticPlantTest())
            {
                _diagnosticMeasurementsComplete = true; 
            }
            return;
        }

        // 1. Explicitly Authorize Movement ONLY during active tracking states.
        // We MUST allow movement during ACQUIRING because the camera needs to physically 
        // move to bring the error down to the LOCKED threshold (< 10px).
        bool isActivelyTracking = trackingMetrics.CurrentState == TrackingState.ACQUIRING ||
                                  trackingMetrics.CurrentState == TrackingState.LOCKED ||
                                  trackingMetrics.CurrentState == TrackingState.REACQUIRING;

        if (!isActivelyTracking)
        {
            // Halt any residual motion safely when detection is completely lost
            _commandedPanVelocity = 0f;
            _commandedTiltVelocity = 0f;
            _rawCommandPan = 0f;
            _rawCommandTilt = 0f;
            _appliedCommandDeltaPan = 0f;
            _appliedCommandDeltaTilt = 0f;
            _hasPreviousError = false;
            _filteredErrorVelX = 0f;
            _filteredErrorVelY = 0f;
            _integralX = 0f;
            return;
        }

        // If supervisor is driving an explicit search pattern, handle it directly
        if (overrideControllerState)
        {
            // We still need to respect PS limits on the step size
            float overrideAbsoluteMaxPan = Mathf.Min(panSpeed, PSConfiguration.Instance != null ? PSConfiguration.Instance.MaximumAllowedPTZSpeed : 10f);
            float overrideAbsoluteMaxTilt = Mathf.Min(tiltSpeed, PSConfiguration.Instance != null ? PSConfiguration.Instance.MaximumAllowedPTZSpeed : 10f);
            
            _currentPanState = Mathf.MoveTowards(_currentPanState, overridePanAngle, overrideAbsoluteMaxPan * Time.deltaTime);
            if (!allowContinuousPan) _currentPanState = Mathf.Clamp(NormalizeAngle(_currentPanState), panMin, panMax);
            transform.localEulerAngles = SetAxisValue(transform.localEulerAngles, panAxis, _currentPanState);
            
            _currentTiltState = Mathf.MoveTowards(_currentTiltState, overrideTiltAngle, overrideAbsoluteMaxTilt * Time.deltaTime);
            _currentTiltState = Mathf.Clamp(NormalizeAngle(_currentTiltState), tiltMin, tiltMax);
            tiltBase.localEulerAngles = SetAxisValue(tiltBase.localEulerAngles, tiltAxis, _currentTiltState);
            
            _commandedPanVelocity = 0f;
            _commandedTiltVelocity = 0f;
            _integralX = 0f;
            _integralY = 0f;
            _lastSetTilt = _currentTiltState;
            return;
        }

        // 2. Deadband and Error Extraction
        float rawErrorX = trackingMetrics.ErrorX;
        float rawErrorY = trackingMetrics.ErrorY;
        
        float errorX = Mathf.Abs(rawErrorX) < deadband ? 0f : rawErrorX;
        float errorY = Mathf.Abs(rawErrorY) < deadband ? 0f : rawErrorY;

        // 3. Convert Pixel Error to True Angular Error
        float vFov = 3f;
        float hFov = 4f;
        if (_cachedVirtualCamera != null)
        {
            vFov = _cachedVirtualCamera.fieldOfView;
            hFov = Camera.VerticalToHorizontalFieldOfView(vFov, _cachedVirtualCamera.aspect);
        }
        
        float degPerPixelX = hFov / 640f;
        float degPerPixelY = vFov / 480f;

        float angleErrorX = errorX * degPerPixelX;
        float angleErrorY = errorY * degPerPixelY;

        // 4. Integral Accumulation with strict velocity windup limit
        _integralX += angleErrorX * Time.deltaTime;
        _integralY += angleErrorY * Time.deltaTime;
        
        float iLimitX = maxIntegralVelocity / (panKi > 0f ? panKi : 1f);
        float iLimitY = maxIntegralVelocity / (tiltKi > 0f ? tiltKi : 1f);
        
        _integralX = Mathf.Clamp(_integralX, -iLimitX, iLimitX);
        _integralY = Mathf.Clamp(_integralY, -iLimitY, iLimitY);

        // 5. Derivative (Error Velocity) Estimation
        if (_hasPreviousError && Time.deltaTime > 0f)
        {
            float rawVelX = (angleErrorX - _previousErrorX) / Time.deltaTime;
            float rawVelY = (angleErrorY - _previousErrorY) / Time.deltaTime;
            
            // Frame-rate independent exponential smoothing
            float alpha = commandTimeConstant > 0f ? (1f - Mathf.Exp(-Time.deltaTime / errorVelocityTimeConstant)) : 1f;
            _filteredErrorVelX = Mathf.Lerp(_filteredErrorVelX, rawVelX, alpha);
            _filteredErrorVelY = Mathf.Lerp(_filteredErrorVelY, rawVelY, alpha);
        }
        else
        {
            _filteredErrorVelX = 0f;
            _filteredErrorVelY = 0f;
            _hasPreviousError = true;
        }
        
        _previousErrorX = angleErrorX;
        _previousErrorY = angleErrorY;
        _currentErrorX = angleErrorX; // Inspector will now show angular error
        _currentErrorY = angleErrorY;

        // Apply strict PS limits from the global configuration
        float maxLimit = 10f; // Default fallback absolute maximum
        if (PSConfiguration.Instance != null)
        {
            maxLimit = PSConfiguration.Instance.MaximumAllowedPTZSpeed;
        }

        // Ensure we never physically drive the gimbal faster than the strict limits
        float absoluteMaxPan = Mathf.Min(panSpeed, maxLimit);
        float absoluteMaxTilt = Mathf.Min(tiltSpeed, maxLimit);

        HandlePan(absoluteMaxPan);
        HandleTilt(absoluteMaxTilt);

        // --- POST-DIAGNOSTIC LIFECYCLE ---
        if (runDiagnosticPlantTest && _diagnosticMeasurementsComplete && !_diagnosticFullyComplete)
        {
            if (trackingMetrics.CurrentState == TrackingState.LOCKED)
            {
                _testTimer += Time.deltaTime; 
                if (_testTimer >= stationaryLockDwellTime)
                {
                    _diagnosticFullyComplete = true;
                    UAVTrajectoryController uav = FindAnyObjectByType<UAVTrajectoryController>();
                    if (uav != null)
                    {
                        uav.diagnosticFreeze = false;
                        uav.ResetTrajectoryTiming();
                    }
                    Debug.Log("[PanStepTest] EXIT_DIAGNOSTIC\nFreeze=false\nTrajectory=" + (uav != null ? uav.currentMode.ToString() : "Unknown"));
                }
            }
            else
            {
                _testTimer = 0f; // reset if lock is lost
            }
        }

        if (Time.time >= _nextDebugLogTime)
        {
            _nextDebugLogTime = Time.time + 0.2f; // 5 Hz
            bool visible = trackingMetrics.CurrentState != TrackingState.SEARCHING && trackingMetrics.CurrentState != TrackingState.LOST;
            
            if (runDiagnosticPlantTest && _diagnosticFullyComplete)
            {
                // We have resumed dynamic tracking, output the required log format
                float radialError = Mathf.Sqrt(rawErrorX * rawErrorX + rawErrorY * rawErrorY);
                UAVTrajectoryController uav = FindAnyObjectByType<UAVTrajectoryController>();
                Vector3 targetPos = uav != null ? uav.transform.position : Vector3.zero;
                
                Debug.Log($"[DynamicTrackingTest]\n" +
                          $"TargetWorldPosition: {targetPos}\n" +
                          $"Centroid: ({(rawErrorX + 320f):F1}, {(rawErrorY + 240f):F1})\n" +
                          $"PixelError: ({rawErrorX:F1}, {rawErrorY:F1})\n" +
                          $"RadialError: {radialError:F1} px\n" +
                          $"Pan: {_currentPanState:F2}°\n" +
                          $"Tilt: {_currentTiltState:F2}°\n" +
                          $"TrackingState: {trackingMetrics.CurrentState}");
            }
            else
            {
                // Standard debug log
                Debug.Log($"[PanTiltControlDebug]\n" +
                          $"TrackingState: {trackingMetrics.CurrentState}\n" +
                          $"Centroid: ({(rawErrorX + 320f):F1}, {(rawErrorY + 240f):F1})\n" +
                          $"PixelError: ({rawErrorX:F1}, {rawErrorY:F1})\n" +
                          $"AngularErrorX: {angleErrorX:F3}°\n" +
                          $"AngularErrorY: {angleErrorY:F3}°\n" +
                          $"CommandPanDegPerSec: {_commandedPanVelocity:F2}\n" +
                          $"CommandTiltDegPerSec: {_commandedTiltVelocity:F2}\n" +
                          $"ActualPan: {_currentPanState:F2}°\n" +
                          $"ActualTilt: {_currentTiltState:F2}°\n" +
                          $"CameraForward: {(_cachedVirtualCamera != null ? _cachedVirtualCamera.transform.forward.ToString("F3") : "NULL")}\n" +
                          $"Target/BeaconVisible: {visible}");
            }
        }
    }

    private void HandlePan(float maxSpeed)
    {
        _pContributionX = _currentErrorX * panKp * panDirection;
        _iContributionX = _integralX * panKi * panDirection;
        _dContributionX = -(_filteredErrorVelX * panKd * panDirection);

        float rawVelocity = _pContributionX + _iContributionX + _dContributionX + feedForwardPanVelocity;
        rawVelocity = Mathf.Clamp(rawVelocity, -maxSpeed, maxSpeed);
        _rawCommandPan = rawVelocity;

        // 1. Output Smoothing (Frame-rate Independent Exponential Moving Average)
        float alpha = commandTimeConstant > 0f ? (1f - Mathf.Exp(-Time.deltaTime / commandTimeConstant)) : 1f;
        float smoothedVelocity = Mathf.Lerp(_commandedPanVelocity, rawVelocity, alpha);

        // 2. Acceleration Slew Rate Limiting (deg/s^2)
        float maxDelta = maxAngularAcceleration * Time.deltaTime;
        float deltaVelocity = smoothedVelocity - _commandedPanVelocity;
        deltaVelocity = Mathf.Clamp(deltaVelocity, -maxDelta, maxDelta);
        
        _appliedCommandDeltaPan = deltaVelocity;
        _commandedPanVelocity += deltaVelocity;
        _commandedPanVelocity = Mathf.Clamp(_commandedPanVelocity, -maxSpeed, maxSpeed);

        _currentPanState += _commandedPanVelocity * Time.deltaTime;

        // 5. Enforce mechanical bounds
        if (!allowContinuousPan)
        {
            _currentPanState = Mathf.Clamp(NormalizeAngle(_currentPanState), panMin, panMax);
        }
        
        if (!bypassActuator)
        {
            // Apply strictly to the configured local rotation axis of the PanBase
            transform.localEulerAngles = SetAxisValue(transform.localEulerAngles, panAxis, _currentPanState);
        }
    }

    private void HandleTilt(float maxSpeed)
    {
        _pContributionY = _currentErrorY * tiltKp * tiltDirection;
        _iContributionY = _integralY * tiltKi * tiltDirection;
        _dContributionY = -(_filteredErrorVelY * tiltKd * tiltDirection);

        float rawVelocity = _pContributionY + _iContributionY + _dContributionY + feedForwardTiltVelocity;
        rawVelocity = Mathf.Clamp(rawVelocity, -maxSpeed, maxSpeed);
        _rawCommandTilt = rawVelocity;

        // 1. Output Smoothing (Frame-rate Independent Exponential Moving Average)
        float alpha = commandTimeConstant > 0f ? (1f - Mathf.Exp(-Time.deltaTime / commandTimeConstant)) : 1f;
        float smoothedVelocity = Mathf.Lerp(_commandedTiltVelocity, rawVelocity, alpha);

        // 2. Acceleration Slew Rate Limiting (deg/s^2)
        float maxDelta = maxAngularAcceleration * Time.deltaTime;
        float deltaVelocity = smoothedVelocity - _commandedTiltVelocity;
        deltaVelocity = Mathf.Clamp(deltaVelocity, -maxDelta, maxDelta);
        
        _appliedCommandDeltaTilt = deltaVelocity;
        _commandedTiltVelocity += deltaVelocity;
        _commandedTiltVelocity = Mathf.Clamp(_commandedTiltVelocity, -maxSpeed, maxSpeed);

        // Diagnostic: Check if another script overwrote our last command
        float currentRawTilt = GetAxisValue(tiltBase.localEulerAngles, tiltAxis);
        float transformTilt = NormalizeAngle(currentRawTilt);

        if (!float.IsNaN(_lastSetTilt))
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(transformTilt, _lastSetTilt));
            if (diff > 0.01f)
            {
                _wasOverwrittenByOtherScript = true;
                // Optionally resync internal state to external override:
                // _currentTiltState = transformTilt;
            }
            else
            {
                _wasOverwrittenByOtherScript = false;
            }
        }

        // 4. Integrate velocity using internal state
        float tiltDelta = _commandedTiltVelocity * Time.deltaTime;
        _appliedTiltDelta = tiltDelta;
        _currentTiltState += tiltDelta;

        // 5. Enforce mechanical bounds
        _currentTiltState = Mathf.Clamp(NormalizeAngle(_currentTiltState), tiltMin, tiltMax);

        if (!bypassActuator)
        {
            // Apply to configured axis
            tiltBase.localEulerAngles = SetAxisValue(tiltBase.localEulerAngles, tiltAxis, _currentTiltState);
        }
        
        _lastSetTilt = _currentTiltState;

        // Update Diagnostics
        _tiltBaseLocalPitchAxis = _currentTiltState;
        if (_cachedVirtualCamera != null)
        {
            _virtualCameraLocalX = NormalizeAngle(_cachedVirtualCamera.transform.localEulerAngles.x);
        }
    }

    private float GetAxisValue(Vector3 euler, RotationAxis axis)
    {
        switch (axis)
        {
            case RotationAxis.X: return euler.x;
            case RotationAxis.Y: return euler.y;
            case RotationAxis.Z: return euler.z;
            default: return euler.x;
        }
    }

    private Vector3 SetAxisValue(Vector3 euler, RotationAxis axis, float value)
    {
        switch (axis)
        {
            case RotationAxis.X: euler.x = value; break;
            case RotationAxis.Y: euler.y = value; break;
            case RotationAxis.Z: euler.z = value; break;
        }
        return euler;
    }

    /// <summary>
    /// Normalizes an angle to be strictly within the -180 to 180 degrees range.
    /// Helpful for comparing Unity's [0, 360] euler angles against [-180, 180] inspector limits.
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

    // ==========================================
    // STATIONARY DIAGNOSTIC PLANT TEST
    // ==========================================
    
    private int _testPhase = 0; 
    private float _testTimer = 0f;
    private int _testSamples = 0;
    private Vector2 _testCentroidSum = Vector2.zero;
    
    private float _basePan, _baseTilt;
    private Vector2 _baseCentroid;
    
    private float _startAngle;
    private Vector2 _startCentroid;
    
    private float _jxSum = 0f, _jySum = 0f;
    private int _validPanSamples = 0;
    private int _validTiltSamples = 0;
    private int _invalidSteps = 0;

    private float GetActualPan() { return NormalizeAngle(GetAxisValue(transform.localEulerAngles, panAxis)); }
    private float GetActualTilt() { return NormalizeAngle(GetAxisValue(tiltBase.localEulerAngles, tiltAxis)); }

    private bool RunDiagnosticPlantTest()
    {
        float targetPanVel = 0f;
        float targetTiltVel = 0f;
        
        bool isTracking = trackingMetrics.CurrentState == TrackingState.ACQUIRING || trackingMetrics.CurrentState == TrackingState.LOCKED;
        
        if (_testPhase == 0) // Initial Wait
        {
            if (isTracking) { _testTimer += Time.deltaTime; } else { _testTimer = 0f; }
            if (_testTimer > 1.0f) 
            { 
                // Set safe baseline BEFORE the 4 measurements to avoid Zenith gimbal lock limit
                _currentTiltState = -89.0f;
                tiltBase.localEulerAngles = SetAxisValue(tiltBase.localEulerAngles, tiltAxis, _currentTiltState);
                
                _testPhase = 5; 
                _testTimer = 0f; 
            } 
        }
        else if (_testPhase == 5)
        {
            // Wait 1.0 seconds for the physical baseline step to visually settle on the sensor
            _testTimer += Time.deltaTime;
            if (_testTimer > 1.0f && isTracking)
            {
                _testPhase = 10;
                _testTimer = 0f;
            }
        }
        else if (_testPhase == 10 || _testPhase == 30 || _testPhase == 50 || _testPhase == 70) // Establish Baseline
        {
            _testTimer += Time.deltaTime;
            if (_testTimer > 0.15f && _testTimer <= 0.35f && isTracking)
            {
                _testSamples++;
                _testCentroidSum += new Vector2(trackingMetrics.ErrorX + 320f, trackingMetrics.ErrorY + 240f);
            }
            if (_testTimer > 0.35f)
            {
                _baseCentroid = _testCentroidSum / Mathf.Max(1, _testSamples);
                _basePan = GetActualPan();
                _baseTilt = GetActualTilt();
                _startCentroid = _baseCentroid;
                _startAngle = (_testPhase == 10 || _testPhase == 30) ? _basePan : _baseTilt;
                
                _testPhase += 10; // Go to 20, 40, 60, 80
                _testTimer = 0f; _testSamples = 0; _testCentroidSum = Vector2.zero;
            }
        }
        else if (_testPhase == 20 || _testPhase == 40 || _testPhase == 60 || _testPhase == 80) // Apply Step
        {
            if (_testPhase == 20) targetPanVel = 1.0f;
            else if (_testPhase == 40) targetPanVel = -1.0f;
            else if (_testPhase == 60) targetTiltVel = 1.0f;
            else if (_testPhase == 80) targetTiltVel = -1.0f;

            _testTimer += Time.deltaTime;
            if (_testTimer > 0.5f)
            {
                _testPhase += 1; // Go to 21, 41, 61, 81
                _testTimer = 0f; _testSamples = 0; _testCentroidSum = Vector2.zero;
            }
        }
        else if (_testPhase == 21 || _testPhase == 41 || _testPhase == 61 || _testPhase == 81) // Measure and Reset
        {
            _testTimer += Time.deltaTime;
            // Wait 0.15s to settle, then sample for 0.20s
            if (_testTimer > 0.15f && _testTimer <= 0.35f && isTracking)
            {
                _testSamples++;
                _testCentroidSum += new Vector2(trackingMetrics.ErrorX + 320f, trackingMetrics.ErrorY + 240f);
            }
            if (_testTimer > 0.35f)
            {
                Vector2 endCentroid = _testCentroidSum / Mathf.Max(1, _testSamples);
                float endAngle = (_testPhase == 21 || _testPhase == 41) ? GetActualPan() : GetActualTilt();
                
                float deltaAngle = endAngle - _startAngle;
                Vector2 deltaPixel = endCentroid - _startCentroid;
                string stepName = _testPhase == 21 ? "+Pan" : _testPhase == 41 ? "-Pan" : _testPhase == 61 ? "+Tilt" : "-Tilt";
                
                if (Mathf.Abs(deltaAngle) < 0.001f || float.IsNaN(deltaAngle) || float.IsNaN(deltaPixel.x) || float.IsNaN(deltaPixel.y))
                {
                    _invalidSteps++;
                    Debug.LogWarning($"[PanStepTest] Step {stepName} INVALID due to zero delta or NaN. deltaAngle={deltaAngle}");
                }
                else
                {
                    float pixelPerDeg = (_testPhase == 21 || _testPhase == 41) ? (deltaPixel.x / deltaAngle) : (deltaPixel.y / deltaAngle);
                    
                    if (_testPhase == 21 || _testPhase == 41) { _jxSum += pixelPerDeg; _validPanSamples++; }
                    if (_testPhase == 61 || _testPhase == 81) { _jySum += pixelPerDeg; _validTiltSamples++; }

                    Debug.Log($"[PanStepTest]\nStep: {stepName}\nCentroidBefore: {_startCentroid:F2}\nCentroidAfter: {endCentroid:F2}\nActualAngleBefore: {_startAngle:F4}\nActualAngleAfter: {endAngle:F4}\nDeltaPixel: {deltaPixel:F2}\nDeltaAngle: {deltaAngle:F4}\nPixelPerDegree: {pixelPerDeg:F2}");
                }
                
                // Reset mechanically
                _currentPanState = _basePan;
                _currentTiltState = _baseTilt;
                transform.localEulerAngles = SetAxisValue(transform.localEulerAngles, panAxis, _currentPanState);
                tiltBase.localEulerAngles = SetAxisValue(tiltBase.localEulerAngles, tiltAxis, _currentTiltState);

                if (_testPhase == 81)
                {
                    _testPhase = 100;
                }
                else
                {
                    // Back to baseline mode
                    _testPhase = (_testPhase == 21) ? 30 : (_testPhase == 41) ? 50 : 70;
                    _testTimer = 0f; _testSamples = 0; _testCentroidSum = Vector2.zero;
                }
            }
        }
        else if (_testPhase == 100)
        {
            float avgJx = _validPanSamples > 0 ? _jxSum / _validPanSamples : 0f;
            float avgJy = _validTiltSamples > 0 ? _jySum / _validTiltSamples : 0f;
            
            // PID Equation: _pContributionX = ErrorX * Kp * panDirection
            // If avgJx > 0, +Pan command yields +PixelX target movement. 
            // If ErrorX > 0 (target is right), we need target to move LEFT (-PixelX), so we need -Pan command.
            // Therefore ErrorX * Kp * panDirection must equal negative command.
            // Hence, panDirection must be negative.
            if (_validPanSamples > 0)
            {
                panDirection = -Mathf.Sign(avgJx);
            }
            if (_validTiltSamples > 0)
            {
                tiltDirection = -Mathf.Sign(avgJy);
            }
            
            Debug.Log($@"[PanStepTest] VALIDATION
Pan Jacobian = {avgJx:F2} px/deg
Tilt Jacobian = {avgJy:F2} px/deg
Pan sign = {panDirection}
Tilt sign = {tiltDirection}
InvalidSteps = {_invalidSteps}");
            
            // Reset internal integral states to prevent sudden jerks
            _integralX = 0f;
            _integralY = 0f;
            _commandedPanVelocity = 0f;
            _commandedTiltVelocity = 0f;
            
            _testTimer = 0f; // Reset for dwell
            return true; // Measurements done, transition to dwell
        }

        // Only apply kinematics if we are in a step phase
        if (_testPhase == 20 || _testPhase == 40 || _testPhase == 60 || _testPhase == 80)
        {
            _currentPanState += targetPanVel * Time.deltaTime;
            if (!allowContinuousPan) _currentPanState = Mathf.Clamp(NormalizeAngle(_currentPanState), panMin, panMax);
            transform.localEulerAngles = SetAxisValue(transform.localEulerAngles, panAxis, _currentPanState);

            _currentTiltState += targetTiltVel * Time.deltaTime;
            _currentTiltState = Mathf.Clamp(NormalizeAngle(_currentTiltState), tiltMin, tiltMax);
            tiltBase.localEulerAngles = SetAxisValue(tiltBase.localEulerAngles, tiltAxis, _currentTiltState);
        }

        return false;
    }
}
