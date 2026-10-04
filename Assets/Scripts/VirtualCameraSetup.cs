using UnityEngine;

/// <summary>
/// Configures the VirtualCamera using parameters defined in the global PSConfiguration.
/// Ensures the camera's FOV and RenderTexture match the benchmark constraints.
/// Attach this directly to the VirtualCamera GameObject.
/// </summary>
[RequireComponent(typeof(Camera))]
public class VirtualCameraSetup : MonoBehaviour
{
    private Camera _virtualCamera;

    private void Start()
    {
        _virtualCamera = GetComponent<Camera>();
        ApplyConfiguration();
    }

    /// <summary>
    /// Reads from the single source of truth (PSConfiguration) and configures the camera optics.
    /// Executed once at startup to avoid unnecessary Update() overhead.
    /// </summary>
    private void ApplyConfiguration()
    {
        PSConfiguration config = PSConfiguration.Instance;
        
        if (config == null)
        {
            // Fallback in case execution order processes this before PSConfiguration.Awake()
            config = FindFirstObjectByType<PSConfiguration>();
        }

        if (config == null)
        {
            Debug.LogError("[VirtualCameraSetup] PSConfiguration is missing from the scene! Cannot apply optical parameters.", this);
            return;
        }

        // 1. Apply Vertical FOV
        // Unity's Camera.fieldOfView property strictly dictates the VERTICAL field of view.
        _virtualCamera.fieldOfView = config.VerticalFOV;

        // 2. Configure Culling Mask for Pure Optical Imaging
        // Exclude the UAV body and all irrelevant background geometry from the sensor feed.
        int opticalLayer = LayerMask.NameToLayer("FSOC_OpticalBeacon");
        if (opticalLayer == -1)
        {
            opticalLayer = 8;
        }
        
        _virtualCamera.cullingMask = (1 << opticalLayer);

        // NOTE regarding Horizontal FOV (4 degrees):
        // Unity automatically derives the horizontal FOV based on the camera's aspect ratio.
        // At 640x480 (a 1.333 aspect ratio), a 3-degree vertical FOV yields roughly a 4-degree horizontal FOV.
        // We do NOT set Camera.fieldOfView to 4 degrees, as that would distort the tracking optics.

        // 2. Validate and Enforce RenderTexture Resolution (FSOC_Sensor_RT)
        if (_virtualCamera.targetTexture != null)
        {
            RenderTexture rt = _virtualCamera.targetTexture;
            
            if (rt.width != config.SensorWidth || rt.height != config.SensorHeight)
            {
                Debug.LogWarning($"[VirtualCameraSetup] Resizing FSOC_Sensor_RT from {rt.width}x{rt.height} to {config.SensorWidth}x{config.SensorHeight} to match PSConfiguration.", this);
                
                // Release and recreate allows resizing the texture in memory without 
                // breaking the object reference for other scripts like BeaconDetector.
                rt.Release();
                rt.width = config.SensorWidth;
                rt.height = config.SensorHeight;
                rt.Create();
            }
        }
        else
        {
            Debug.LogWarning("[VirtualCameraSetup] The VirtualCamera has no Output Texture (FSOC_Sensor_RT) assigned in the Inspector. Detection will fail.", this);
        }
    }
}
