using UnityEngine;
using FSOC.Contracts;

namespace FSOC.AI
{
    public class ClassicalDetectorAdapter : MonoBehaviour, IBeaconDetector
    {
        [Tooltip("Assign the existing Classical BeaconDetector here.")]
        public BeaconDetector classicalDetector;

        public DetectionResult ProcessFrame(SensorFrame frame)
        {
            // Minimal adapter since we cannot modify the internal classical math
            // and it uses its own update loop currently. 
            // This allows routing to consume it uniformly later.
            
            DetectionResult result = new DetectionResult
            {
                IsDetected = false,
                Type = DetectorType.Classical,
                Timestamp = frame.Timestamp
            };
            
            if (classicalDetector != null)
            {
                // In a full implementation this would read classicalDetector properties.
                // For now, this is a placeholder adapter.
            }
            
            return result;
        }
    }
}
