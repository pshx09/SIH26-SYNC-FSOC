using UnityEngine;

namespace FSOC.AI
{
    public struct InferenceOutput
    {
        public bool IsDetected;
        public Vector2 Centroid;
        public Rect BoundingBox;
        public float Confidence;
        public int TargetID;
    }

    public interface IAIInferenceBackend
    {
        bool IsInitialized { get; }
        bool Initialize();
        InferenceOutput Run(Texture image);
        void Dispose();
    }
}
