using System;

namespace FSOC.Contracts
{
    public enum RunState
    {
        STOPPED,
        RUNNING,
        PAUSED,
        WAITING
    }

    [Serializable]
    public struct SimulationConfigSnapshot
    {
        public RunState runState;
        
        // Trajectory
        public string trajectoryMode;
        
        public float straightLineStartX;
        public float straightLineStartY;
        public float straightLineStartZ;
        public float straightLineDirX;
        public float straightLineDirY;
        public float straightLineDirZ;
        public float straightLineSpeed;
        public float straightLineRange;

        public float orbitCenterX;
        public float orbitCenterY;
        public float orbitCenterZ;
        public float orbitRadius;
        public float orbitAltitude;
        public float orbitAngularSpeed;
        public int orbitDirection;

        public float figure8AmplitudeX;
        public float figure8AmplitudeY;
        public float figure8Period;
        public float figure8MaxSpeed;

        public float randomBoundsX;
        public float randomBoundsY;
        public float randomSpeed;
        public int randomSeed;

        // Target
        public string targetType;
        public int targetCount;
        public string targetShape;
        public float targetSize;
        public float initialPositionX;
        public float initialPositionY;
        public float initialPositionZ;

        // Camera
        public string cameraResolution;
        public float hfov;
        public float vfov;
        public int cameraUpdateRate;

        // PTZ
        public float maxPanSpeed;
        public float maxTiltSpeed;
        public int ptzUpdateRate;

        // Atmosphere
        public bool atmosphereEnabled;
        public string atmosphereMode;
        public float atmosphereIntensity;
        public float atmosphereContrast;
        public float atmosphereBrightness;
        public float hazeAmount;
        public float fogAmount;
        public float rainAmount;
        public float atmosphereSeed;

        // Disturbance
        public bool disturbanceEnabled;
        public string noiseType;
        public float noiseStrength;
        public bool cameraJitterEnabled;
        public float cameraJitterMagnitude;
        public string platformMotionMode;
        public float platformMotionMagnitude;
    }
}
