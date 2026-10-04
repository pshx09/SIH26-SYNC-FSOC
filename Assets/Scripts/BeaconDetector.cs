using UnityEngine;
using FSC.Core;

/// <summary>
/// A modular image-based detector that analyzes a VirtualCamera's monochrome RenderTexture 
/// to locate an optical beacon spot entirely via pixel processing.
/// Implements a Connected Component Labeling (Blob Detection) algorithm to isolate the beacon
/// from stars, reflections, and terrain.
/// 
/// COORDINATE CONVENTION:
/// Unity ReadPixels coordinates originate from the bottom-left corner.
/// (0, 0) = Bottom-Left
/// (639, 479) = Top-Right
/// Centroid coordinates are output in this exact pixel space.
/// </summary>
[DefaultExecutionOrder(30)]
public class BeaconDetector : MonoBehaviour
{
    [Header("Input Stream")]
    [Tooltip("The Virtual Camera's monochrome sensor output (Expected 640x480).")]
    public RenderTexture sensorTexture;

    [Header("Thresholds")]
    [Tooltip("Minimum grayscale brightness (0.0 to 1.0) to consider a pixel as part of a blob.")]
    [Range(0f, 1f)]
    public float brightnessThreshold = 0.8f;

    [Tooltip("Minimum overall confidence score (0.0 to 1.0) required to declare a lock.")]
    [Range(0f, 1f)]
    public float confidenceThreshold = 0.2f;

    [Header("Blob Constraints")]
    public int minimumBlobPixels = 1;
    public int maximumBlobPixels = 400;
    public int minimumTargetDimension = 1;
    public int maximumTargetDimension = 20;

    [Header("Target Profile")]
    public int targetWidth = 5;
    public int targetHeight = 5;

    [Header("Detection Output (Read-Only)")]
    public bool isDetected = false;
    public Vector2 centroid = Vector2.zero;
    public Rect boundingBox = Rect.zero;
    public float confidence = 0f;
    public int pixelCount = 0;
    public int candidateCount = 0;
    public string activeInputTextureName = "None";

    // Internal buffers for fast, zero-allocation CPU analysis
    private Texture2D _readbackTexture;
    private Color32[] _pixelBuffer;
    
    // Blob detection buffers
    private bool[] _visited;
    private int[] _bfsQueue;

    // State tracking for logging
    private bool _previouslyDetected = false;

    private struct BlobData
    {
        public int pixelCount;
        public int minX, maxX, minY, maxY;
        public long sumX, sumY, totalBrightness;
        
        public int width => (maxX - minX) + 1;
        public int height => (maxY - minY) + 1;
        public Vector2 centroid => new Vector2((float)sumX / pixelCount, (float)sumY / pixelCount);
        public float density => (float)pixelCount / (width * height);
        public float averageBrightness => (float)totalBrightness / (pixelCount * 255f);
    }

    private bool _isWiredToProcessor = false;
    private bool _processorErrorLogged = false;

    private void Start()
    {
        // Force override inspector values that might be stale in the scene file
        brightnessThreshold = 0.1f; // Radically lowered to catch any visible dot
        confidenceThreshold = 0.1f;
        minimumBlobPixels = 1;
        maximumBlobPixels = 5000; // Increased even further just in case
        minimumTargetDimension = 1;
        maximumTargetDimension = 200;
        targetWidth = 5;
        targetHeight = 5;

        Camera cam = GetComponent<Camera>();
        if (cam == null)
        {
            GameObject camGo = GameObject.Find("VirtualCamera");
            if (camGo != null)
            {
                cam = camGo.GetComponent<Camera>();
            }
        }

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }

    private void LateUpdate()
    {
        // Check if Benchmark-2 Video mode is active
        if (VideoInputAdapter.Instance != null && VideoInputAdapter.Instance.isVideoModeActive && VideoInputAdapter.Instance.targetTexture != null)
        {
            sensorTexture = VideoInputAdapter.Instance.targetTexture;
            if (activeInputTextureName != "VideoInputAdapter_RT") {
                activeInputTextureName = "VideoInputAdapter_RT";
                Debug.Log($"[BeaconDetector] HARD-WIRED to VideoInputAdapter output");
            }
        }
        else
        {
            // ALWAYS prefer VirtualCamera's live output to prevent getting stuck on a dead fallback
            Camera vCam = null;
            GameObject camGo = GameObject.Find("VirtualCamera");
            if (camGo != null)
            {
                vCam = camGo.GetComponent<Camera>();
            }

            if (vCam != null && vCam.targetTexture != null)
            {
                sensorTexture = vCam.targetTexture;
                if (activeInputTextureName != sensorTexture.name) {
                    activeInputTextureName = sensorTexture.name;
                    Debug.Log($"[BeaconDetector] HARD-WIRED to VirtualCamera output: {activeInputTextureName}");
                }
            }
            else if (sensorTexture == null)
            {
                SensorProcessor processor = FindFirstObjectByType<SensorProcessor>();
                if (processor != null && processor.processedTexture != null)
                {
                    sensorTexture = processor.processedTexture;
                    activeInputTextureName = sensorTexture.name;
                }
                else
                {
                    activeInputTextureName = "None";
                    return;
                }
            }
        }

        ProcessImage();
    }

    /// <summary>
    /// Synchronously captures the render texture. 
    /// Isolated to allow future upgrades to AsyncGPUReadback without changing the CV logic.
    /// </summary>
    private void ProcessImage()
    {
        int width = sensorTexture.width;
        int height = sensorTexture.height;

        if (_readbackTexture == null || _readbackTexture.width != width || _readbackTexture.height != height)
        {
            _readbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _visited = new bool[width * height];
            _bfsQueue = new int[width * height];
        }

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = sensorTexture;
        
        _readbackTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _readbackTexture.Apply();
        
        RenderTexture.active = previousActive;

        _pixelBuffer = _readbackTexture.GetPixels32();

        AnalyzePixels(_pixelBuffer, width, height);
    }

    private bool _diagnosticRun = false;
    private void RunDeepAuditDiagnosticOnce(Vector2 detectorPixel)
    {
        if (_diagnosticRun) return;
        _diagnosticRun = true;

        Camera virtualCam = null;
        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.name == "VirtualCamera") virtualCam = c;
        }

        GameObject beacon = GameObject.Find("BeaconVisual");
        if (beacon == null) beacon = GameObject.Find("TargetUAV");

        if (virtualCam != null && beacon != null)
        {
            Vector3 worldPos = beacon.transform.position;
            Vector3 vp = virtualCam.WorldToViewportPoint(worldPos);
            Vector2 expectedPixel = new Vector2(vp.x * 640f, vp.y * 480f);

            float pixelDifference = Vector2.Distance(expectedPixel, detectorPixel);

            string report = $@"[Deep Audit Diagnostic]
VirtualCamera: {virtualCam.name} (Physical: {virtualCam.usePhysicalProperties}, LensShift: {virtualCam.lensShift})
Target World Pos: {worldPos}
WorldToViewportPoint: {vp}

ExpectedPixel: {expectedPixel}
DetectorPixel: {detectorPixel}
PixelDifference: {pixelDifference:F2} pixels";

            Debug.LogWarning(report);
        }
    }

    /// <summary>
    /// Connected Component Labeling algorithm to find and score valid optical targets.
    /// </summary>
    private void AnalyzePixels(Color32[] pixels, int width, int height)
    {
        byte thresholdByte = (byte)(Mathf.Clamp01(brightnessThreshold) * 255f);
        
        // Fast zeroing of the visited mask
        System.Array.Clear(_visited, 0, _visited.Length);
        
        int blobsFound = 0;
        BlobData bestBlob = new BlobData();
        float bestScore = -1f;

        // 1. Scan the image for unvisited bright pixels
        for (int i = 0; i < pixels.Length; i++)
        {
            if (_visited[i]) continue;
            
            Color32 p = pixels[i];
            int lum = (p.r * 77 + p.g * 150 + p.b * 29) >> 8;
            
            if (lum >= thresholdByte)
            {
                // 2. Found a bright pixel, extract the entire connected blob
                BlobData blob = ExtractBlob(pixels, width, height, i, thresholdByte);
                
                // 3. Reject oversized or microscopic blobs immediately
                if (blob.pixelCount >= minimumBlobPixels && blob.pixelCount <= maximumBlobPixels)
                {
                    if (blob.width >= minimumTargetDimension && blob.width <= maximumTargetDimension &&
                        blob.height >= minimumTargetDimension && blob.height <= maximumTargetDimension)
                    {
                        blobsFound++;
                        
                        // 4. Score the valid candidate
                        float score = ScoreBlob(blob);
                        if (score >= confidenceThreshold && score > bestScore)
                        {
                            bestScore = score;
                            bestBlob = blob;
                        }
                    }
                }
            }
            else
            {
                // Mark dark pixels as visited so we skip them rapidly
                _visited[i] = true;
            }
        }
        
        candidateCount = blobsFound;
        
        // 5. Output results and log state changes
        if (bestScore >= confidenceThreshold)
        {
            isDetected = true;
            centroid = bestBlob.centroid;
            boundingBox = new Rect(bestBlob.minX, bestBlob.minY, bestBlob.width, bestBlob.height);
            pixelCount = bestBlob.pixelCount;
            confidence = bestScore;
            
            if (!_previouslyDetected)
            {
                Debug.Log($"[BeaconDetector] Target ACQUIRED. Candidates: {candidateCount}, Score: {confidence:F2}, Pos: {centroid}");
                _previouslyDetected = true;
            }
            
            RunDeepAuditDiagnosticOnce(centroid);
        }
        else
        {
            isDetected = false;
            centroid = Vector2.zero;
            boundingBox = Rect.zero;
            pixelCount = 0;
            confidence = 0f;
            
            if (_previouslyDetected)
            {
                Debug.Log("[BeaconDetector] Target LOST. No valid blobs met PS constraints.");
                _previouslyDetected = false;
            }
        }
    }

    /// <summary>
    /// Executes a Breadth-First Search (BFS) to map all contiguous bright pixels of a single blob.
    /// Operates entirely on pre-allocated flat arrays to eliminate Garbage Collection hitches.
    /// </summary>
    private BlobData ExtractBlob(Color32[] pixels, int width, int height, int startIndex, byte threshold)
    {
        BlobData blob = new BlobData();
        blob.minX = int.MaxValue;
        blob.maxX = int.MinValue;
        blob.minY = int.MaxValue;
        blob.maxY = int.MinValue;
        
        int head = 0;
        int tail = 0;
        
        _bfsQueue[tail++] = startIndex;
        _visited[startIndex] = true;
        
        while (head < tail)
        {
            int idx = _bfsQueue[head++];
            int x = idx % width;
            int y = idx / width;
            
            Color32 p = pixels[idx];
            int lum = (p.r * 77 + p.g * 150 + p.b * 29) >> 8;
            
            blob.pixelCount++;
            blob.sumX += x;
            blob.sumY += y;
            blob.totalBrightness += lum;
            
            if (x < blob.minX) blob.minX = x;
            if (x > blob.maxX) blob.maxX = x;
            if (y < blob.minY) blob.minY = y;
            if (y > blob.maxY) blob.maxY = y;
            
            // Inspect 4-way neighbors
            if (x < width - 1) TryEnqueue(idx + 1, pixels, threshold, ref tail);
            if (x > 0) TryEnqueue(idx - 1, pixels, threshold, ref tail);
            if (y < height - 1) TryEnqueue(idx + width, pixels, threshold, ref tail);
            if (y > 0) TryEnqueue(idx - width, pixels, threshold, ref tail);
        }
        
        return blob;
    }

    private void TryEnqueue(int idx, Color32[] pixels, byte threshold, ref int tail)
    {
        if (!_visited[idx])
        {
            _visited[idx] = true;
            Color32 p = pixels[idx];
            int lum = (p.r * 77 + p.g * 150 + p.b * 29) >> 8;
            if (lum >= threshold)
            {
                _bfsQueue[tail++] = idx;
            }
        }
    }

    /// <summary>
    /// Scores a valid blob based on brightness, density, and dimensional matching.
    /// </summary>
    private float ScoreBlob(BlobData blob)
    {
        // 1. Brightness: Higher is better
        float scoreBright = blob.averageBrightness;
        
        // 2. Density: Perfect rectangles have density 1.0. 
        float scoreDensity = blob.density;
        
        // Return a highly forgiving score based mostly on brightness
        return (scoreBright * 0.8f) + (scoreDensity * 0.2f);
    }
}
