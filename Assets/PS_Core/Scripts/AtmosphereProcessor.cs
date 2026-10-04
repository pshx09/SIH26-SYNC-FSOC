using UnityEngine;

namespace FSC.Core
{
    public enum AtmosphereMode
    {
        Clear,
        Haze,
        Fog,
        Rain,
        LowLight
    }

    /// <summary>
    /// Simulates environmental visibility degradation in the sensor image pipeline.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class AtmosphereProcessor : MonoBehaviour
    {
        [Header("Atmosphere Control")]
        public bool enableAtmosphere = true;
        public AtmosphereMode atmosphereMode = AtmosphereMode.Clear;

        [Header("Atmosphere Settings")]
        [Range(0f, 1f)] public float intensity = 0.5f;
        [Range(0f, 2f)] public float contrast = 1f;
        [Range(0f, 2f)] public float brightness = 1f;
        
        [Range(0f, 1f)] public float hazeAmount = 0.5f;
        [Range(0f, 1f)] public float fogAmount = 0.5f;
        [Range(0f, 1f)] public float rainAmount = 0.5f;
        
        public float randomSeed = 100f;

        [Header("Pipeline Connections")]
        [Tooltip("The source providing the clean monochrome frame. Must be assigned, or will fallback to searching the scene.")]
        public SensorProcessor sourceSensorProcessor;
        
        [Tooltip("The next stage in the pipeline that will consume this atmosphere output.")]
        public DisturbanceProcessor targetDisturbanceProcessor;

        [Header("Diagnostics (Read Only)")]
        [SerializeField] private bool _pipelineReady = false;
        [SerializeField] private AtmosphereMode _currentMode = AtmosphereMode.Clear;
        [SerializeField] private float _currentIntensity = 0f;
        [SerializeField] private Vector2Int _inputResolution;
        [SerializeField] private Vector2Int _outputResolution;

        private Material _atmosphereMaterial;
        private RenderTexture _atmosphereTexture;
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
            // 1. Unwire explicit connection BEFORE destroying the texture it references
            if (targetDisturbanceProcessor != null && targetDisturbanceProcessor.inputTextureOverride == _atmosphereTexture)
            {
                targetDisturbanceProcessor.inputTextureOverride = null;
            }

            // 2. Safe cleanup of owned resources
            if (_atmosphereTexture != null)
            {
                if (Application.isPlaying) Destroy(_atmosphereTexture);
                else DestroyImmediate(_atmosphereTexture);
                _atmosphereTexture = null;
            }

            if (_atmosphereMaterial != null)
            {
                if (Application.isPlaying) Destroy(_atmosphereMaterial);
                else DestroyImmediate(_atmosphereMaterial);
                _atmosphereMaterial = null;
            }

            _pipelineReady = false;
            _isInitialized = false;
        }

        private void InitializePipeline()
        {
            // Authoritative Inspector references first, fallback to search only if null
            if (sourceSensorProcessor == null) sourceSensorProcessor = FindFirstObjectByType<SensorProcessor>();
            if (targetDisturbanceProcessor == null) targetDisturbanceProcessor = FindFirstObjectByType<DisturbanceProcessor>();

            if (sourceSensorProcessor == null || sourceSensorProcessor.processedTexture == null) return;
            if (targetDisturbanceProcessor == null) return;

            // Dynamically allocate based on actual source
            int width = sourceSensorProcessor.processedTexture.width;
            int height = sourceSensorProcessor.processedTexture.height;
            
            _atmosphereTexture = new RenderTexture(width, height, 0, sourceSensorProcessor.processedTexture.format)
            {
                name = "FSOC_Atmosphere_RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _atmosphereTexture.Create();

            _inputResolution = new Vector2Int(width, height);
            _outputResolution = new Vector2Int(width, height);

            Shader shader = Shader.Find("Hidden/FSOC/AtmosphereEffects");
            if (shader != null)
            {
                _atmosphereMaterial = new Material(shader);
            }
            else
            {
                Debug.LogError("[AtmosphereProcessor] Failed to find Hidden/FSOC/AtmosphereEffects shader.");
                return;
            }

            // Contract: explicitly wire to the next stage
            targetDisturbanceProcessor.inputTextureOverride = _atmosphereTexture;
            
            _pipelineReady = true;
            _isInitialized = true;
            
            Debug.Log($"[AtmosphereProcessor] Initialized. Pipeline: SensorProcessor ({width}x{height}) -> AtmosphereProcessor -> DisturbanceProcessor.");
        }

        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                InitializePipeline();
                // Do not return here. Process the very first frame immediately to prevent uninitialized texture consumption.
                if (!_isInitialized) return; 
            }

            if (sourceSensorProcessor == null || sourceSensorProcessor.processedTexture == null || _atmosphereTexture == null)
            {
                _pipelineReady = false;
                return;
            }

            // Dynamic resolution tracking
            if (sourceSensorProcessor.processedTexture.width != _atmosphereTexture.width ||
                sourceSensorProcessor.processedTexture.height != _atmosphereTexture.height)
            {
                CleanupResources();
                return; // Re-initialize next frame
            }

            // Maintain the override contract safely
            if (targetDisturbanceProcessor != null && targetDisturbanceProcessor.inputTextureOverride != _atmosphereTexture)
            {
                targetDisturbanceProcessor.inputTextureOverride = _atmosphereTexture;
            }

            // True GPU pass-through to ensure no stale frame remains
            if (!enableAtmosphere || atmosphereMode == AtmosphereMode.Clear || _atmosphereMaterial == null)
            {
                _currentMode = AtmosphereMode.Clear;
                _currentIntensity = 0f;
                Graphics.Blit(sourceSensorProcessor.processedTexture, _atmosphereTexture);
                return;
            }

            _currentMode = atmosphereMode;
            _currentIntensity = intensity;

            _atmosphereMaterial.SetInt("_AtmosphereMode", (int)atmosphereMode);
            _atmosphereMaterial.SetFloat("_Intensity", intensity);
            _atmosphereMaterial.SetFloat("_Contrast", contrast);
            _atmosphereMaterial.SetFloat("_Brightness", brightness);
            _atmosphereMaterial.SetFloat("_HazeAmount", hazeAmount);
            _atmosphereMaterial.SetFloat("_FogAmount", fogAmount);
            _atmosphereMaterial.SetFloat("_RainAmount", rainAmount);
            
            // Deterministic temporal component
            _atmosphereMaterial.SetFloat("_TimeSeed", Time.time + randomSeed);

            Graphics.Blit(sourceSensorProcessor.processedTexture, _atmosphereTexture, _atmosphereMaterial);
        }
    }
}
