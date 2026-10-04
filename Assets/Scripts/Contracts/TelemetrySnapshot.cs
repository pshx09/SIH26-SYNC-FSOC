using System;

namespace FSOC.Contracts
{
    public enum TrackingState
    {
        Searching,
        Acquiring,
        Locked,
        Predicting,
        Reacquiring,
        Lost
    }

    [Serializable]
    public struct TelemetrySnapshot
    {
        // Timestamp
        public float Timestamp;
        public int RunId;
        public string RunState;

        // Target/detection
        public DetectorType DetectorType;
        public bool IsDetected;
        public float DetectionConfidence;
        public float CentroidX;
        public float CentroidY;
        public float BoundingBoxWidth;
        public float BoundingBoxHeight;

        // Tracking
        public TrackingState TrackingState;
        public float ErrorX;
        public float ErrorY;
        public float RadialError;
        public float MeanError;
        public float RMSE;
        public float MaxError;
        public int FramesWithin10px;
        public float TargetLossRate;
        public float LockRetention;
        public float AcquisitionTime;
        public float ReacquisitionTime;

        // Performance
        public float FPS;
        public float FrameTimeMs;
        public float DetectorInferenceTimeMs;
        public float PTZUpdateRate;

        // PTZ
        public float PanAngle;
        public float TiltAngle;
        public float PanVelocity;
        public float TiltVelocity;
        public float MaxPanLimit;
        public float MinPanLimit;
        public float MaxTiltLimit;
        public float MinTiltLimit;

        // Scenario/input
        public InputMode InputMode;
        public int VideoFrame;
        public float VideoTime;
        public float VideoDuration;
        public float VideoFrameRate;
        public int VideoFrameCount;
        public bool VideoIsPlaying;
        public string TrajectoryScenario;
        public string Resolution;
        public float HFOV;
        public float VFOV;
        public int CameraFrameRate;
        public float BeaconSize;
        public string NoiseType;
        public float NoiseStrength;
        public string Atmosphere;
        public string CameraJitter;
        public string PlatformMotion;
        
        // Event Log
        public string[] RecentEvents;
        
        // Full authoritative configuration
        public SimulationConfigSnapshot ConfigSnapshot;
    }
}
