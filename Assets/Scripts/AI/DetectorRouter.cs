using UnityEngine;
using FSOC.Contracts;

namespace FSOC.AI
{
    public class DetectorRouter : MonoBehaviour, IBeaconDetector
    {
        [Header("Routing Configuration")]
        [Tooltip("The primary AI detector.")]
        public AIDetector primaryAIDetector;
        
        [Tooltip("The fallback classical detector adapter.")]
        public MonoBehaviour fallbackClassicalDetector; 
        
        [Tooltip("Allow falling back to Classical if AI fails or is unavailable?")]
        public bool fallbackEnabled = true;

        private IBeaconDetector _fallbackInterface;

        private void Awake()
        {
            if (fallbackClassicalDetector != null)
            {
                _fallbackInterface = fallbackClassicalDetector as IBeaconDetector;
                if (_fallbackInterface == null)
                {
                    Debug.LogError("[DetectorRouter] Assigned fallback Classical detector does not implement IBeaconDetector!");
                }
            }
        }

        public DetectionResult ProcessFrame(SensorFrame frame)
        {
            if (primaryAIDetector != null)
            {
                AIStatus aiStatus = primaryAIDetector.GetStatus();
                
                if (aiStatus != AIStatus.ModelUnavailable && aiStatus != AIStatus.Error)
                {
                    return primaryAIDetector.ProcessFrame(frame);
                }
            }

            // --- FALLBACK PATH ---
            if (fallbackEnabled && _fallbackInterface != null)
            {
                return _fallbackInterface.ProcessFrame(frame);
            }

            // Total failure
            return new DetectionResult 
            { 
                IsDetected = false, 
                Timestamp = frame.Timestamp, 
                Type = DetectorType.Unknown 
            };
        }
    }
}
