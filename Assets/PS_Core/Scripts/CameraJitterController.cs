using UnityEngine;

namespace FSC.Core
{
    public enum JitterMode
    {
        None,
        Random,
        Sinusoidal
    }

    /// <summary>
    /// Simulates high-frequency camera pointing jitter (e.g., from engine vibrations or wind).
    /// Operates on the VirtualCamera transform directly to completely isolate the disturbance
    /// from the PanTiltTracker's closed-loop control system.
    /// </summary>
    [DefaultExecutionOrder(100)] // Ensures this runs after the PanTiltTracker evaluates its movement.
    public class CameraJitterController : MonoBehaviour
    {
        [Header("Camera Jitter Control")]
        [Tooltip("Master toggle for camera jitter.")]
        public bool enableCameraJitter = true;

        [Tooltip("The mathematical model used to generate jitter.")]
        public JitterMode jitterMode = JitterMode.None;

        [Header("Jitter Settings")]
        [Tooltip("Maximum jitter pointing change per frame (Pixels/Frame).")]
        [Range(0f, 20f)]
        public float maxJitterXPixels = 0f;

        [Tooltip("Maximum jitter pointing change per frame (Pixels/Frame).")]
        [Range(0f, 20f)]
        public float maxJitterYPixels = 0f;

        [Tooltip("Frequency of the jitter in Hz (Used for Sinusoidal and Random target tracking).")]
        public float jitterFrequencyHz = 10f;

        [Tooltip("Seed for the random number generator to ensure repeatable tests.")]
        public int randomSeed = 42;

        public float CurrentJitterMagnitude => Mathf.Max(maxJitterXPixels, maxJitterYPixels);

        [Header("Dependencies")]
        [Tooltip("The optical camera that will receive the physical pointing disturbance.")]
        public Camera virtualCamera;

        [Header("Diagnostics (Read Only)")]
        [SerializeField] private float currentJitterDeltaXPixels = 0f;
        [SerializeField] private float currentJitterDeltaYPixels = 0f;
        [SerializeField] private float currentJitterDeltaXDegrees = 0f;
        [SerializeField] private float currentJitterDeltaYDegrees = 0f;

        // Constants derived from PS constraints
        // Resolution: 640x480
        // FOV: 4 deg H x 3 deg V
        // 4 deg / 640 px = 0.00625 deg/px
        private const float DEGREES_PER_PIXEL = 0.00625f;
        private const float MAX_JITTER_PIXELS = 20f;

        private Quaternion _nominalLocalRotation;
        private bool _isInitialized = false;

        private void Start()
        {
            InitializeController();
        }

        private void InitializeController()
        {
            if (virtualCamera == null)
            {
                // Attempt to auto-find the VirtualCamera.
                virtualCamera = GetComponentInChildren<Camera>();
                if (virtualCamera == null)
                {
                    virtualCamera = FindFirstObjectByType<Camera>();
                }
            }

            if (virtualCamera != null)
            {
                _nominalLocalRotation = virtualCamera.transform.localRotation;
                _isInitialized = true;
            }
            else
            {
                Debug.LogError("[CameraJitterController] Could not find a VirtualCamera in the scene. Jitter will not run.");
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized || virtualCamera == null)
                return;

            if (!enableCameraJitter || jitterMode == JitterMode.None)
            {
                // Restore clean nominal pointing
                virtualCamera.transform.localRotation = _nominalLocalRotation;
                ClearDiagnostics();
                return;
            }

            float time = Time.time;
            float targetX = 0f;
            float targetY = 0f;

            // 1. Generate the underlying continuous offset target based on the mathematical model.
            // We scale this to the absolute maximum allowed PS limit to utilize the full required range.
            if (jitterMode == JitterMode.Random)
            {
                // Use PerlinNoise as a continuous, deterministic random target.
                // Add a decimal offset to the seed to avoid integer grid artifacts in Perlin evaluation.
                float seedOffset = randomSeed + 0.1f;
                targetX = (Mathf.PerlinNoise(time * jitterFrequencyHz, seedOffset) * 2f - 1f) * MAX_JITTER_PIXELS;
                targetY = (Mathf.PerlinNoise(seedOffset, time * jitterFrequencyHz) * 2f - 1f) * MAX_JITTER_PIXELS;
            }
            else if (jitterMode == JitterMode.Sinusoidal)
            {
                targetX = Mathf.Sin(time * jitterFrequencyHz * 2f * Mathf.PI) * MAX_JITTER_PIXELS;
                targetY = Mathf.Sin((time * jitterFrequencyHz * 2f * Mathf.PI) + (Mathf.PI / 4f)) * MAX_JITTER_PIXELS;
            }

            // 2. Ensure the maximum change in pointing between frames corresponds strictly to the configured limit.
            float requiredStepX = targetX - currentJitterDeltaXPixels;
            float requiredStepY = targetY - currentJitterDeltaYPixels;

            float stepX = Mathf.Clamp(requiredStepX, -maxJitterXPixels, maxJitterXPixels);
            float stepY = Mathf.Clamp(requiredStepY, -maxJitterYPixels, maxJitterYPixels);

            // 3. Accumulate the allowed step to determine this frame's actual applied pixel delta.
            currentJitterDeltaXPixels += stepX;
            currentJitterDeltaYPixels += stepY;

            // 4. Strictly clamp the actual per-frame jitter delta to the absolute PS limits (-20 <= delta <= 20).
            currentJitterDeltaXPixels = Mathf.Clamp(currentJitterDeltaXPixels, -MAX_JITTER_PIXELS, MAX_JITTER_PIXELS);
            currentJitterDeltaYPixels = Mathf.Clamp(currentJitterDeltaYPixels, -MAX_JITTER_PIXELS, MAX_JITTER_PIXELS);

            // 5. Convert that per-frame pixel delta to angular delta using PS geometry.
            currentJitterDeltaXDegrees = currentJitterDeltaXPixels * DEGREES_PER_PIXEL;
            currentJitterDeltaYDegrees = currentJitterDeltaYPixels * DEGREES_PER_PIXEL;

            // 6. Apply the resulting angular DELTA to the nominal commanded camera orientation.
            Quaternion jitterOffset = Quaternion.Euler(-currentJitterDeltaYDegrees, currentJitterDeltaXDegrees, 0f);
            
            // Reapply from the pristine nominal rotation every frame. 
            // This guarantees we never accumulate error into the base orientation.
            virtualCamera.transform.localRotation = _nominalLocalRotation * jitterOffset;
        }

        private void ClearDiagnostics()
        {
            currentJitterDeltaXPixels = 0f;
            currentJitterDeltaYPixels = 0f;
            currentJitterDeltaXDegrees = 0f;
            currentJitterDeltaYDegrees = 0f;
        }
    }
}
