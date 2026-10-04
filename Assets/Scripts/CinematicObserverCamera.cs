using UnityEngine;

public class CinematicObserverCamera : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var displayCamGO = GameObject.Find("DisplayCamera");
        if (displayCamGO != null && displayCamGO.GetComponent<CinematicObserverCamera>() == null)
        {
            displayCamGO.AddComponent<CinematicObserverCamera>();
        }
    }

    private Transform groundStation;
    private Transform target;

    // Cinematic configuration
    public Vector3 offsetDirection = new Vector3(-0.5f, 0.5f, -1f).normalized;
    public float minDistance = 20f;
    public float distanceMultiplier = 1.2f;
    
    // Smoothing parameters
    public float positionSmoothTime = 0.5f;
    public float rotationSmoothTime = 2.0f; // Note: For Slerp, this acts as a speed multiplier instead of smooth time if we use Time.deltaTime
    
    private Vector3 positionVelocity = Vector3.zero;
    
    private Vector3 lastValidFramingCenter;
    private float lastValidDistance;

    private void Start()
    {
        // Try to find the required objects
        var gsObject = GameObject.Find("GroundStation");
        if (gsObject != null)
        {
            groundStation = gsObject.transform;
        }
        else
        {
            Debug.LogWarning("[CinematicObserverCamera] Could not find 'GroundStation'.");
        }

        var uavObject = GameObject.Find("UAV_Target");
        if (uavObject != null)
        {
            target = uavObject.transform;
        }
        else
        {
            Debug.LogWarning("[CinematicObserverCamera] Could not find 'UAV_Target'.");
        }

        if (groundStation != null && target != null)
        {
            lastValidFramingCenter = (groundStation.position + target.position) / 2f;
            lastValidDistance = Vector3.Distance(groundStation.position, target.position);
        }
    }

    private void LateUpdate()
    {
        if (groundStation == null)
            return;

        Vector3 currentCenter = lastValidFramingCenter;
        float currentDistance = lastValidDistance;

        if (target != null)
        {
            currentCenter = (groundStation.position + target.position) / 2f;
            currentDistance = Vector3.Distance(groundStation.position, target.position);
            
            // Save last valid state in case target is lost
            lastValidFramingCenter = currentCenter;
            lastValidDistance = currentDistance;
        }

        // Calculate desired distance based on separation to keep both in frame
        float desiredDistance = Mathf.Max(minDistance, currentDistance * distanceMultiplier);

        // Calculate desired position
        Vector3 desiredPosition = currentCenter + (offsetDirection * desiredDistance);

        // Calculate desired rotation (looking at the center)
        Quaternion desiredRotation = Quaternion.LookRotation(currentCenter - desiredPosition);

        // Smoothly interpolate position using SmoothDamp (critically damped)
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);

        // Smoothly interpolate rotation using Slerp
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSmoothTime);
    }
}
