using UnityEngine;
using UnityEngine.UI;
using FSC.Core;

/// <summary>
/// Wires the UI SensorView (RawImage) to the dynamically generated sensor textures.
/// Automatically handles Aspect Ratio constraints and execution-order safe linking.
/// Dynamically updates the viewed texture based on which disturbance layers are active.
/// Attach this script to the SensorView RawImage GameObject.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class SensorViewSetup : MonoBehaviour
{
    [Header("Pipeline References")]
    public SensorProcessor sensorProcessor;
    public AtmosphereProcessor atmosphereProcessor;
    public DisturbanceProcessor disturbanceProcessor;

    [Header("Diagnostics (Read-Only)")]
    public string displayedTextureName = "None";

    private RawImage _rawImage;
    private Texture _currentTarget = null;

    private void Start()
    {
        _rawImage = GetComponent<RawImage>();
        EnforceAspectRatio();
    }

    private void LateUpdate()
    {
        LinkTexture();
    }

    private void LinkTexture()
    {
        // 1. Ensure references exist
        if (sensorProcessor == null) sensorProcessor = FindFirstObjectByType<SensorProcessor>();
        if (atmosphereProcessor == null) atmosphereProcessor = FindFirstObjectByType<AtmosphereProcessor>();
        if (disturbanceProcessor == null) disturbanceProcessor = FindFirstObjectByType<DisturbanceProcessor>();

        Texture targetTexture = null;

        // 2. Select the final active stage in the pipeline
        bool disturbanceActive = disturbanceProcessor != null && disturbanceProcessor.enableDisturbances && disturbanceProcessor.disturbanceType != DisturbanceType.None;
        bool atmosphereActive = atmosphereProcessor != null && atmosphereProcessor.enableAtmosphere && atmosphereProcessor.atmosphereMode != AtmosphereMode.Clear;

        if (disturbanceActive)
        {
            // Extract the output texture from DisturbanceProcessor via its downstream contract
            if (disturbanceProcessor.beaconDetector != null)
            {
                targetTexture = disturbanceProcessor.beaconDetector.sensorTexture;
            }
        }
        else if (atmosphereActive)
        {
            // Extract the output texture from AtmosphereProcessor via its downstream contract
            if (atmosphereProcessor.targetDisturbanceProcessor != null)
            {
                targetTexture = atmosphereProcessor.targetDisturbanceProcessor.inputTextureOverride;
            }
        }
        
        // 3. Fallback to clean monochrome if nothing is active or if we failed to extract the textures
        if (targetTexture == null && sensorProcessor != null)
        {
            targetTexture = sensorProcessor.processedTexture;
        }

        // 4. Update UI if changed
        if (targetTexture != null && _currentTarget != targetTexture)
        {
            _rawImage.texture = targetTexture;
            _currentTarget = targetTexture;
            displayedTextureName = targetTexture.name;
            Debug.Log($"[SensorViewSetup] UI View updated to: {displayedTextureName}");
        }
        else if (targetTexture == null && _currentTarget != null)
        {
            _rawImage.texture = null;
            _currentTarget = null;
            displayedTextureName = "None";
        }
    }

    private void EnforceAspectRatio()
    {
        // Dynamically fetch the aspect ratio from the global truth, defaulting to 640x480 (1.333...)
        float width = PSConfiguration.Instance != null ? PSConfiguration.Instance.SensorWidth : 640f;
        float height = PSConfiguration.Instance != null ? PSConfiguration.Instance.SensorHeight : 480f;

        // Ensure an AspectRatioFitter exists to prevent the UI from stretching the image
        AspectRatioFitter fitter = GetComponent<AspectRatioFitter>();
        if (fitter == null)
        {
            fitter = gameObject.AddComponent<AspectRatioFitter>();
        }

        fitter.aspectRatio = width / height;
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
    }
}
