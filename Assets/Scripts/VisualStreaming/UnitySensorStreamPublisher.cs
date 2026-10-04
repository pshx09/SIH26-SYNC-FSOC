using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using FSC.Core;

public class UnitySensorStreamPublisher : MonoBehaviour
{
    [SerializeField] private int targetFPS = 15;
    [SerializeField] private string websocketUrl = "ws://127.0.0.1:8000/ws/sensor";
    [SerializeField] private int jpegQuality = 75;

    private RenderTexture sourceRT;
    private RenderTexture captureRT;
    private ClientWebSocket ws;
    private CancellationTokenSource cts;

    private float _timeSinceLastFrame = 0f;
    private float _frameInterval = 0.066f;
    private bool _isConnecting = false;
    private bool _isRequestPending = false;

    private void Start()
    {
        _frameInterval = 1f / Mathf.Max(1, targetFPS);
        InitializeSource();
        ConnectWebSocket();
    }

    private void InitializeSource()
    {
        var virtualCamGO = GameObject.Find("VirtualCamera");
        if (virtualCamGO != null)
        {
            var cam = virtualCamGO.GetComponent<Camera>();
            if (cam != null)
            {
                sourceRT = cam.targetTexture;
            }
        }

        if (sourceRT == null)
        {
            Debug.LogWarning("[UnitySensorStreamPublisher] Could not find VirtualCamera targetTexture on startup. Will check in Update.");
            return;
        }

        captureRT = new RenderTexture(sourceRT.width, sourceRT.height, 0, GraphicsFormat.R8G8B8A8_UNorm);
        captureRT.Create();
    }

    private void Update()
    {
        _timeSinceLastFrame += Time.deltaTime;

        // Dynamic Source Switching for Benchmark-2
        if (VideoInputAdapter.Instance != null && VideoInputAdapter.Instance.isVideoModeActive && VideoInputAdapter.Instance.targetTexture != null)
        {
            sourceRT = VideoInputAdapter.Instance.targetTexture;
            if (captureRT == null || captureRT.width != sourceRT.width || captureRT.height != sourceRT.height)
            {
                if (captureRT != null) captureRT.Release();
                captureRT = new RenderTexture(sourceRT.width, sourceRT.height, 0, GraphicsFormat.R8G8B8A8_UNorm);
                captureRT.Create();
            }
        }
        else
        {
            // Re-initialize from camera if needed
            var virtualCamGO = GameObject.Find("VirtualCamera");
            if (virtualCamGO != null)
            {
                var cam = virtualCamGO.GetComponent<Camera>();
                if (cam != null && cam.targetTexture != null && sourceRT != cam.targetTexture)
                {
                    sourceRT = cam.targetTexture;
                    if (captureRT == null || captureRT.width != sourceRT.width || captureRT.height != sourceRT.height)
                    {
                        if (captureRT != null) captureRT.Release();
                        captureRT = new RenderTexture(sourceRT.width, sourceRT.height, 0, GraphicsFormat.R8G8B8A8_UNorm);
                        captureRT.Create();
                    }
                }
            }
        }

        if (_timeSinceLastFrame >= _frameInterval)
        {
            _timeSinceLastFrame = 0f;
            TryCaptureAndSend();
        }

        CheckReconnect();
        
        if (ws != null && ws.State != WebSocketState.Open && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[UnitySensorStreamPublisher] WebSocket State: {ws.State}");
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
            await ws.ConnectAsync(new Uri(websocketUrl), cts.Token);
        }
        catch (Exception)
        {
            // Silent retry
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
        if (ws == null || ws.State != WebSocketState.Open || sourceRT == null || captureRT == null)
            return;

        if (_isRequestPending)
            return;

        _isRequestPending = true;

        // Blit to ensure R8 is safely converted to R8G8B8A8_UNorm for JPG encoding
        Graphics.Blit(sourceRT, captureRT);

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
                    Debug.LogWarning($"[UnitySensorStreamPublisher] Send Error (or Timeout): {e.Message}. Reconnecting...");
                    // Force a reconnect on next frame
                    ws.Dispose();
                    ws = null;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnitySensorStreamPublisher] Readback/Encode Error: {e.Message}\n{e.StackTrace}");
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
