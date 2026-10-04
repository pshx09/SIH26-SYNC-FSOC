using UnityEngine;

namespace FSOC.Contracts
{
    public enum DetectorType
    {
        Unknown,
        Classical,
        AI,
        Hybrid
    }

    public struct DetectionResult
    {
        public bool IsDetected;
        public Vector2 Centroid;
        public Rect BoundingBox;
        public float Confidence;
        public int TargetID;
        public float Timestamp;
        public DetectorType Type;
        public float ProcessingTimeMs;
    }
}
