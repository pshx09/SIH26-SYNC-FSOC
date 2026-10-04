using UnityEngine;

namespace FSOC.Contracts
{
    public enum InputMode
    {
        VirtualSensor,
        MP4
    }

    public struct SensorFrame
    {
        public Texture Image;
        public int Width;
        public int Height;
        public float Timestamp;
        public int FrameIndex;
        public float SourceFrameRate;
        public InputMode Mode;
    }
}
