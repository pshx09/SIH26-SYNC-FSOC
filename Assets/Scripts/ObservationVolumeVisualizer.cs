using UnityEngine;

public class ObservationVolumeVisualizer : MonoBehaviour
{
    private Camera virtualCamera;
    private Transform opticalOrigin;
    private Transform target;
    private FSC.Core.TrackingMetrics trackingMetrics;
    
    private GameObject volumeRoot;
    private LineRenderer centralAxis;
    private LineRenderer[] frustumRays;
    private LineRenderer farPlaneFrame;
    private LineRenderer[] bracketLines;
    private LineRenderer[] internalRays;

    private int displayFxLayer;
    private Material lineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var displayCamGO = GameObject.Find("DisplayCamera");
        if (displayCamGO != null && displayCamGO.GetComponent<ObservationVolumeVisualizer>() == null)
        {
            displayCamGO.AddComponent<ObservationVolumeVisualizer>();
        }
    }

    private void Start()
    {
        displayFxLayer = LayerMask.NameToLayer("FSOC_DisplayFX");
        if (displayFxLayer < 0)
        {
            Debug.LogError("[ObservationVolumeVisualizer] CRITICAL: Layer FSOC_DisplayFX not found!");
            return;
        }

        var dispCam = GetComponent<Camera>();
        if (dispCam != null) dispCam.cullingMask |= (1 << displayFxLayer);

        var virtCamGO = GameObject.Find("VirtualCamera");
        if (virtCamGO != null)
        {
            virtualCamera = virtCamGO.GetComponent<Camera>();
            if (virtualCamera != null)
            {
                virtualCamera.cullingMask &= ~(1 << displayFxLayer);
                opticalOrigin = virtualCamera.transform;
            }
        }

        var uavObject = GameObject.Find("UAV_Target");
        if (uavObject != null) target = uavObject.transform;

        trackingMetrics = Object.FindAnyObjectByType<FSC.Core.TrackingMetrics>();

        lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineMaterial.SetFloat("_Surface", 1); 
        lineMaterial.SetFloat("_Blend", 0);   
        lineMaterial.renderQueue = 3000;

        CreateGeometry();
    }

    private LineRenderer CreateLine(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.layer = displayFxLayer;
        var lr = go.AddComponent<LineRenderer>();
        lr.material = lineMaterial;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        
        // High quality rounded caps
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        return lr;
    }

    private void CreateGeometry()
    {
        volumeRoot = new GameObject("ObservationVolume");
        volumeRoot.transform.SetParent(transform);
        volumeRoot.layer = displayFxLayer;

        centralAxis = CreateLine("CentralAxis", volumeRoot.transform);
        centralAxis.positionCount = 2;

        frustumRays = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            frustumRays[i] = CreateLine($"FrustumRay_{i}", volumeRoot.transform);
            frustumRays[i].positionCount = 2;
        }

        farPlaneFrame = CreateLine("FarPlaneFrame", volumeRoot.transform);
        farPlaneFrame.positionCount = 5; 

        bracketLines = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            bracketLines[i] = CreateLine($"BracketCorner_{i}", volumeRoot.transform);
            bracketLines[i].positionCount = 3; 
        }

        internalRays = new LineRenderer[8];
        for (int i = 0; i < 8; i++)
        {
            internalRays[i] = CreateLine($"InternalRay_{i}", volumeRoot.transform);
            internalRays[i].positionCount = 2;
        }
    }

    private void LateUpdate()
    {
        if (opticalOrigin == null || target == null || virtualCamera == null)
            return;

        // 1. Color State
        Color stateColor = new Color(0.36f, 0.48f, 0.6f, 0.2f);
        if (trackingMetrics != null)
        {
            var state = trackingMetrics.CurrentState;
            if (state == FSC.Core.TrackingState.ACQUIRING || state == FSC.Core.TrackingState.REACQUIRING)
                stateColor = new Color(0.88f, 0.66f, 0.2f, 0.8f); // AMBER
            else if (state == FSC.Core.TrackingState.LOCKED || state == FSC.Core.TrackingState.PREDICTING)
                stateColor = new Color(0.22f, 0.83f, 0.33f, 0.9f); // GREEN
            else if (state == FSC.Core.TrackingState.LOST)
                stateColor = new Color(0.36f, 0.48f, 0.6f, 0.1f); // DIM
        }

        lineMaterial.color = Color.white; 
        
        // 2. Geometry Vectors & Distance-Aware Widths
        Vector3 originPos = opticalOrigin.position;
        Vector3 targetPos = target.position;
        Vector3 forward = (targetPos - originPos).normalized;
        float dist = Vector3.Distance(originPos, targetPos);
        
        // Sensible bounds: at 100m, baseWidth = 0.05m
        float baseWidth = Mathf.Clamp(dist * 0.0005f, 0.002f, 0.2f);

        // Styling profiles
        ApplyStyle(centralAxis, stateColor * 1.2f, baseWidth * 4f); // Strongest central
        ApplyStyle(farPlaneFrame, stateColor * 0.7f, baseWidth);
        for (int i = 0; i < 4; i++) ApplyStyle(frustumRays[i], stateColor * 0.5f, baseWidth); // Outer shell
        for (int i = 0; i < 8; i++) ApplyStyle(internalRays[i], stateColor * 0.3f, baseWidth * 0.5f); // Thin internals
        
        Color bracketColor = new Color(stateColor.r, stateColor.g, stateColor.b, Mathf.Clamp01(stateColor.a * 1.5f));
        for (int i = 0; i < 4; i++) ApplyStyle(bracketLines[i], bracketColor, baseWidth * 5f); // Punchy bracket

        // Frustum spread
        float vFov = virtualCamera.fieldOfView;
        float aspect = virtualCamera.aspect;
        float halfHeight = Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad) * dist;
        float halfWidth = halfHeight * aspect;

        Quaternion lookRot = Quaternion.LookRotation(forward);
        Vector3 right = lookRot * Vector3.right;
        Vector3 up = lookRot * Vector3.up;

        // 3. Central Axis
        centralAxis.SetPosition(0, originPos);
        centralAxis.SetPosition(1, targetPos);

        // 4. Far Plane Corners
        Vector3 tr = targetPos + right * halfWidth + up * halfHeight;
        Vector3 tl = targetPos - right * halfWidth + up * halfHeight;
        Vector3 bl = targetPos - right * halfWidth - up * halfHeight;
        Vector3 br = targetPos + right * halfWidth - up * halfHeight;

        // 5. Frustum Rays
        frustumRays[0].SetPosition(0, originPos); frustumRays[0].SetPosition(1, tr);
        frustumRays[1].SetPosition(0, originPos); frustumRays[1].SetPosition(1, tl);
        frustumRays[2].SetPosition(0, originPos); frustumRays[2].SetPosition(1, bl);
        frustumRays[3].SetPosition(0, originPos); frustumRays[3].SetPosition(1, br);

        // 6. Far Plane Frame
        farPlaneFrame.SetPosition(0, tr);
        farPlaneFrame.SetPosition(1, tl);
        farPlaneFrame.SetPosition(2, bl);
        farPlaneFrame.SetPosition(3, br);
        farPlaneFrame.SetPosition(4, tr); 

        // 7. Internal Longitudinal Guide Rays (Polar distribution around center)
        for(int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            // Place them at ~60% of the frustum size
            float rX = Mathf.Cos(angle) * halfWidth * 0.6f;
            float rY = Mathf.Sin(angle) * halfHeight * 0.6f;
            Vector3 internalEnd = targetPos + right * rX + up * rY;
            internalRays[i].SetPosition(0, originPos);
            internalRays[i].SetPosition(1, internalEnd);
        }

        // 8. Target Bracket (Billboarded to DisplayCamera)
        Camera activeCam = GetComponent<Camera>();
        if (activeCam != null)
        {
            Vector3 camForward = (activeCam.transform.position - targetPos).normalized; 
            Quaternion billboardRot = Quaternion.LookRotation(camForward);
            Vector3 bRight = billboardRot * Vector3.right;
            Vector3 bUp = billboardRot * Vector3.up;

            float bSize = dist * 0.02f; 
            float corner = bSize * 0.35f;

            bracketLines[0].SetPosition(0, targetPos + bRight * (bSize - corner) + bUp * bSize);
            bracketLines[0].SetPosition(1, targetPos + bRight * bSize + bUp * bSize);
            bracketLines[0].SetPosition(2, targetPos + bRight * bSize + bUp * (bSize - corner));

            bracketLines[1].SetPosition(0, targetPos - bRight * (bSize - corner) + bUp * bSize);
            bracketLines[1].SetPosition(1, targetPos - bRight * bSize + bUp * bSize);
            bracketLines[1].SetPosition(2, targetPos - bRight * bSize + bUp * (bSize - corner));

            bracketLines[2].SetPosition(0, targetPos - bRight * bSize - bUp * (bSize - corner));
            bracketLines[2].SetPosition(1, targetPos - bRight * bSize - bUp * bSize);
            bracketLines[2].SetPosition(2, targetPos - bRight * (bSize - corner) - bUp * bSize);

            bracketLines[3].SetPosition(0, targetPos + bRight * bSize - bUp * (bSize - corner));
            bracketLines[3].SetPosition(1, targetPos + bRight * bSize - bUp * bSize);
            bracketLines[3].SetPosition(2, targetPos + bRight * (bSize - corner) - bUp * bSize);
        }
    }

    private void ApplyStyle(LineRenderer lr, Color color, float width)
    {
        // Prevent color blowout from clamping
        color.r = Mathf.Clamp01(color.r);
        color.g = Mathf.Clamp01(color.g);
        color.b = Mathf.Clamp01(color.b);
        color.a = Mathf.Clamp01(color.a);
        
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
    }
}
