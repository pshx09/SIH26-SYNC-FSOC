using UnityEngine;

namespace FSC.Core
{
    public enum PlatformMotionMode
    {
        None,
        Linear,
        Circular,
        Random
    }

    /// <summary>
    /// Simulates physical TRANSLATIONAL motion of the mounting platform (e.g., GroundStation_Base).
    /// Enforces PS pixel-per-frame limits by mathematically mapping physical translation 
    /// back to image-space displacement based on the camera FOV and target depth.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlatformMotionController : MonoBehaviour
    {
        [Header("Platform Motion Control")]
        public bool enablePlatformMotion = true;
        public PlatformMotionMode motionMode = PlatformMotionMode.Linear;

        [Header("Motion Settings")]
        [Tooltip("Direction vector for Linear mode (will be normalized).")]
        public Vector3 linearDirection = new Vector3(1f, 0f, 0f);
        
        [Tooltip("Platform movement speed (Meters per second).")]
        public float platformSpeed = 2f;
        
        [Tooltip("Maximum physical distance in meters from the nominal position before reversing direction.")]
        public float maxMotionRange = 5f;

        public int randomSeed = 100;

        [Header("PS Limits (Pixels/Frame)")]
        [Range(0f, 20f)]
        public float maxPlatformMotionXPixels = 20f;
        [Range(0f, 20f)]
        public float maxPlatformMotionYPixels = 20f;
        
        public float CurrentPlatformMotionMagnitude => Mathf.Max(maxPlatformMotionXPixels, maxPlatformMotionYPixels);

        [Header("Optics & Geometry")]
        [Tooltip("Representative distance to the target in meters, used to calculate image-space pixel shift.")]
        public float targetDepthMeters = 200f;

        [Header("Dependencies")]
        [Tooltip("The physical platform mount (e.g., GroundStation_Base).")]
        public Transform platformTransform;
        
        [Tooltip("The optical camera used to determine local projection axes.")]
        public Camera virtualCamera;

        [Header("Diagnostics (Read Only)")]
        [SerializeField] private float currentPlatformDeltaX = 0f;
        [SerializeField] private float currentPlatformDeltaY = 0f;
        [SerializeField] private float currentPlatformDeltaZ = 0f;
        [SerializeField] private float currentImageMotionXPixels = 0f;
        [SerializeField] private float currentImageMotionYPixels = 0f;

        // Constants derived from PS constraints
        // Resolution: 640x480, FOV: 4 deg H x 3 deg V
        private const float DEGREES_PER_PIXEL = 0.00625f;

        private Vector3 _nominalLocalPosition;
        private Vector3 _currentPositionalOffset = Vector3.zero;
        private float _currentLinearDirectionSign = 1f;
        private float _phaseTime = 0f;
        private bool _isInitialized = false;
        private PlatformMotionMode _previousMotionMode = PlatformMotionMode.None;

        private void Start()
        {
            InitializeController();
        }

        private void InitializeController()
        {
            if (platformTransform == null)
            {
                // The actual platform mount is typically the root object or the direct parent of PanBase.
                PanTiltTracker ptt = FindFirstObjectByType<PanTiltTracker>();
                if (ptt != null && ptt.transform.parent != null)
                {
                    platformTransform = ptt.transform.parent; // GroundStation_Base
                }
            }

            if (virtualCamera == null)
            {
                virtualCamera = GetComponentInChildren<Camera>();
                if (virtualCamera == null)
                {
                    virtualCamera = FindFirstObjectByType<Camera>();
                }
            }

            if (platformTransform != null)
            {
                _nominalLocalPosition = platformTransform.localPosition;
                _isInitialized = true;
            }
            else
            {
                Debug.LogError("[PlatformMotionController] Could not find a Platform Transform in the scene.");
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized || platformTransform == null || virtualCamera == null)
                return;

            if (!enablePlatformMotion || motionMode == PlatformMotionMode.None)
            {
                ResetMotionState();
                _previousMotionMode = motionMode;
                return;
            }

            if (motionMode != _previousMotionMode)
            {
                ResetMotionState();
                _previousMotionMode = motionMode;
            }

            float dt = Time.deltaTime;
            Vector3 desiredStepLocal = Vector3.zero;

            // 1. Calculate the intended physical step in LOCAL space
            if (motionMode == PlatformMotionMode.Linear)
            {
                desiredStepLocal = linearDirection.normalized * (platformSpeed * _currentLinearDirectionSign * dt);

                Vector3 projectedOffset = _currentPositionalOffset + desiredStepLocal;
                if (projectedOffset.magnitude > maxMotionRange)
                {
                    // Clamp movement exactly to the boundary without overshooting
                    float overshoot = projectedOffset.magnitude - maxMotionRange;
                    desiredStepLocal = desiredStepLocal.normalized * (desiredStepLocal.magnitude - overshoot);
                    
                    // Reverse direction for the next frame
                    _currentLinearDirectionSign *= -1f;
                }
            }
            else if (motionMode == PlatformMotionMode.Circular)
            {
                _phaseTime += dt * platformSpeed;
                Vector3 targetOffset = (Vector3.right * Mathf.Cos(_phaseTime) + Vector3.forward * Mathf.Sin(_phaseTime)) * maxMotionRange;
                desiredStepLocal = targetOffset - _currentPositionalOffset;
            }
            else if (motionMode == PlatformMotionMode.Random)
            {
                _phaseTime += dt * platformSpeed;
                float seedOffset = randomSeed + 0.1f;
                float tx = (Mathf.PerlinNoise(_phaseTime, seedOffset) * 2f - 1f) * maxMotionRange;
                float ty = (Mathf.PerlinNoise(seedOffset, _phaseTime) * 2f - 1f) * maxMotionRange;
                float tz = (Mathf.PerlinNoise(_phaseTime, seedOffset + 10f) * 2f - 1f) * maxMotionRange;
                
                Vector3 targetOffset = new Vector3(tx, ty, tz);
                desiredStepLocal = targetOffset - _currentPositionalOffset;
            }

            // 2. Convert desired local step to WORLD space before camera projection
            Vector3 desiredStepWorld = platformTransform.TransformDirection(desiredStepLocal);

            // 3. Project the world-space step into the camera's local viewing plane
            float displacementRight = Vector3.Dot(virtualCamera.transform.right, desiredStepWorld);
            float displacementUp = Vector3.Dot(virtualCamera.transform.up, desiredStepWorld);

            float degreesX = (displacementRight / targetDepthMeters) * Mathf.Rad2Deg;
            float degreesY = (displacementUp / targetDepthMeters) * Mathf.Rad2Deg;

            float pixelShiftX = degreesX / DEGREES_PER_PIXEL;
            float pixelShiftY = degreesY / DEGREES_PER_PIXEL;

            // 4. Compare image-space shift against strict PS limits and handle zero limits safely
            float scaleFactor = 1f;

            if (maxPlatformMotionXPixels <= 0f)
            {
                if (Mathf.Abs(pixelShiftX) > 0.001f) scaleFactor = 0f;
            }
            else if (Mathf.Abs(pixelShiftX) > maxPlatformMotionXPixels)
            {
                scaleFactor = Mathf.Min(scaleFactor, maxPlatformMotionXPixels / Mathf.Abs(pixelShiftX));
            }
            
            if (maxPlatformMotionYPixels <= 0f)
            {
                if (Mathf.Abs(pixelShiftY) > 0.001f) scaleFactor = 0f;
            }
            else if (Mathf.Abs(pixelShiftY) > maxPlatformMotionYPixels)
            {
                scaleFactor = Mathf.Min(scaleFactor, maxPlatformMotionYPixels / Mathf.Abs(pixelShiftY));
            }

            // 5. Scale the LOCAL step down if it violates pixel-per-frame limits
            desiredStepLocal *= scaleFactor;
            
            currentImageMotionXPixels = pixelShiftX * scaleFactor;
            currentImageMotionYPixels = pixelShiftY * scaleFactor;

            // 6. Accumulate local offset and apply
            _currentPositionalOffset += desiredStepLocal;
            platformTransform.localPosition = _nominalLocalPosition + _currentPositionalOffset;

            // Update Diagnostics
            currentPlatformDeltaX = desiredStepLocal.x;
            currentPlatformDeltaY = desiredStepLocal.y;
            currentPlatformDeltaZ = desiredStepLocal.z;
        }

        private void ResetMotionState()
        {
            if (platformTransform != null)
            {
                platformTransform.localPosition = _nominalLocalPosition;
            }
            _currentPositionalOffset = Vector3.zero;
            _currentLinearDirectionSign = 1f;
            _phaseTime = 0f;
            ClearDiagnostics();
        }

        private void ClearDiagnostics()
        {
            currentPlatformDeltaX = 0f;
            currentPlatformDeltaY = 0f;
            currentPlatformDeltaZ = 0f;
            currentImageMotionXPixels = 0f;
            currentImageMotionYPixels = 0f;
        }
    }
}
