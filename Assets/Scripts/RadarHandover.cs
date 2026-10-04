using UnityEngine;

/// <summary>
/// Simulates a one-time radar or GPS handover by instantly slewing the PTZ 
/// towards the target's initial position with a controlled residual error.
/// </summary>
[DefaultExecutionOrder(10)] // Runs after UAVTrajectoryController (0) and before PanTiltTracker (50)
public class RadarHandover : MonoBehaviour
{
    [Header("Residual Inaccuracy (Degrees)")]
    public float residualYaw = 1.0f;
    public float residualPitch = 0.5f;

    [Header("References")]
    public PanTiltTracker panTiltTracker;
    public UAVTrajectoryController targetUAV;
    
    // We optionally cache the virtual camera if not provided directly
    private Camera _virtualCamera;

    private void Start()
    {
        // Auto-resolve references if not explicitly assigned
        if (panTiltTracker == null)
        {
            panTiltTracker = GetComponentInChildren<PanTiltTracker>();
            if (panTiltTracker == null)
            {
                panTiltTracker = GetComponentInParent<PanTiltTracker>();
            }
        }

        if (targetUAV == null)
        {
            targetUAV = FindObjectOfType<UAVTrajectoryController>();
        }

        if (panTiltTracker == null || targetUAV == null)
        {
            Debug.LogWarning("[RadarHandover] Missing references. Cannot perform handover.");
            return;
        }

        _virtualCamera = panTiltTracker.tiltBase != null ? panTiltTracker.tiltBase.GetComponentInChildren<Camera>() : null;
        if (_virtualCamera == null)
        {
            Debug.LogWarning("[RadarHandover] Missing Virtual Camera. Cannot perform handover.");
            return;
        }

        Vector3 targetPos = targetUAV.transform.position;
        Vector3 cameraPos = _virtualCamera.transform.position;
        Vector3 dirToTarget = (targetPos - cameraPos).normalized;

        Transform panBase = panTiltTracker.transform;
        Transform tiltBase = panTiltTracker.tiltBase;

        float appliedYaw = NormalizeAngle(GetAxisValue(panBase.localEulerAngles, panTiltTracker.panAxis));
        float appliedPitch = NormalizeAngle(GetAxisValue(tiltBase.localEulerAngles, panTiltTracker.tiltAxis));
        
        // Target optical axis offset to intentionally place the target off-center
        Quaternion perfectCamRot = Quaternion.LookRotation(dirToTarget);
        // +residualPitch = pitch down (target goes UP in image)
        // -residualYaw = pan left (target goes RIGHT in image)
        Quaternion desiredCamRot = perfectCamRot * Quaternion.Euler(residualPitch, -residualYaw, 0);
        Vector3 desiredCamForward = desiredCamRot * Vector3.forward;

        // Use an iterative Jacobian IK solver to find the EXACT mechanical angles required 
        // to point the VirtualCamera's optical axis at the desired offset, regardless of hierarchy offsets.
        for (int i = 0; i < 50; i++)
        {
            Vector3 camForward = _virtualCamera.transform.forward;
            
            // Cross product gives the required rotation axis to align camForward with desiredCamForward
            Vector3 errorAxis = Vector3.Cross(camForward, desiredCamForward);
            
            // Project the error onto the actual mechanical axes in world space
            Vector3 panAxisWorld = panBase.TransformDirection(GetAxisVector(panTiltTracker.panAxis));
            float panStep = Vector3.Dot(errorAxis, panAxisWorld) * Mathf.Rad2Deg;
            
            Vector3 tiltAxisWorld = tiltBase.TransformDirection(GetAxisVector(panTiltTracker.tiltAxis));
            float tiltStep = Vector3.Dot(errorAxis, tiltAxisWorld) * Mathf.Rad2Deg;
            
            // Apply step with small learning rate for stability near gimbal lock
            appliedYaw += panStep * 0.1f;
            appliedPitch += tiltStep * 0.1f;
            
            // Write to transforms to update the hierarchy for the next iteration
            panBase.localEulerAngles = SetAxisValue(panBase.localEulerAngles, panTiltTracker.panAxis, appliedYaw);
            tiltBase.localEulerAngles = SetAxisValue(tiltBase.localEulerAngles, panTiltTracker.tiltAxis, appliedPitch);
        }

        // Normalize and clamp to mechanical limits
        appliedYaw = NormalizeAngle(appliedYaw);
        appliedYaw = Mathf.Clamp(appliedYaw, panTiltTracker.panMin, panTiltTracker.panMax);

        appliedPitch = NormalizeAngle(appliedPitch);
        appliedPitch = Mathf.Clamp(appliedPitch, panTiltTracker.tiltMin, panTiltTracker.tiltMax);
        
        panBase.localEulerAngles = SetAxisValue(panBase.localEulerAngles, panTiltTracker.panAxis, appliedYaw);
        tiltBase.localEulerAngles = SetAxisValue(tiltBase.localEulerAngles, panTiltTracker.tiltAxis, appliedPitch);

        // Calculate Final Angular Error for Diagnostics
        Vector3 finalCameraForward = _virtualCamera.transform.forward;
        float finalAngularError = Vector3.Angle(finalCameraForward, dirToTarget);

        Debug.Log($"[RadarHandover]\n" +
                  $"Target Position: {targetPos}\n" +
                  $"Target Direction: {dirToTarget}\n" +
                  $"Initial Camera Forward: {dirToTarget}\n" +
                  $"Coarse Pan: {appliedYaw:F2}°\n" +
                  $"Coarse Tilt: {appliedPitch:F2}°\n" +
                  $"Residual H: {residualYaw:F2}°\n" +
                  $"Residual V: {residualPitch:F2}°\n" +
                  $"Final Camera Forward: {finalCameraForward}\n" +
                  $"Final Camera/Target Angular Error: {finalAngularError:F2}°\n" +
                  $"Inside FOV: {(finalAngularError <= 2.5f ? "YES" : "NO")}");
                  
        StartCoroutine(ValidationRoutine());
    }

    private System.Collections.IEnumerator ValidationRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (_virtualCamera == null || targetUAV == null) yield break;

        BeaconDetector detector = FindFirstObjectByType<BeaconDetector>();
        
        Vector3 camPos = _virtualCamera.transform.position;
        Vector3 targetPos = targetUAV.transform.position;
        Vector3 dirToTarget = (targetPos - camPos).normalized;
        Vector3 camForward = _virtualCamera.transform.forward;

        float angularError = Vector3.Angle(camForward, dirToTarget);

        Vector3 viewportPoint = _virtualCamera.WorldToViewportPoint(targetPos);
        Vector2 expectedPixel = new Vector2(viewportPoint.x * _virtualCamera.pixelWidth, viewportPoint.y * _virtualCamera.pixelHeight);
        
        bool inFov = viewportPoint.z > 0 && 
                     viewportPoint.x >= 0f && viewportPoint.x <= 1f && 
                     viewportPoint.y >= 0f && viewportPoint.y <= 1f;

        string detectorPixelStr = (detector != null && detector.isDetected) ? detector.centroid.ToString("F2") : "NONE";

        Debug.Log($"[HandoverValidation]\n" +
                  $"CameraForward: {camForward}\n" +
                  $"TargetDirection: {dirToTarget}\n" +
                  $"AngularError: {angularError:F3}°\n" +
                  $"ExpectedViewport: {viewportPoint}\n" +
                  $"ExpectedPixel: {expectedPixel}\n" +
                  $"DetectorPixel: {detectorPixelStr}\n" +
                  $"InsideFOV: {(inFov ? "YES" : "NO")}");
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private Vector3 SetAxisValue(Vector3 euler, PanTiltTracker.RotationAxis axis, float value)
    {
        switch (axis)
        {
            case PanTiltTracker.RotationAxis.X: euler.x = value; break;
            case PanTiltTracker.RotationAxis.Y: euler.y = value; break;
            case PanTiltTracker.RotationAxis.Z: euler.z = value; break;
        }
        return euler;
    }

    private float GetAxisValue(Vector3 euler, PanTiltTracker.RotationAxis axis)
    {
        switch (axis)
        {
            case PanTiltTracker.RotationAxis.X: return euler.x;
            case PanTiltTracker.RotationAxis.Y: return euler.y;
            case PanTiltTracker.RotationAxis.Z: return euler.z;
        }
        return 0f;
    }

    private Vector3 GetAxisVector(PanTiltTracker.RotationAxis axis)
    {
        switch (axis)
        {
            case PanTiltTracker.RotationAxis.X: return Vector3.right;
            case PanTiltTracker.RotationAxis.Y: return Vector3.up;
            case PanTiltTracker.RotationAxis.Z: return Vector3.forward;
        }
        return Vector3.zero;
    }
}
