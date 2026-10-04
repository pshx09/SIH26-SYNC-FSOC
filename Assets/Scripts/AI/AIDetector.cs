using UnityEngine;
using FSOC.Contracts;
using System.Diagnostics;

namespace FSOC.AI
{
    public class AIDetector : MonoBehaviour, IBeaconDetector
    {
        [Header("AI Inference Configuration")]
        [Tooltip("The neural network model asset (ONNX format).")]
        public Object modelAsset; 
        
        [Tooltip("Expected input width for the model (e.g., 640).")]
        public int inputWidth = 640;
        
        [Tooltip("Expected input height for the model (e.g., 480).")]
        public int inputHeight = 480;
        
        [Tooltip("Minimum confidence threshold for a valid detection (0.0 to 1.0).")]
        [Range(0f, 1f)]
        public float confidenceThreshold = 0.5f;
        
        [Tooltip("Minimum bounding box size to filter out noise.")]
        public Vector2 expectedBeaconMinSize = new Vector2(5f, 5f);
        
        [Tooltip("Maximum bounding box size to filter out false positives.")]
        public Vector2 expectedBeaconMaxSize = new Vector2(100f, 100f);
        
        [Tooltip("Target inference frequency (Hz). 0 means process every frame.")]
        public float inferenceFrequency = 0f;
        
        [Tooltip("Execute on GPU if available.")]
        public bool preferGPU = true;

        [Header("Runtime Status (Read Only)")]
        [SerializeField] private AIStatus _currentStatus = AIStatus.Unknown;
        
        private IAIInferenceBackend _backend;
        private Stopwatch _stopwatch = new Stopwatch();

        private void Awake()
        {
            if (modelAsset == null)
            {
                _currentStatus = AIStatus.ModelUnavailable;
                UnityEngine.Debug.LogWarning("[AIDetector] No AI model asset assigned. Entering ModelUnavailable state.");
            }
            else
            {
                // To be implemented in Step 3 when model is available
                _currentStatus = AIStatus.ModelUnavailable; 
            }
        }

        public AIStatus GetStatus() => _currentStatus;

        public DetectionResult ProcessFrame(SensorFrame frame)
        {
            DetectionResult result = new DetectionResult
            {
                IsDetected = false,
                Timestamp = frame.Timestamp,
                Type = DetectorType.AI,
                TargetID = -1,
                Confidence = 0f,
                ProcessingTimeMs = 0f
            };

            if (_currentStatus == AIStatus.ModelUnavailable || _backend == null || !_backend.IsInitialized)
            {
                return result; 
            }

            _currentStatus = AIStatus.InferenceActive;
            
            _stopwatch.Restart();
            
            InferenceOutput output = _backend.Run(frame.Image);
            
            _stopwatch.Stop();
            result.ProcessingTimeMs = (float)_stopwatch.Elapsed.TotalMilliseconds;

            if (output.IsDetected && output.Confidence >= confidenceThreshold)
            {
                if (output.BoundingBox.width >= expectedBeaconMinSize.x && 
                    output.BoundingBox.height >= expectedBeaconMinSize.y &&
                    output.BoundingBox.width <= expectedBeaconMaxSize.x &&
                    output.BoundingBox.height <= expectedBeaconMaxSize.y)
                {
                    result.IsDetected = true;
                    result.Centroid = output.Centroid;
                    result.BoundingBox = output.BoundingBox;
                    result.Confidence = output.Confidence;
                    result.TargetID = output.TargetID;
                }
            }

            return result;
        }

        private void OnDestroy()
        {
            if (_backend != null)
            {
                _backend.Dispose();
                _backend = null;
            }
        }
    }
}
