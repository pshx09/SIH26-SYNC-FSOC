using UnityEngine;

/// <summary>
/// Single source of truth for benchmark and configuration parameters
/// used throughout the ISRO FSOC Virtual Camera Tracking System.
/// This script contains no tracking logic, acting solely as a global data container.
/// </summary>
public class PSConfiguration : MonoBehaviour
{
    // Singleton instance for easy global access
    public static PSConfiguration Instance { get; private set; }

    [Header("CAMERA")]
    [Tooltip("The horizontal resolution of the sensor in pixels.")]
    [SerializeField] private int _sensorWidth = 640;
    public int SensorWidth => _sensorWidth;

    [Tooltip("The vertical resolution of the sensor in pixels.")]
    [SerializeField] private int _sensorHeight = 480;
    public int SensorHeight => _sensorHeight;

    [Tooltip("The horizontal field of view of the sensor in degrees.")]
    [SerializeField] private float _horizontalFOV = 4f;
    public float HorizontalFOV => _horizontalFOV;

    [Tooltip("The vertical field of view of the sensor in degrees.")]
    [SerializeField] private float _verticalFOV = 3f;
    public float VerticalFOV => _verticalFOV;

    [Tooltip("The update rate of the camera processing in Hz.")]
    [SerializeField] private int _cameraUpdateRateHz = 30;
    public int CameraUpdateRateHz => _cameraUpdateRateHz;


    [Header("BEACON")]
    [Tooltip("The absolute minimum allowable pixel size for a tracked beacon.")]
    [SerializeField] private int _minimumPixelSize = 5;
    public int MinimumPixelSize => _minimumPixelSize;

    [Tooltip("The standard/expected horizontal pixel width of the beacon.")]
    [SerializeField] private int _defaultPixelWidth = 10;
    public int DefaultPixelWidth => _defaultPixelWidth;

    [Tooltip("The standard/expected vertical pixel height of the beacon.")]
    [SerializeField] private int _defaultPixelHeight = 10;
    public int DefaultPixelHeight => _defaultPixelHeight;

    [Tooltip("The absolute maximum allowable pixel size for a tracked beacon.")]
    [SerializeField] private int _maximumPixelSize = 20;
    public int MaximumPixelSize => _maximumPixelSize;

    [Tooltip("The number of targets the system is expected to track simultaneously.")]
    [SerializeField] private int _targetCount = 1;
    public int TargetCount => _targetCount;


    [Header("PTZ (Pan-Tilt-Zoom)")]
    [Tooltip("The default maximum pan speed in degrees per second.")]
    [SerializeField] private float _defaultMaxPanSpeedDegPerSec = 5f;
    public float DefaultMaxPanSpeedDegPerSec => _defaultMaxPanSpeedDegPerSec;

    [Tooltip("The default maximum tilt speed in degrees per second.")]
    [SerializeField] private float _defaultMaxTiltSpeedDegPerSec = 5f;
    public float DefaultMaxTiltSpeedDegPerSec => _defaultMaxTiltSpeedDegPerSec;

    [Tooltip("The minimum allowed speed for the PTZ tracking mount in degrees per second.")]
    [SerializeField] private float _minimumAllowedPTZSpeed = 5f;
    public float MinimumAllowedPTZSpeed => _minimumAllowedPTZSpeed;

    [Tooltip("The maximum allowed speed for the PTZ tracking mount in degrees per second.")]
    [SerializeField] private float _maximumAllowedPTZSpeed = 10f;
    public float MaximumAllowedPTZSpeed => _maximumAllowedPTZSpeed;

    [Tooltip("The update rate for PTZ mechanical control logic in Hz.")]
    [SerializeField] private int _controlUpdateRateHz = 20;
    public int ControlUpdateRateHz => _controlUpdateRateHz;


    [Header("PERFORMANCE TARGETS")]
    [Tooltip("The maximum allowable time in seconds to achieve initial target lock.")]
    [SerializeField] private float _maxAcquisitionTimeSec = 2f;
    public float MaxAcquisitionTimeSec => _maxAcquisitionTimeSec;

    [Tooltip("The maximum allowable radial error in pixels to consider the target 'Locked'.")]
    [SerializeField] private float _maxTrackingErrorPixels = 10f;
    public float MaxTrackingErrorPixels => _maxTrackingErrorPixels;

    [Tooltip("The maximum acceptable percentage of frames where the target is lost.")]
    [SerializeField] private float _maxTargetLossPercent = 5f;
    public float MaxTargetLossPercent => _maxTargetLossPercent;

    [Tooltip("The maximum allowable time in seconds to reacquire a lost target.")]
    [SerializeField] private float _maxReacquisitionTimeSec = 1f;
    public float MaxReacquisitionTimeSec => _maxReacquisitionTimeSec;

    [Tooltip("The minimum expected frames per second (FPS) for the processing loop.")]
    [SerializeField] private int _minimumProcessingFPS = 20;
    public int MinimumProcessingFPS => _minimumProcessingFPS;


    [Header("DISTURBANCE LIMITS")]
    [Tooltip("The maximum standard deviation for applied image noise.")]
    [SerializeField] private float _maxNoiseStandardDeviation = 20f;
    public float MaxNoiseStandardDeviation => _maxNoiseStandardDeviation;

    [Tooltip("The maximum displacement in pixels per frame caused by simulated camera jitter.")]
    [SerializeField] private float _maxCameraJitterPixelsPerFrame = 20f;
    public float MaxCameraJitterPixelsPerFrame => _maxCameraJitterPixelsPerFrame;

    [Tooltip("The maximum displacement in pixels per frame caused by underlying platform motion.")]
    [SerializeField] private float _maxPlatformMotionPixelsPerFrame = 20f;
    public float MaxPlatformMotionPixelsPerFrame => _maxPlatformMotionPixelsPerFrame;


    private void Awake()
    {
        // Enforce Singleton Pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnValidate()
    {
        // Basic sensible validations to prevent divide-by-zero or physics breakdowns
        
        // Camera
        _sensorWidth = Mathf.Max(1, _sensorWidth);
        _sensorHeight = Mathf.Max(1, _sensorHeight);
        _horizontalFOV = Mathf.Max(0.01f, _horizontalFOV);
        _verticalFOV = Mathf.Max(0.01f, _verticalFOV);
        _cameraUpdateRateHz = Mathf.Max(1, _cameraUpdateRateHz);

        // Beacon
        _minimumPixelSize = Mathf.Max(1, _minimumPixelSize);
        _maximumPixelSize = Mathf.Max(_minimumPixelSize, _maximumPixelSize);
        _defaultPixelWidth = Mathf.Clamp(_defaultPixelWidth, _minimumPixelSize, _maximumPixelSize);
        _defaultPixelHeight = Mathf.Clamp(_defaultPixelHeight, _minimumPixelSize, _maximumPixelSize);
        _targetCount = Mathf.Max(1, _targetCount);

        // PTZ
        _minimumAllowedPTZSpeed = Mathf.Max(0f, _minimumAllowedPTZSpeed);
        _maximumAllowedPTZSpeed = Mathf.Max(_minimumAllowedPTZSpeed, _maximumAllowedPTZSpeed);
        _defaultMaxPanSpeedDegPerSec = Mathf.Clamp(_defaultMaxPanSpeedDegPerSec, _minimumAllowedPTZSpeed, _maximumAllowedPTZSpeed);
        _defaultMaxTiltSpeedDegPerSec = Mathf.Clamp(_defaultMaxTiltSpeedDegPerSec, _minimumAllowedPTZSpeed, _maximumAllowedPTZSpeed);
        _controlUpdateRateHz = Mathf.Max(1, _controlUpdateRateHz);

        // Performance Targets
        _maxAcquisitionTimeSec = Mathf.Max(0f, _maxAcquisitionTimeSec);
        _maxTrackingErrorPixels = Mathf.Max(0f, _maxTrackingErrorPixels);
        _maxTargetLossPercent = Mathf.Clamp(_maxTargetLossPercent, 0f, 100f);
        _maxReacquisitionTimeSec = Mathf.Max(0f, _maxReacquisitionTimeSec);
        _minimumProcessingFPS = Mathf.Max(1, _minimumProcessingFPS);

        // Disturbance Limits
        _maxNoiseStandardDeviation = Mathf.Max(0f, _maxNoiseStandardDeviation);
        _maxCameraJitterPixelsPerFrame = Mathf.Max(0f, _maxCameraJitterPixelsPerFrame);
        _maxPlatformMotionPixelsPerFrame = Mathf.Max(0f, _maxPlatformMotionPixelsPerFrame);
    }
}
