using UnityEngine;

/// <summary>
/// Controls the movement of a target along a continuous, smooth orbital/circular trajectory.
/// This script does not require any external packages and works out-of-the-box in Unity 6.
/// </summary>
public class UAVTrajectoryController : MonoBehaviour
{
    public enum TrajectoryMode
    {
        Circular,
        StraightLine,
        ControlledMovingTest,
        Figure8,
        Random
    }

    public enum OrbitDirection
    {
        Clockwise = -1,
        CounterClockwise = 1
    }

    public enum SimulationPreset
    {
        Custom,
        DebugClose,
        UAVDemonstration,
        HighAltitude
    }

    [Header("Simulation Scale Setup")]
    [Tooltip("Select a preset to automatically configure radius and altitude.")]
    public SimulationPreset preset = SimulationPreset.Custom;

    // Used to detect when the user changes the preset in the Inspector
    [SerializeField, HideInInspector]
    private SimulationPreset _previousPreset = SimulationPreset.Custom;

    [Header("TRAJECTORY MODE")]
    [Tooltip("Select the active motion scenario for the UAV.")]
    public TrajectoryMode currentMode = TrajectoryMode.Circular;

    [Header("STRAIGHT LINE PARAMETERS")]
    [Tooltip("The starting position of the straight line path.")]
    public Vector3 straightLineStart = new Vector3(-50f, 100f, 200f);
    
    [Tooltip("The vector direction of travel.")]
    public Vector3 straightLineDirection = Vector3.right;
    
    [Tooltip("Speed of travel in meters per second.")]
    public float straightLineSpeed = 5f;
    
    [Tooltip("Total distance to travel before reversing direction (Ping-Pong).")]
    public float straightLineRange = 100f;

    [Header("CIRCULAR PARAMETERS")]
    [Tooltip("The center point of the orbit in world space.")]
    public Vector3 orbitCenter = Vector3.zero;

    [Tooltip("The radius of the circular orbit.")]
    public float orbitRadius = 10f;

    [Tooltip("The constant height (Y-axis) of the UAV relative to the orbit center.")]
    public float altitude = 5f;

    [Tooltip("The speed of rotation in degrees per second.")]
    public float angularSpeed = 45f;

    [Tooltip("Direction of the orbit around the Y axis.")]
    public OrbitDirection direction = OrbitDirection.Clockwise;

    [Header("FIGURE-8 PARAMETERS")]
    [Tooltip("The width (X) and length (Z) amplitude of the Figure-8.")]
    public Vector2 figure8Amplitude = new Vector2(30f, 40f);
    
    [Tooltip("Nominal time in seconds to complete one full Figure-8 traversal.")]
    public float figure8Period = 20f;

    [Tooltip("The maximum linear speed of the target in m/s. This stretches the effective period to prevent the target from outrunning the PTZ camera limits.")]
    public float figure8MaxSpeed = 8f;

    [Header("RANDOM PARAMETERS")]
    [Tooltip("The total width (X) and length (Z) of the bounding area for random motion.")]
    public Vector2 randomBounds = new Vector2(100f, 100f);
    
    [Tooltip("Speed of random traversal. Controls how fast it moves through the continuous noise field.")]
    public float randomSpeed = 0.5f;

    [Tooltip("Seed for reproducible random trajectories. Different seeds produce different paths.")]
    public int randomSeed = 42;

    [Header("Orientation Settings")]
    [Tooltip("If true, the object will rotate to face its movement direction.")]
    public bool faceMovementDirection = true;

    [Header("SENSOR CALIBRATION")]
    [Tooltip("If true, normal orbit is suspended and the UAV is locked in front of the calibration camera.")]
    public bool sensorCalibrationMode = false;
    public Transform calibrationCamera;
    public float calibrationDistance = 50f;
    public float horizontalOffset = 0f;
    public float verticalOffset = 0f;

    [Header("STATIC TARGET TEST MODE")]
    [Tooltip("If true, all movement and calibration logic is disabled. Target is placed relative to the camera's initial view.")]
    public bool staticTargetTestMode = false;
    
    [Tooltip("Distance from the camera to place the target.")]
    public float staticTargetDistance = 200f;
    
    [Tooltip("Vertical offset in pixels relative to center (positive = up in image space)")]
    public float staticTargetPixelOffset = 0f;

    [Header("DIAGNOSTIC FREEZE")]
    [Tooltip("If true, perfectly freezes the UAV at its current position after acquisition for control plant testing.")]
    public bool diagnosticFreeze = true;

    [Header("Static Test Diagnostics (Read Only)")]
    [SerializeField] private Vector3 _initialCameraPosition;
    [SerializeField] private Vector3 _initialCameraForward;
    [SerializeField] private Vector3 _staticTargetWorldPosition;

    [Header("Runtime Diagnostics (Read Only)")]
    [SerializeField] private TrajectoryMode _currentTrajectoryMode;
    [SerializeField] private Vector3 _initialTargetPosition;
    [SerializeField] private Vector3 _currentTargetPosition;
    [SerializeField] private float _currentTargetSpeed;
    [SerializeField] private float _normalizedTime;
    [SerializeField] private int _activeSeed;
    [SerializeField] private bool _testActive;

    // Internal accumulators and state flags
    private float currentAngle = -90f;
    private float _straightLineCurrentDistance = 0f;
    private int _straightLineCurrentDirection = 1;
    private Vector3 _controlledTestRightVector;

    private bool _previousCalibrationMode = false;
    private bool _staticTargetInitialized = false;
    private bool _controlledTestInitialized = false;

    private Vector3 _transitionStartPosition;
    private float _modeTransitionProgress = 1f;

    private Vector2 _randomCurrentDirection;
    private Vector2 _randomCurrentPosXZ;
    private float _randomSteeringTime;
    private float _randomHeadingAngle;
    private bool _randomInitialized;

    public void ApplyValidatedPhase2Parameters(TrajectoryMode mode)
    {
        // 1. Enforce global Phase 2 benchmark constraints
        diagnosticFreeze = false;
        sensorCalibrationMode = false;
        staticTargetTestMode = false;

        // 2. Enforce mode-specific trajectory bounds
        switch (mode)
        {
            case TrajectoryMode.Circular:
                orbitRadius = 10f;
                angularSpeed = 18f;
                altitude = 0f;
                break;
                
            case TrajectoryMode.StraightLine:
                straightLineRange = 40f;
                straightLineSpeed = 5f;
                straightLineStart = new Vector3(orbitCenter.x, orbitCenter.y + altitude, orbitCenter.z);
                break;
                
            case TrajectoryMode.Figure8:
                figure8Amplitude = new Vector2(30f, 40f);
                figure8Period = 20f;
                break;
                
            case TrajectoryMode.Random:
                randomBounds = new Vector2(15f, 15f);
                randomSpeed = 5f;
                break;
        }
    }

    private void Awake()
    {
        // OVERRIDE BROKEN SCENE SERIALIZATION:
        // Force the validated Phase 2 configuration into memory immediately on startup
        // before any other script (like Start or external initialization) calculates geometry.
        ApplyValidatedPhase2Parameters(currentMode);
    }

    private bool _acquisitionGeometryEstablished = false;

    private void OnValidate()
    {
        if (figure8Period < 0.1f) figure8Period = 0.1f;
        if (randomSpeed < 0f) randomSpeed = 0f;

        // Apply preset values if the user changes the dropdown in the inspector
        if (preset != _previousPreset)
        {
            switch (preset)
            {
                case SimulationPreset.DebugClose:
                    orbitRadius = 10f;
                    altitude = 15f;
                    break;
                case SimulationPreset.UAVDemonstration:
                    orbitRadius = 200f;
                    altitude = 100f;
                    break;
                case SimulationPreset.HighAltitude:
                    orbitRadius = 1000f;
                    altitude = 500f;
                    break;
            }
            _previousPreset = preset;
        }
    }

    private void Start()
    {
        // Force the PTZ to horizontal center to prevent Gimbal Lock spawning
        PanTiltTracker tracker = Object.FindAnyObjectByType<PanTiltTracker>();
        if (tracker != null)
        {
            tracker.ForceState(0f, 0f);
            tracker.transform.localEulerAngles = Vector3.zero;
            if (tracker.tiltBase != null) tracker.tiltBase.localEulerAngles = Vector3.zero;
        }

        // Establish initial acquisition geometry for dynamic modes at simulation start
        if (currentMode != TrajectoryMode.ControlledMovingTest)
        {
            EstablishInitialAcquisitionGeometry();

            // RE-APPLY parameters now that orbitCenter is firmly established
            // This safely resolves straightLineStart = orbitCenter
            ApplyValidatedPhase2Parameters(currentMode);

            // Snap the UAV instantly to the initialized acquisition geometry
            transform.position = new Vector3(orbitCenter.x, orbitCenter.y + altitude, orbitCenter.z);

            // Prevent the first Update() from triggering a false mode transition lerp
            _currentTrajectoryMode = currentMode;
            _modeTransitionProgress = 1f;
            _transitionStartPosition = transform.position;

            if (calibrationCamera != null)
            {
                Debug.Log($"[UAVTrajectoryController] Startup acquisition target initialized\n" +
                          $"Mode: {currentMode}\n" +
                          $"Target Position: {transform.position}\n" +
                          $"Camera Forward: {calibrationCamera.forward}\n" +
                          $"Distance: {staticTargetDistance}");
            }
        }
        else
        {
            // Sync mode to prevent a false mode-transition on the very first frame for legacy modes
            _currentTrajectoryMode = currentMode;
        }
    }

    public void EstablishInitialAcquisitionGeometry()
    {
        Camera cam = calibrationCamera != null ? calibrationCamera.GetComponent<Camera>() : null;
        if (cam == null)
        {
            foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            {
                if (c.name == "VirtualCamera") { cam = c; break; }
            }
        }
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            Vector3 forwardDir = cam.transform.forward;
            Vector3 basePos = cam.transform.position + forwardDir * staticTargetDistance;
            
            // Adjust orbitCenter so that when altitude is added, the UAV is exactly at basePos
            orbitCenter = new Vector3(basePos.x, basePos.y - altitude, basePos.z);
            _initialCameraPosition = cam.transform.position;
            _initialCameraForward = forwardDir;
            _acquisitionGeometryEstablished = true;
            Debug.Log($"[Geometry] CamPos: {cam.transform.position}, CamFwd: {forwardDir}, orbitCenter: {orbitCenter}, altitude: {altitude}");
        }
    }

    public void ForcePositionUpdate()
    {
        Update();
    }

    private void Update()
    {
        if (diagnosticFreeze && _acquisitionGeometryEstablished)
        {
            // Lock position entirely
            return;
        }

        if (staticTargetTestMode)
        {
            if (!_staticTargetInitialized)
            {
                if (calibrationCamera != null)
                {
                    _initialCameraPosition = calibrationCamera.position;
                    _initialCameraForward = calibrationCamera.forward;

                    // Convert pixel offset to degrees. (Vertical FOV = 3 deg, Height = 480 px)
                    // 3 / 480 = 0.00625 degrees per pixel
                    float angleOffset = staticTargetPixelOffset * (3f / 480f);

                    // In Unity (Left-Handed), positive rotation around the camera's Right axis pitches DOWN.
                    // To place the target UP (positive pixel offset), we rotate by a negative angle.
                    Quaternion offsetRotation = Quaternion.AngleAxis(-angleOffset, calibrationCamera.right);
                    Vector3 offsetDirection = offsetRotation * calibrationCamera.forward;

                    _staticTargetWorldPosition = _initialCameraPosition + (offsetDirection.normalized * staticTargetDistance);
                    
                    transform.position = _staticTargetWorldPosition;
                    _staticTargetInitialized = true;
                    Debug.Log($"[UAVTrajectoryController] Static Test Target locked at {_staticTargetWorldPosition} with offset {angleOffset} deg");
                }
                else
                {
                    Debug.LogWarning("[UAVTrajectoryController] Calibration Camera missing. Cannot initialize static target test mode.");
                }
            }
            else
            {
                // Force it to remain perfectly stationary
                transform.position = _staticTargetWorldPosition;
            }
            return;
        }
        else
        {
            // Reset the initialization flag if toggled off so it can be recaptured later if needed
            _staticTargetInitialized = false;
        }

        if (sensorCalibrationMode)
        {
            if (!_previousCalibrationMode)
            {
                Debug.Log("[UAVTrajectoryController] Sensor calibration mode active.");
                _previousCalibrationMode = true;
            }

            if (calibrationCamera != null)
            {
                transform.position = calibrationCamera.position 
                    + calibrationCamera.forward * calibrationDistance 
                    + calibrationCamera.right * horizontalOffset 
                    + calibrationCamera.up * verticalOffset;

                if (faceMovementDirection)
                {
                    transform.forward = -calibrationCamera.forward;
                }
            }
            return;
        }

        if (_previousCalibrationMode)
        {
            _previousCalibrationMode = false;
        }

        // ==========================================
        // DYNAMIC TRAJECTORY LOGIC
        // ==========================================
        
        if (_currentTrajectoryMode != currentMode)
        {
            _controlledTestInitialized = false;
            _normalizedTime = 0f; // Reset continuous time accumulator for new modes
            
            if (Application.isPlaying && currentMode != TrajectoryMode.ControlledMovingTest)
            {
                if (!_acquisitionGeometryEstablished)
                {
                    EstablishInitialAcquisitionGeometry();
                }

                _transitionStartPosition = transform.position;
                _modeTransitionProgress = 0f;
                
                if (currentMode == TrajectoryMode.Random)
                {
                    _randomInitialized = false;
                }
            }
            else
            {
                _modeTransitionProgress = 1f; // instantly complete for legacy modes
            }
            
            _currentTrajectoryMode = currentMode;
            
            PanTiltTracker ptz = Object.FindAnyObjectByType<PanTiltTracker>();
            float ptzPanRate = ptz != null ? ptz.panSpeed : -1f;
            float ptzTiltRate = ptz != null ? ptz.tiltSpeed : -1f;

            float maxAngularVelocity = 0f;
            float maxAngularAccel = 0f;

            if (_currentTrajectoryMode == TrajectoryMode.StraightLine)
            {
                float minDist = Mathf.Max(0.1f, staticTargetDistance);
                maxAngularVelocity = (straightLineSpeed / minDist) * Mathf.Rad2Deg;
                maxAngularAccel = (straightLineSpeed * straightLineSpeed) / (minDist * minDist) * Mathf.Rad2Deg;
                
                if (maxAngularVelocity > ptzPanRate && ptzPanRate > 0)
                {
                    straightLineSpeed = (ptzPanRate * Mathf.Deg2Rad) * minDist * 0.95f;
                    maxAngularVelocity = (straightLineSpeed / minDist) * Mathf.Rad2Deg;
                }
            }
            else if (_currentTrajectoryMode == TrajectoryMode.Circular)
            {
                float v = (angularSpeed * Mathf.Deg2Rad) * orbitRadius;
                float minDist = Mathf.Max(0.1f, staticTargetDistance - orbitRadius);
                maxAngularVelocity = (v / minDist) * Mathf.Rad2Deg;
                maxAngularAccel = (v * v) / (minDist * minDist) * Mathf.Rad2Deg;
                
                if (maxAngularVelocity > ptzPanRate && ptzPanRate > 0)
                {
                    v = (ptzPanRate * Mathf.Deg2Rad) * minDist * 0.95f;
                    angularSpeed = (v / orbitRadius) * Mathf.Rad2Deg;
                    maxAngularVelocity = (v / minDist) * Mathf.Rad2Deg;
                }
            }
            else if (_currentTrajectoryMode == TrajectoryMode.Figure8)
            {
                float aX = figure8Amplitude.x;
                float aZ = figure8Amplitude.y;
                float w = (Mathf.PI * 2f) / Mathf.Max(0.1f, figure8Period);
                float estMaxSpeed = w * Mathf.Sqrt(aX * aX + 4f * aZ * aZ);
                float minDist = Mathf.Max(0.1f, staticTargetDistance - aZ);
                maxAngularVelocity = (estMaxSpeed / minDist) * Mathf.Rad2Deg;
                maxAngularAccel = (estMaxSpeed * estMaxSpeed) / (minDist * minDist) * Mathf.Rad2Deg;
                
                if (maxAngularVelocity > ptzPanRate && ptzPanRate > 0)
                {
                    estMaxSpeed = (ptzPanRate * Mathf.Deg2Rad) * minDist * 0.95f;
                    w = estMaxSpeed / Mathf.Sqrt(aX * aX + 4f * aZ * aZ);
                    figure8Period = (Mathf.PI * 2f) / w;
                    maxAngularVelocity = (estMaxSpeed / minDist) * Mathf.Rad2Deg;
                }
            }
            else if (_currentTrajectoryMode == TrajectoryMode.Random)
            {
                float minDist = Mathf.Max(0.1f, staticTargetDistance - randomBounds.y * 0.5f);
                maxAngularVelocity = (randomSpeed / minDist) * Mathf.Rad2Deg;
                maxAngularAccel = 0f;
                
                if (maxAngularVelocity > ptzPanRate && ptzPanRate > 0)
                {
                    randomSpeed = (ptzPanRate * Mathf.Deg2Rad) * minDist * 0.95f;
                    maxAngularVelocity = (randomSpeed / minDist) * Mathf.Rad2Deg;
                }
            }

            if (Application.isPlaying && _currentTrajectoryMode != TrajectoryMode.ControlledMovingTest)
            {
                // We use maxAngularVelocity for both pan and tilt as a conservative estimate, 
                // since orbit projections depend on camera orientation.
                float targetMaxPanRate = maxAngularVelocity;
                float targetMaxTiltRate = maxAngularVelocity;
                
                bool isFeasible = targetMaxPanRate <= ptzPanRate && targetMaxTiltRate <= ptzTiltRate;
                
                Debug.Log($"[TrajectoryFeasibility]\n" +
                          $"Mode: {_currentTrajectoryMode}\n" +
                          $"Target max pan rate = {targetMaxPanRate:F2} deg/s\n" +
                          $"Target max tilt rate = {targetMaxTiltRate:F2} deg/s\n" +
                          $"PTZ max pan rate = {ptzPanRate:F2} deg/s\n" +
                          $"PTZ max tilt rate = {ptzTiltRate:F2} deg/s\n" +
                          $"Feasible = {(isFeasible ? "YES" : "NO")}");
            }
        }
        
        if (currentMode == TrajectoryMode.StraightLine)
        {
            _currentTargetSpeed = straightLineSpeed;
            _testActive = false;

            // 1. Calculate linear distance delta
            float distanceDelta = straightLineSpeed * _straightLineCurrentDirection * Mathf.Min(Time.deltaTime, 0.05f);
            _straightLineCurrentDistance += distanceDelta;

            // 2. Ping-Pong bounds check
            if (_straightLineCurrentDistance > straightLineRange)
            {
                _straightLineCurrentDistance = straightLineRange;
                _straightLineCurrentDirection = -1; // Reverse
            }
            else if (_straightLineCurrentDistance < 0f)
            {
                _straightLineCurrentDistance = 0f;
                _straightLineCurrentDirection = 1; // Forward
            }

            // 3. Apply Position
            Vector3 newPosition = straightLineStart + (straightLineDirection.normalized * _straightLineCurrentDistance);
            transform.position = newPosition;

            // 4. Apply Orientation
            if (faceMovementDirection && straightLineDirection != Vector3.zero)
            {
                transform.forward = straightLineDirection.normalized * _straightLineCurrentDirection;
            }
        }
        else if (currentMode == TrajectoryMode.ControlledMovingTest)
        {
            _currentTargetSpeed = straightLineSpeed;
            _testActive = true;

            if (!_controlledTestInitialized)
            {
                if (calibrationCamera != null)
                {
                    _initialCameraPosition = calibrationCamera.position;
                    _initialCameraForward = calibrationCamera.forward;
                    _controlledTestRightVector = calibrationCamera.right;

                    // 3 degrees / 480 pixels
                    float angleOffset = staticTargetPixelOffset * (3f / 480f);

                    Quaternion offsetRotation = Quaternion.AngleAxis(-angleOffset, _controlledTestRightVector);
                    Vector3 offsetDirection = offsetRotation * _initialCameraForward;

                    _initialTargetPosition = _initialCameraPosition + (offsetDirection.normalized * staticTargetDistance);
                    
                    transform.position = _initialTargetPosition;
                    _straightLineCurrentDistance = 0f;
                    _straightLineCurrentDirection = 1;
                    _controlledTestInitialized = true;
                    Debug.Log($"[UAVTrajectoryController] Controlled Moving Test initialized at {_initialTargetPosition}");
                }
                else
                {
                    Debug.LogWarning("[UAVTrajectoryController] Calibration Camera missing. Cannot initialize Controlled Moving Test.");
                }
            }
            else
            {
                // Independent ping-pong movement perpendicular to initial view
                float distanceDelta = straightLineSpeed * _straightLineCurrentDirection * Mathf.Min(Time.deltaTime, 0.05f);
                _straightLineCurrentDistance += distanceDelta;

                if (_straightLineCurrentDistance > straightLineRange)
                {
                    _straightLineCurrentDistance = straightLineRange;
                    _straightLineCurrentDirection = -1;
                }
                else if (_straightLineCurrentDistance < 0f)
                {
                    _straightLineCurrentDistance = 0f;
                    _straightLineCurrentDirection = 1;
                }

                Vector3 newPosition = _initialTargetPosition + (_controlledTestRightVector * _straightLineCurrentDistance);
                transform.position = newPosition;

                if (faceMovementDirection)
                {
                    transform.forward = _controlledTestRightVector * _straightLineCurrentDirection;
                }
            }
        }
        else if (currentMode == TrajectoryMode.Circular)
        {
            _testActive = false;
            _currentTargetSpeed = (angularSpeed * Mathf.Deg2Rad) * orbitRadius; // Tangential speed v = r * w

            // 1. Calculate the change in angle for this frame.
            float angleDelta = angularSpeed * (float)direction * Mathf.Min(Time.deltaTime, 0.05f);
            
            // 2. Accumulate the current angle.
            currentAngle += angleDelta;

            // 3. Keep the angle within [0, 360) range
            currentAngle = Mathf.Repeat(currentAngle, 360f);

            // 4. Convert degrees to radians
            float angleRad = currentAngle * Mathf.Deg2Rad;

            // 5. Calculate new position using parametric circle equations on the XZ plane.
            float x = orbitCenter.x + Mathf.Cos(angleRad) * orbitRadius;
            float z = orbitCenter.z + Mathf.Sin(angleRad) * orbitRadius;
            float y = orbitCenter.y + altitude;

            // 6. Update the transform's position.
            Vector3 newPosition = new Vector3(x, y, z);
            transform.position = newPosition;

            // 7. Optional: Make the UAV face the direction it's moving
            if (faceMovementDirection)
            {
                Vector3 movementDirection = new Vector3(
                    -Mathf.Sin(angleRad) * (float)direction,
                    0f,
                    Mathf.Cos(angleRad) * (float)direction
                ).normalized;

                if (movementDirection != Vector3.zero)
                {
                    transform.forward = movementDirection;
                }
            }
        }
        else if (currentMode == TrajectoryMode.Figure8)
        {
            _testActive = false;
            float safePeriod = Mathf.Max(0.1f, figure8Period);

            // The Figure-8 shape is parameterized by phase theta (tParam in radians)
            // x(theta) = A * sin(theta)
            // z(theta) = B * sin(2*theta)
            // dx/dtheta = A * cos(theta)
            // dz/dtheta = 2*B * cos(2*theta)

            float tParam = _normalizedTime * Mathf.PI * 2f;
            
            float A = figure8Amplitude.x;
            float B = figure8Amplitude.y;

            float dx_dtheta = A * Mathf.Cos(tParam);
            float dz_dtheta = 2f * B * Mathf.Cos(2f * tParam);

            // speedPerTheta is the instantaneous path derivative magnitude
            float speedPerTheta = Mathf.Sqrt(dx_dtheta * dx_dtheta + dz_dtheta * dz_dtheta);

            // We want physical linear speed <= figure8MaxSpeed. 
            // So: phaseRate * speedPerTheta <= figure8MaxSpeed
            float maxAllowedPhaseRate = figure8MaxSpeed / Mathf.Max(speedPerTheta, 0.001f);
            
            // The nominal phase rate if we just blindly followed the inspector period:
            float nominalPhaseRate = (Mathf.PI * 2f) / safePeriod;

            // Adaptively limit the phase rate to enforce the physical speed ceiling
            float actualPhaseRate = Mathf.Min(nominalPhaseRate, maxAllowedPhaseRate);

            // Advance the phase
            tParam += actualPhaseRate * Mathf.Min(Time.deltaTime, 0.05f);
            
            // Convert back to normalized time [0, 1] for state persistence
            _normalizedTime = (tParam / (Mathf.PI * 2f));
            _normalizedTime = Mathf.Repeat(_normalizedTime, 1f);

            // Lissajous curve: x = A*sin(t), z = B*sin(2t)
            float x = orbitCenter.x + A * Mathf.Sin(tParam);
            float z = orbitCenter.z + B * Mathf.Sin(2f * tParam);
            float y = orbitCenter.y + altitude;

            Vector3 targetPosition = new Vector3(x, y, z);
            Vector3 newPosition = targetPosition;
            
            // Smooth transition from previous location to prevent teleportation
            if (_modeTransitionProgress < 1f)
            {
                _modeTransitionProgress += Mathf.Min(Time.deltaTime, 0.05f) / 1.5f; 
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_modeTransitionProgress));
                newPosition = Vector3.Lerp(_transitionStartPosition, targetPosition, blend);
            }
            
            Vector3 velocity = Vector3.zero;
            if (Mathf.Min(Time.deltaTime, 0.05f) > 0.0001f)
            {
                velocity = (newPosition - transform.position) / Mathf.Min(Time.deltaTime, 0.05f);
            }
            
            _currentTargetSpeed = velocity.magnitude;

            if (faceMovementDirection)
            {
                if (velocity.sqrMagnitude > 0.001f)
                {
                    transform.forward = velocity.normalized;
                }
            }
            
            transform.position = newPosition;
        }
        else if (currentMode == TrajectoryMode.Random)
        {
            _testActive = false;
            _activeSeed = randomSeed;
            
            if (!_randomInitialized)
            {
                // If starting instantly without a transition (e.g. at simulation start), 
                // snap to the established orbitCenter geometry to guarantee acquisition.
                if (_modeTransitionProgress >= 1f)
                {
                    transform.position = new Vector3(orbitCenter.x, orbitCenter.y + altitude, orbitCenter.z);
                }

                // Initialize position from the CURRENT transform.position
                _randomCurrentPosXZ = new Vector2(transform.position.x, transform.position.z);
                
                // Initialize the random heading deterministically from randomSeed
                _randomHeadingAngle = Mathf.PerlinNoise(randomSeed * 0.1f, 0f) * Mathf.PI * 2f;
                
                // Initialize normalized horizontal vector
                _randomCurrentDirection = new Vector2(Mathf.Cos(_randomHeadingAngle), Mathf.Sin(_randomHeadingAngle)).normalized;
                
                _randomSteeringTime = 0f;
                _randomInitialized = true;
                
                Debug.Log($"[UAVTrajectoryController] Random initialized\n" +
                          $"PositionXZ: {_randomCurrentPosXZ}\n" +
                          $"DirectionXZ: {_randomCurrentDirection}\n" +
                          $"Speed: {randomSpeed}");
            }
            
            // Generate smooth deterministic random heading changes
            _randomSteeringTime += Mathf.Min(Time.deltaTime, 0.05f) * 0.2f;
            
            // Continuous deterministic noise sampling to slowly alter heading
            float noiseVal = Mathf.PerlinNoise(_randomSteeringTime, randomSeed * 123.456f);
            
            // Use noise to generate a desired angle and smoothly steer towards it
            float desiredAngle = noiseVal * Mathf.PI * 4f; 
            Vector2 desiredDirection = new Vector2(Mathf.Cos(desiredAngle), Mathf.Sin(desiredAngle)).normalized;
            
            // Boundary handling
            float minX = orbitCenter.x - randomBounds.x * 0.5f;
            float maxX = orbitCenter.x + randomBounds.x * 0.5f;
            float minZ = orbitCenter.z - randomBounds.y * 0.5f;
            float maxZ = orbitCenter.z + randomBounds.y * 0.5f;
            
            // Margin where steering back begins (20% of bounds)
            float marginX = randomBounds.x * 0.2f;
            float marginZ = randomBounds.y * 0.2f;
            
            Vector2 boundaryForce = Vector2.zero;
            
            if (_randomCurrentPosXZ.x < minX + marginX) 
                boundaryForce.x += (minX + marginX - _randomCurrentPosXZ.x) / marginX;
            if (_randomCurrentPosXZ.x > maxX - marginX) 
                boundaryForce.x -= (_randomCurrentPosXZ.x - (maxX - marginX)) / marginX;
                
            if (_randomCurrentPosXZ.y < minZ + marginZ) 
                boundaryForce.y += (minZ + marginZ - _randomCurrentPosXZ.y) / marginZ;
            if (_randomCurrentPosXZ.y > maxZ - marginZ) 
                boundaryForce.y -= (_randomCurrentPosXZ.y - (maxZ - marginZ)) / marginZ;
                
            if (boundaryForce != Vector2.zero)
            {
                // Smoothly steer toward interior when approaching boundary
                desiredDirection = (desiredDirection + boundaryForce * 1.5f).normalized;
            }
            
            // Smoothly interpolate current direction toward desired direction
            _randomCurrentDirection = Vector2.Lerp(_randomCurrentDirection, desiredDirection, Mathf.Min(Time.deltaTime, 0.05f) * 1.5f).normalized;
            
            // Velocity integration
            // displacement = direction * randomSpeed * Mathf.Min(Time.deltaTime, 0.05f)
            Vector2 displacement = _randomCurrentDirection * randomSpeed * Mathf.Min(Time.deltaTime, 0.05f);
            _randomCurrentPosXZ += displacement;
            
            // Hard clamp safety guard so numerical error can never place the UAV outside the bounds
            _randomCurrentPosXZ.x = Mathf.Clamp(_randomCurrentPosXZ.x, minX, maxX);
            _randomCurrentPosXZ.y = Mathf.Clamp(_randomCurrentPosXZ.y, minZ, maxZ);
            
            Vector3 targetPosition = new Vector3(_randomCurrentPosXZ.x, orbitCenter.y + altitude, _randomCurrentPosXZ.y);
            Vector3 newPosition = targetPosition;
            
            // Smooth transition from previous location to prevent teleportation
            if (_modeTransitionProgress < 1f)
            {
                _modeTransitionProgress += Mathf.Min(Time.deltaTime, 0.05f) / 1.5f; 
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_modeTransitionProgress));
                newPosition = Vector3.Lerp(_transitionStartPosition, targetPosition, blend);
            }
            
            Vector3 velocity = Vector3.zero;
            if (Mathf.Min(Time.deltaTime, 0.05f) > 0.0001f)
            {
                velocity = (newPosition - transform.position) / Mathf.Min(Time.deltaTime, 0.05f);
            }
            
            _currentTargetSpeed = velocity.magnitude;
            
            if (faceMovementDirection)
            {
                if (velocity.sqrMagnitude > 0.001f)
                {
                    transform.forward = Vector3.Slerp(transform.forward, velocity.normalized, Mathf.Min(Time.deltaTime, 0.05f) * 3f);
                }
            }
            
            transform.position = newPosition;
        }
        else
        {
            _testActive = false;
        }

        // Update runtime position diagnostic
        _currentTargetPosition = transform.position;
    }

    public void ResetTrajectoryTiming()
    {
        _normalizedTime = 0f;
        _modeTransitionProgress = 0f;
        currentAngle = -90f;
        _transitionStartPosition = transform.position;
        if (currentMode == TrajectoryMode.Random) 
        {
            _randomInitialized = false;
        }
    }
}

