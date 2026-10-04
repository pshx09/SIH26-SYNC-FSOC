using UnityEngine;

/// <summary>
/// Controls the simulated optical beacon size to ensure it covers a specific 
/// number of pixels on the virtual camera sensor, regardless of distance.
/// Automatically enforces PSConfiguration limits and maintains intense optical visibility.
/// </summary>
public class BeaconSpotController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Virtual Camera observing the beacon.")]
    public Camera sensorCamera;

    [Tooltip("The visual element of the beacon to scale and rotate (e.g., BeaconVisual).")]
    public Transform visualElement;

    // These fields are kept as fallbacks if PSConfiguration is missing or disabled
    [Header("Fallback Optical Spot Settings")]
    public int targetPixelSize = 10;
    public int minPixelSize = 5;
    public int maxPixelSize = 20;
    public int sensorVerticalResolution = 480;

    [Header("Optical Mounting")]
    [Tooltip("Local offset to ensure the beacon protrudes from the UAV body mesh (e.g., mounted underneath).")]
    public Vector3 localOffset = new Vector3(0f, -0.5f, 0f);

    [Header("Live Debug Status")]
    [SerializeField] private string _activeLayer = "Uninitialized";
    [SerializeField] private string _activeMaterial = "Uninitialized";
    [SerializeField] private string _activeFootprint = "Uninitialized";
    
    [Space]
    [Tooltip("The configured target size the beacon is attempting to achieve.")]
    [SerializeField] private string _configuredTargetSize = "Uninitialized";
    
    [Tooltip("The mathematically estimated width of the beacon footprint in sensor pixels.")]
    [SerializeField] private float _projectedSensorWidthPx = 0f;
    
    [Tooltip("The mathematically estimated height of the beacon footprint in sensor pixels.")]
    [SerializeField] private float _projectedSensorHeightPx = 0f;

    private int _previousLoggedSize = -1;

    private void Start()
    {
        InitializeOpticalVisual();
    }

    /// <summary>
    /// Programmatically guarantees the visual node meets strict optical processing criteria.
    /// It enforces a 2D square quad geometry and a pure white unlit emission to 
    /// stand out fiercely in the monochrome processor feed.
    /// </summary>
    private void InitializeOpticalVisual()
    {
        if (visualElement == null)
        {
            Transform existing = transform.Find("BeaconVisual");
            if (existing != null)
            {
                visualElement = existing;
            }
            else
            {
                Debug.LogWarning("[BeaconSpotController] visualElement not assigned. Auto-generating BeaconVisual child.", this);
                GameObject generated = GameObject.CreatePrimitive(PrimitiveType.Quad);
                generated.name = "BeaconVisual";
                generated.transform.SetParent(this.transform);
                generated.transform.localPosition = Vector3.zero;
                visualElement = generated.transform;
            }
        }

        // 0. Enforce Layer Separation
        int opticalLayer = LayerMask.NameToLayer("FSOC_OpticalBeacon");
        if (opticalLayer == -1)
        {
            Debug.LogWarning("[BeaconSpotController] 'FSOC_OpticalBeacon' layer missing! Please add it in Unity (Edit > Project Settings > Tags and Layers). Falling back to layer 8.", this);
            opticalLayer = 8;
        }
        visualElement.gameObject.layer = opticalLayer;

        // 1. Force the visual to be a square Quad for PS-compliant optical imaging
        MeshFilter mf = visualElement.GetComponent<MeshFilter>();
        if (mf == null) mf = visualElement.gameObject.AddComponent<MeshFilter>();
        
        GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mf.sharedMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempQuad); // Clean up the temp geometric generator immediately

        // 2. Remove physics colliders as this is purely optical
        Collider col = visualElement.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // 3. Force intense optical emission material for URP
        // By using Universal Render Pipeline/Unlit, we ensure the beacon is always 100% white/bright
        // independent of scene lighting, and compatible with Unity 6.
        MeshRenderer mr = visualElement.GetComponent<MeshRenderer>();
        if (mr == null) mr = visualElement.gameObject.AddComponent<MeshRenderer>();
        
        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material unlitMat;
        if (urpUnlit != null)
        {
            unlitMat = new Material(urpUnlit);
            unlitMat.SetColor("_BaseColor", Color.white);
            // Optionally force it to render on top of the UAV in the editor if it clips
            // unlitMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }
        else
        {
            // Fallback for non-URP/Built-in
            unlitMat = new Material(Shader.Find("Unlit/Color"));
            unlitMat.color = Color.white;
        }
        
        mr.sharedMaterial = unlitMat;

        _activeLayer = LayerMask.LayerToName(opticalLayer);
        _activeMaterial = unlitMat.shader.name;
    }

    private void LateUpdate()
    {
        if (sensorCamera == null || visualElement == null)
            return;

        // Fetch authoritative PS configuration, prioritizing the global truth over local arbitrary fields
        PSConfiguration config = PSConfiguration.Instance;
        int tSize = config != null ? config.DefaultPixelWidth : targetPixelSize; // Assume square footprint
        int minSize = config != null ? config.MinimumPixelSize : minPixelSize;
        int maxSize = config != null ? config.MaximumPixelSize : maxPixelSize;
        int sensorRes = config != null ? config.SensorHeight : sensorVerticalResolution;

        tSize = Mathf.Clamp(tSize, minSize, maxSize);

        // 1. Calculate the distance from the visual element to the camera lens.
        float distance = Vector3.Distance(visualElement.position, sensorCamera.transform.position);

        // 2. Calculate the world-space height of the camera's view frustum at that exact distance.
        float fovRadians = sensorCamera.fieldOfView * Mathf.Deg2Rad;
        float frustumHeightAtDistance = 2.0f * distance * Mathf.Tan(fovRadians * 0.5f);

        // 3. Determine the physical world size of a single pixel at that distance.
        float worldSizePerVerticalPixel = frustumHeightAtDistance / sensorRes;
        
        int sensorHorizontalRes = config != null ? config.SensorWidth : 640;
        float frustumWidthAtDistance = frustumHeightAtDistance * sensorCamera.aspect;
        float worldSizePerHorizontalPixel = frustumWidthAtDistance / sensorHorizontalRes;

        // 4. Calculate total required world scale so it occupies the exact target pixel footprint.
        float targetWorldScale = tSize * worldSizePerVerticalPixel;

        // 5. Apply the uniform scale and mounting offset to the visual element.
        visualElement.localScale = new Vector3(targetWorldScale, targetWorldScale, targetWorldScale);
        visualElement.localPosition = localOffset;

        // Populate independent diagnostic measurements
        _configuredTargetSize = $"{tSize}x{tSize} px";
        _projectedSensorWidthPx = targetWorldScale / worldSizePerHorizontalPixel;
        _projectedSensorHeightPx = targetWorldScale / worldSizePerVerticalPixel;

        // Diagnostic Logging & Live Status Update
        _activeFootprint = $"{tSize}x{tSize} px (Geo: {targetWorldScale:F3}m)";

        if (tSize != _previousLoggedSize)
        {
            Debug.Log($"[BeaconSpotController] Target footprint: {tSize}x{tSize} px | Projected geometric size: {tSize:F1} px | UAV Distance: {distance:F2}m");
            _previousLoggedSize = tSize;
        }

        // 6. Make the Quad continually face the camera. 
        // Note: A Unity Quad's visible face points along its -Z axis.
        // Therefore, to make the visible face point AT the camera, we must point its +Z axis AWAY from the camera.
        Vector3 directionToCamera = sensorCamera.transform.position - visualElement.position;
        if (directionToCamera != Vector3.zero)
        {
            visualElement.rotation = Quaternion.LookRotation(-directionToCamera);
        }
    }
}
