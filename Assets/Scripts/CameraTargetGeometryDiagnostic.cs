using UnityEngine;

/// <summary>
/// TEMPORARY DIAGNOSTIC SCRIPT
/// Calculates mathematically if the BeaconVisual is within the VirtualCamera's optical FOV.
/// Evaluates once per second.
/// </summary>
public class CameraTargetGeometryDiagnostic : MonoBehaviour
{
    [Tooltip("Assign GroundStation/.../CameraMount/VirtualCamera here.")]
    public Camera virtualCamera;

    [Tooltip("Assign TargetSystem/UAV_Target/BeaconSpot/BeaconVisual here.")]
    public Transform beaconVisual;

    private float _timer = 0f;

    private void Start()
    {
        LogDiagnostic();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= 1f)
        {
            _timer = 0f;
            LogDiagnostic();
        }
    }

    private void LogDiagnostic()
    {
        if (virtualCamera == null || beaconVisual == null)
        {
            Debug.LogWarning("[CameraTargetGeometry] Missing camera or beacon reference.");
            return;
        }

        Vector3 camPos = virtualCamera.transform.position;
        Vector3 camForward = virtualCamera.transform.forward;
        
        Vector3 beaconPos = beaconVisual.position;
        
        float distance = Vector3.Distance(camPos, beaconPos);
        Vector3 directionToBeacon = (beaconPos - camPos).normalized;

        // Viewport coordinate: 
        // x and y are [0,1] if on screen.
        // z is distance from camera plane (positive is in front)
        Vector3 viewportPoint = virtualCamera.WorldToViewportPoint(beaconPos);

        // Project direction into Camera's local space to find exact angle offsets
        Vector3 localDir = virtualCamera.transform.InverseTransformDirection(directionToBeacon);
        
        // Horizontal angle offset from optical axis (Y-axis rotation in local space)
        float horizontalAngle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        // Vertical angle offset from optical axis (X-axis rotation in local space)
        float verticalAngle = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;

        // FOV calculations
        float vFov = virtualCamera.fieldOfView;
        float hFov = Camera.VerticalToHorizontalFieldOfView(vFov, virtualCamera.aspect);

        // Geometric Boolean Tests
        bool inFront = viewportPoint.z > 0;
        bool inFov = inFront && 
                     viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
                     viewportPoint.y >= 0f && viewportPoint.y <= 1f;

        string logMessage = 
$@"[CameraTargetGeometry]
Camera Position: {camPos}
Beacon Position: {beaconPos}
Distance: {distance:F2} m
Viewport: x={viewportPoint.x:F4}, y={viewportPoint.y:F4}, z={viewportPoint.z:F4}
Horizontal Angle: {horizontalAngle:F3} deg
Vertical Angle: {verticalAngle:F3} deg
Vertical FOV: {vFov:F3} deg
Horizontal FOV: {hFov:F3} deg
IN FRONT OF CAMERA: {(inFront ? "YES" : "NO")}
INSIDE OPTICAL FOV: {(inFov ? "YES" : "NO")}";

        Debug.Log(logMessage);
    }
}
