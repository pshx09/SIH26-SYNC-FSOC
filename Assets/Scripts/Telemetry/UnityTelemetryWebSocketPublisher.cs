using UnityEngine;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using FSOC.Dashboard;
using FSOC.Contracts;

namespace FSOC.Telemetry
{
    [DefaultExecutionOrder(70)]
    public class UnityTelemetryWebSocketPublisher : MonoBehaviour
    {
        [Header("WebSocket Settings")]
        public bool enablePublishing = true;
        public string serverUrl = "ws://127.0.0.1:8000/ws/telemetry";
        [Range(1f, 60f)]
        public float publishRateHz = 15f;

        [Header("Dependencies")]
        public TelemetryBus telemetryBus;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private float _lastPublishTime = 0f;
        private bool _isConnecting = false;

        private void OnEnable()
        {
            if (enablePublishing)
            {
                Debug.Log($"[TelemetryPublisher] Enabled. Will attempt connection to {serverUrl}");
                StartConnection();
            }
        }

        private void OnDisable()
        {
            Debug.Log("[TelemetryPublisher] Disabled.");
            StopConnection();
        }

        private void Update()
        {
            if (!enablePublishing || telemetryBus == null) return;

            // Enforce publish rate
            if (Time.unscaledTime - _lastPublishTime < (1f / publishRateHz)) return;

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                TelemetrySnapshot snapshot = telemetryBus.GetLatest();
                if (snapshot.Timestamp > 0) // Ensure valid snapshot exists
                {
                    PublishSnapshot(snapshot);
                    _lastPublishTime = Time.unscaledTime;
                }
            }
            else if (!_isConnecting)
            {
                // Reconnect loop if closed or aborted
                StartConnection();
            }
        }

        private async void StartConnection()
        {
            if (_isConnecting) return;
            
            _isConnecting = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            try
            {
                var environmentUrl = Environment.GetEnvironmentVariable("UNITY_TELEMETRY_WS_URL");
                var endpoint = string.IsNullOrWhiteSpace(environmentUrl)
                    ? "wss://sih26-sync-fsoc.onrender.com/ws/telemetry"
                    : environmentUrl;
                Debug.Log($"[TelemetryPublisher] Using WebSocket URL: {endpoint}");
                await _webSocket.ConnectAsync(new Uri(endpoint), _cancellationTokenSource.Token);
                Debug.Log($"[TelemetryPublisher] Connection established to {endpoint}");
            }
            catch (Exception)
            {
                // Suppress stack trace to keep console clean; it will retry gracefully.
                CleanupWebSocket();
                
                // Enforce a bounded, non-blocking backoff before allowing the next retry
                await Task.Delay(2000);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private void StopConnection()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                // We use CancellationToken.None here to allow the close frame to send even though we cancelled the main token
                _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Publisher stopped", CancellationToken.None);
            }
            
            CleanupWebSocket();
            _isConnecting = false;
        }

        private void CleanupWebSocket()
        {
            if (_webSocket != null)
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
        }

        private async void PublishSnapshot(TelemetrySnapshot snapshot)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;

            try
            {
                // Relying on Unity's built-in JsonUtility for fast, dependency-free serialization
                string json = JsonUtility.ToJson(snapshot);
                var bytes = Encoding.UTF8.GetBytes(json);
                var segment = new ArraySegment<byte>(bytes);

                await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                // Log only once per disconnection event
                Debug.LogWarning($"[TelemetryPublisher] Connection lost/error during send: {ex.Message}. Will reconnect.");
                CleanupWebSocket();
            }
        }
    }
}
