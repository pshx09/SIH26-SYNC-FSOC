using UnityEngine;

namespace FSC.Core
{
    public enum DisturbanceType
    {
        None,
        Gaussian,
        SaltAndPepper,
        Poisson
    }

    [DefaultExecutionOrder(20)] // Enforces: SensorProcessor (0) -> Atmosphere (10) -> Disturbance (20)
    public class DisturbanceProcessor : MonoBehaviour
    {
        [Header("Disturbance Control")]
        [Tooltip("Master toggle for all disturbances. If false, acts as a clean passthrough.")]
        public bool enableDisturbances = true;

        [Tooltip("The type of sensor noise to simulate.")]
        public DisturbanceType disturbanceType = DisturbanceType.None;

        [Header("Gaussian Settings")]
        [Tooltip("Standard deviation (strength) of the Gaussian noise. Range 0 to 20.")]
        [Range(0f, 20f)]
        public float gaussianStandardDeviation = 0f;

        [Header("Salt & Pepper Settings")]
        [Tooltip("Probability of a pixel becoming black or white. Range 0.0 to 1.0 (e.g. 0.05 = 5%).")]
        [Range(0f, 1f)]
        public float saltAndPepperIntensity = 0f;

        [Header("Poisson Settings")]
        [Tooltip("Strength multiplier for Poisson shot noise. Higher value = more noise.")]
        [Range(0f, 10f)]
        public float poissonStrength = 1f;

        [Header("Pipeline Connections")]
        [Tooltip("The source providing the clean monochrome frame (Fallback if Override is null).")]
        public SensorProcessor sensorProcessor;
        
        [Tooltip("The target detector that will consume the disturbed output.")]
        public BeaconDetector beaconDetector;
        
        [Tooltip("Optional. If assigned by an upstream processor (e.g. Atmosphere), overrides the SensorProcessor's output as the input to this stage.")]
        public RenderTexture inputTextureOverride;

        [Header("Diagnostics (Read-Only)")]
        [SerializeField] private string _activeInputSource = "None";
        [SerializeField] private DisturbanceType _currentDisturbanceType = DisturbanceType.None;
        [SerializeField] private float _currentStrength = 0f;
        [SerializeField] private Vector2Int _inputResolution = Vector2Int.zero;
        [SerializeField] private Vector2Int _outputResolution = Vector2Int.zero;

        // Dedicated output texture
        private RenderTexture _disturbedTexture;
        private Material _noiseMaterial;
        
        private bool _isInitialized = false;

        private void OnEnable()
        {
            _isInitialized = false;
        }

        private void OnDisable()
        {
            CleanupResources();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        private void CleanupResources()
        {
            // Unwire downstream explicitly before destroying the texture
            if (beaconDetector != null && beaconDetector.sensorTexture == _disturbedTexture)
            {
                beaconDetector.sensorTexture = null;
            }

            if (_disturbedTexture != null)
            {
                if (Application.isPlaying) Destroy(_disturbedTexture);
                else DestroyImmediate(_disturbedTexture);
                _disturbedTexture = null;
            }

            if (_noiseMaterial != null)
            {
                if (Application.isPlaying) Destroy(_noiseMaterial);
                else DestroyImmediate(_noiseMaterial);
                _noiseMaterial = null;
            }

            _activeInputSource = "None";
            _isInitialized = false;
        }

        private void InitializePipeline()
        {
            if (sensorProcessor == null) sensorProcessor = FindFirstObjectByType<SensorProcessor>();
            if (beaconDetector == null) beaconDetector = FindFirstObjectByType<BeaconDetector>();

            if (sensorProcessor == null || beaconDetector == null) return;
            
            Texture inputTex = inputTextureOverride != null ? inputTextureOverride : (sensorProcessor != null ? sensorProcessor.processedTexture : null);
            if (inputTex == null) return;

            // 1. Create the dedicated output texture for disturbed frames dynamically sized
            int width = inputTex.width;
            int height = inputTex.height;

            _disturbedTexture = new RenderTexture(width, height, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm) // Fallback format safely
            {
                name = "FSOC_Disturbed_RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (inputTex is RenderTexture rt)
            {
                _disturbedTexture.format = rt.format;
            }
            _disturbedTexture.Create();

            _inputResolution = new Vector2Int(width, height);
            _outputResolution = new Vector2Int(width, height);

            // 2. Setup the GPU Material
            Shader shader = Shader.Find("Hidden/FSOC/SensorNoise");
            if (shader != null)
            {
                _noiseMaterial = new Material(shader);
            }
            else
            {
                Debug.LogError("[DisturbanceProcessor] Failed to find Hidden/FSOC/SensorNoise shader.");
                return;
            }

            // 3. Explicitly wire pipeline downstream
            beaconDetector.sensorTexture = _disturbedTexture;
            _activeInputSource = inputTextureOverride != null ? inputTextureOverride.name : sensorProcessor.processedTexture.name;
            
            _isInitialized = true;
            Debug.Log($"[DisturbanceProcessor] Initialized. Pipeline: {_activeInputSource} ({width}x{height}) -> DisturbanceProcessor -> BeaconDetector.");
        }

        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                InitializePipeline();
                if (!_isInitialized) return; // Process immediately if init succeeded
            }

            Texture inputTex = inputTextureOverride != null ? inputTextureOverride : (sensorProcessor != null ? sensorProcessor.processedTexture : null);

            if (inputTex == null || _disturbedTexture == null)
                return;

            // Handle resolution changes from upstream dynamically
            if (inputTex.width != _disturbedTexture.width || inputTex.height != _disturbedTexture.height)
            {
                CleanupResources();
                return;
            }

            // Maintain downstream contract explicitly
            if (beaconDetector != null && beaconDetector.sensorTexture != _disturbedTexture)
            {
                beaconDetector.sensorTexture = _disturbedTexture;
            }
            
            _activeInputSource = inputTextureOverride != null ? inputTextureOverride.name : (sensorProcessor.processedTexture != null ? sensorProcessor.processedTexture.name : "None");

            if (!enableDisturbances || disturbanceType == DisturbanceType.None || _noiseMaterial == null)
            {
                _currentDisturbanceType = DisturbanceType.None;
                _currentStrength = 0f;
                // True hardware passthrough
                Graphics.Blit(inputTex, _disturbedTexture);
                return;
            }

            _currentDisturbanceType = disturbanceType;
            _noiseMaterial.SetInt("_NoiseType", (int)disturbanceType);
            _noiseMaterial.SetFloat("_TimeSeed", Time.time % 1000f);

            if (disturbanceType == DisturbanceType.Gaussian)
            {
                float maxSigma = 20f;
                gaussianStandardDeviation = Mathf.Clamp(gaussianStandardDeviation, 0f, maxSigma);
                _currentStrength = gaussianStandardDeviation;
                _noiseMaterial.SetFloat("_NoiseStrength", _currentStrength);
            }
            else if (disturbanceType == DisturbanceType.SaltAndPepper)
            {
                _currentStrength = saltAndPepperIntensity;
                _noiseMaterial.SetFloat("_NoiseStrength", _currentStrength);
            }
            else if (disturbanceType == DisturbanceType.Poisson)
            {
                _currentStrength = poissonStrength;
                _noiseMaterial.SetFloat("_NoiseStrength", _currentStrength);
            }

            // Apply fast GPU noise
            Graphics.Blit(inputTex, _disturbedTexture, _noiseMaterial);
        }
    }
}
