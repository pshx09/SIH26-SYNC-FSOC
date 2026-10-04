using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class UnitySimulationStreamPublisher : MonoBehaviour
{
    [SerializeField] private int targetFPS = 10;
    [SerializeField] private int renderWidth = 1920;
    [SerializeField] private int renderHeight = 1080;
    [SerializeField] private string websocketUrl = "ws://127.0.0.1:8000/ws/simulation";
    [SerializeField] private int jpegQuality = 90;

    private Camera sourceCamera;
    private Camera captureCamera;
    private RenderTexture captureRT;
    private ClientWebSocket ws;
    private CancellationTokenSource cts;

    private float _timeSinceLastFrame = 0f;
    private float _frameInterval = 0.1f;
    private bool _isConnecting = false;
    private bool _isRequestPending = false;

    private void Start()
    {
        // Force high-quality overrides, ignoring potentially degraded Inspector/Scene values
        renderWidth = 1920;
        renderHeight = 1080;
        jpegQuality = 92;

        _frameInterval = 1f / Mathf.Max(1, targetFPS);
        InitializeCaptureCamera();
        ConnectWebSocket();
    }

    private void InitializeCaptureCamera()
    {
        // Find DisplayCamera
        var displayCamGO = GameObject.Find("DisplayCamera");
        if (displayCamGO != null)
        {
            sourceCamera = displayCamGO.GetComponent<Camera>();
        }
        else
        {
            sourceCamera = Camera.main;
        }

        if (sourceCamera == null)
        {
            Debug.LogError("[UnitySimulationStreamPublisher] No source camera found!");
            enabled = false;
            return;
        }

        // Create capture camera
        var captureGO = new GameObject("SimulationStream_CaptureCamera");
        captureGO.transform.SetParent(transform);
        captureCamera = captureGO.AddComponent<Camera>();
        captureCamera.CopyFrom(sourceCamera);
        
        // Force perfect 16:9 aspect and full viewport to eliminate black borders
        captureCamera.rect = new Rect(0, 0, 1, 1);
        captureCamera.aspect = (float)renderWidth / renderHeight;
        
        // Enhance render quality for the stream (URP)
        var cameraData = captureCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
        cameraData.renderShadows = true;
        
        // We do not want to render to the screen or interfere with main render
        captureCamera.enabled = false; // We will call Render() manually
        
        captureRT = new RenderTexture(renderWidth, renderHeight, 24, GraphicsFormat.R8G8B8A8_UNorm);
        captureRT.Create();
        captureCamera.targetTexture = captureRT;
    }

    private void Update()
    {
        if (sourceCamera != null && captureCamera != null)
        {
            // Continuously mirror transform and settings
            captureCamera.transform.position = sourceCamera.transform.position;
            captureCamera.transform.rotation = sourceCamera.transform.rotation;
            captureCamera.fieldOfView = sourceCamera.fieldOfView;
            captureCamera.nearClipPlane = sourceCamera.nearClipPlane;
            captureCamera.farClipPlane = sourceCamera.farClipPlane;
        }

        _timeSinceLastFrame += Time.deltaTime;

        if (_timeSinceLastFrame >= _frameInterval)
        {
            _timeSinceLastFrame = 0f;
            TryCaptureAndSend();
        }

        CheckReconnect();
        
        if (ws != null && ws.State != WebSocketState.Open && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[UnitySimulationStreamPublisher] WebSocket State: {ws.State}");
        }
    }

    private float _nextReconnectTime = 0f;

    private async void ConnectWebSocket()
    {
        if (_isConnecting || (ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting)))
            return;

        _isConnecting = true;
        ws = new ClientWebSocket();
        cts = new CancellationTokenSource();

        try
        {
            var environmentUrl = Environment.GetEnvironmentVariable("UNITY_SIMULATION_WS_URL");
            var endpoint = string.IsNullOrWhiteSpace(environmentUrl)
                ? "wss://sih26-sync-fsoc.onrender.com/ws/simulation"
                : environmentUrl;
            Debug.Log($"[UnitySimulationStreamPublisher] Using WebSocket URL: {endpoint}");
            await ws.ConnectAsync(new Uri(endpoint), cts.Token);
        }
        catch (Exception)
        {
            // Silently retry later
            await Task.Delay(2000);
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private void CheckReconnect()
    {
        if (Time.realtimeSinceStartup < _nextReconnectTime)
            return;

        if (ws == null)
        {
            ConnectWebSocket();
            _nextReconnectTime = Time.realtimeSinceStartup + 2f;
            return;
        }

        if (ws.State != WebSocketState.Open && ws.State != WebSocketState.Connecting)
        {
            ws.Dispose();
            ws = null;
            ConnectWebSocket();
            _nextReconnectTime = Time.realtimeSinceStartup + 2f;
        }
    }

    private void TryCaptureAndSend()
    {
        if (ws == null || ws.State != WebSocketState.Open)
            return;

        // Ensure max one in-flight frame
        if (_isRequestPending)
            return;

        _isRequestPending = true;

        // Render manual frame
        captureCamera.Render();

        // Async GPU readback to R8G8B8A8_UNorm
        AsyncGPUReadback.Request(captureRT, 0, GraphicsFormat.R8G8B8A8_UNorm, OnCompleteReadback);
    }

    private async void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        try
        {
            if (request.hasError)
                return;

            if (ws != null && ws.State == WebSocketState.Open)
            {
                var data = request.GetData<byte>();
                
                // Encode NativeArray to JPG natively (fast, no allocation)
                var jpgNative = ImageConversion.EncodeNativeArrayToJPG(
                    data, 
                    GraphicsFormat.R8G8B8A8_UNorm, 
                    (uint)request.width, 
                    (uint)request.height, 
                    (uint)(request.width * 4), 
                    jpegQuality
                );

                byte[] jpgBytes = jpgNative.ToArray();
                jpgNative.Dispose();

                try
                {
                    using var timeoutCts = new CancellationTokenSource(1000);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);
                    await ws.SendAsync(new ArraySegment<byte>(jpgBytes), WebSocketMessageType.Binary, true, linkedCts.Token);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UnitySimulationStreamPublisher] Send Error (or Timeout): {e.Message}. Reconnecting...");
                    // Force a reconnect on next frame
                    ws.Dispose();
                    ws = null;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnitySimulationStreamPublisher] Readback/Encode Error: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            _isRequestPending = false;
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
        
        if (ws != null)
        {
            if (ws.State == WebSocketState.Open)
                ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Destroy", CancellationToken.None);
            ws.Dispose();
        }

        AsyncGPUReadback.WaitAllRequests();

        if (captureRT != null)
        {
            captureRT.Release();
            Destroy(captureRT);
        }
    }
}
