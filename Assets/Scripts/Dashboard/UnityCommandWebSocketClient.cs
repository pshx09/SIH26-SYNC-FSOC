using UnityEngine;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using FSC.Core;
using FSOC.Contracts;
using System.Collections.Concurrent;

namespace FSOC.Dashboard
{
    [Serializable]
    public class CommandPayload
    {
        // Trajectory
        public string mode;
        public float straightLineStartX;
        public float straightLineStartY;
        public float straightLineStartZ;
        public float straightLineDirX;
        public float straightLineDirY;
        public float straightLineDirZ;
        public float straightLineSpeed;
        public float straightLineRange;
        public float orbitCenterX;
        public float orbitCenterY;
        public float orbitCenterZ;
        public float orbitRadius;
        public float orbitAltitude;
        public float orbitAngularSpeed;
        public int orbitDirection;
        public float figure8AmplitudeX;
        public float figure8AmplitudeY;
        public float figure8Period;
        public float figure8MaxSpeed;
        public float randomBoundsX;
        public float randomBoundsY;
        public float randomSpeed;
        public int randomSeed;

        // PTZ
        public float maxPanSpeed;
        public float maxTiltSpeed;
        public int ptzUpdateRate;

        // Environment / Disturbance
        public bool atmosphereEnabled;
        public string atmosphereMode;
        public float atmosphereIntensity;
        public float atmosphereContrast;
        public float atmosphereBrightness;
        public float hazeAmount;
        public float fogAmount;
        public float rainAmount;
        public float atmosphereSeed;
        
        public bool disturbanceEnabled;
        public string noiseType;
        public float noiseStrength;
        public bool cameraJitterEnabled;
        public float cameraJitterMagnitude;
        public string platformMotionMode;
        public float platformMotionMagnitude;
        
        // Video Input
        public string filePath;
        public float speed;
    }

    [Serializable]
    public class CommandMessage
    {
        public string commandId;
        public string commandType;
        public CommandPayload payload;
        public float timestamp;
    }

    [Serializable]
    public class CommandResponse
    {
        public string commandId;
        public string status;
        public string message;
        public string downloadFileName;
        public string downloadContent;
        public int stateRevision;
    }

    [DefaultExecutionOrder(75)]
    public class UnityCommandWebSocketClient : MonoBehaviour
    {
        public bool enableCommands = true;
        public string serverUrl = "ws://127.0.0.1:8000/ws/commands/unity";

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isConnecting = false;
        private int _stateRevision = 1;

        // Thread-safe queue to marshal commands to the main Unity thread
        private ConcurrentQueue<CommandMessage> _commandQueue = new ConcurrentQueue<CommandMessage>();

        private void OnEnable()
        {
            if (enableCommands)
            {
                StartConnection();
            }
        }

        private void OnDisable()
        {
            StopConnection();
        }

        private float _reconnectTimer = 0f;
        private const float ReconnectInterval = 2.0f;

        private void Update()
        {
            if (!enableCommands) return;

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                // Process commands on the main thread
                while (_commandQueue.TryDequeue(out var command))
                {
                    ProcessCommand(command);
                }
            }
            else if (!_isConnecting)
            {
                _reconnectTimer += Time.deltaTime;
                if (_reconnectTimer >= ReconnectInterval)
                {
                    _reconnectTimer = 0f;
                    StartConnection();
                }
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
                await _webSocket.ConnectAsync(new Uri(serverUrl), _cancellationTokenSource.Token);
                Debug.Log($"[CommandClient] Connected to {serverUrl}");
                _ = ReceiveLoop();
            }
            catch (Exception)
            {
                CleanupWebSocket();
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            try
            {
                while (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.Log($"[UNITY] command received: {json}");
                    try
                    {
                        var command = JsonUtility.FromJson<CommandMessage>(json);
                        if (command != null && !string.IsNullOrEmpty(command.commandId))
                        {
                            _commandQueue.Enqueue(command);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[CommandClient] JSON Parse Error: {ex.Message}");
                    }
                }
            }
            catch (Exception)
            {
                // Reconnect will be handled in Update
            }
        }

        private void ProcessCommand(CommandMessage command)
        {
            string status = "REJECTED";
            string message = "Unknown command type";
            string downloadFileName = null;
            string downloadContent = null;
            bool success = false;

            try
            {
                switch (command.commandType)
                {
                    // RUN CONTROL
                    case "simulation.start":
                        SimulationRuntimeController.Instance.StartSimulation();
                        success = true; message = "Simulation started.";
                        break;
                    case "simulation.pause":
                        SimulationRuntimeController.Instance.PauseSimulation();
                        success = true; message = "Simulation paused.";
                        break;
                    case "simulation.resume":
                        SimulationRuntimeController.Instance.ResumeSimulation();
                        success = true; message = "Simulation resumed.";
                        break;
                    case "simulation.stop":
                        SimulationRuntimeController.Instance.StopSimulation();
                        success = true; message = "Simulation stopped.";
                        break;
                    case "simulation.reset":
                        SimulationRuntimeController.Instance.ResetSimulation();
                        success = true; message = "Simulation reset to initial state.";
                        break;
                        
                    // VIDEO INPUT
                    case "video.load":
                        var adapter = VideoInputAdapter.GetOrCreate();
                        adapter.isVideoModeActive = true;
                        adapter.LoadVideo(command.payload.filePath);
                        ResetBenchmarkTrackingState();
                        
                        var ptzVideo = FindFirstObjectByType<PanTiltTracker>();
                        if (ptzVideo != null) ptzVideo.bypassActuator = true;
                        
                        success = true; message = "Video loaded and PTZ bypassed.";
                        break;
                    case "video.play":
                        if (VideoInputAdapter.Instance != null)
                        {
                            VideoInputAdapter.Instance.Play();
                            success = true; message = "Video playing.";
                        }
                        break;
                    case "video.pause":
                        if (VideoInputAdapter.Instance != null)
                        {
                            VideoInputAdapter.Instance.Pause();
                            success = true; message = "Video paused.";
                        }
                        break;
                    case "video.stop":
                        if (VideoInputAdapter.Instance != null)
                        {
                            VideoInputAdapter.Instance.Stop();
                            success = true; message = "Video stopped.";
                        }
                        break;
                    case "video.reset":
                        if (VideoInputAdapter.Instance != null && VideoInputAdapter.Instance.videoPlayer != null)
                        {
                            VideoInputAdapter.Instance.Pause();
                            VideoInputAdapter.Instance.videoPlayer.time = 0;
                            success = true; message = "Video reset.";
                        }
                        break;
                    case "video.setSpeed":
                        if (VideoInputAdapter.Instance != null && VideoInputAdapter.Instance.videoPlayer != null)
                        {
                            VideoInputAdapter.Instance.videoPlayer.playbackSpeed = command.payload.speed;
                            success = true; message = "Video speed set.";
                        }
                        break;
                    case "benchmark.start":
                        var startMonitor = FindFirstObjectByType<PerformanceMonitor>();
                        if (startMonitor != null)
                        {
                            if (startMonitor.benchmarkStatus == BenchmarkStatus.Paused)
                                startMonitor.ResumeBenchmark();
                            else
                                startMonitor.StartBenchmark();
                            success = true; message = "Benchmark started or resumed.";
                        }
                        else message = "Performance monitor is unavailable.";
                        break;
                    case "benchmark.pause":
                        var pauseMonitor = FindFirstObjectByType<PerformanceMonitor>();
                        if (pauseMonitor != null)
                        {
                            pauseMonitor.PauseBenchmark();
                            success = true; message = "Benchmark paused.";
                        }
                        else message = "Performance monitor is unavailable.";
                        break;
                    case "benchmark.stop":
                        var stopMonitor = FindFirstObjectByType<PerformanceMonitor>();
                        if (stopMonitor != null)
                        {
                            stopMonitor.StopBenchmark();
                            success = true; message = "Benchmark stopped.";
                        }
                        else message = "Performance monitor is unavailable.";
                        break;
                    case "benchmark.reset":
                        ResetBenchmarkTrackingState();
                        success = true; message = "Benchmark and tracking metrics reset.";
                        break;
                    case "benchmark.exportCsv":
                        var csvMonitor = FindFirstObjectByType<PerformanceMonitor>();
                        if (csvMonitor != null && csvMonitor.TryGetSamplesCsv(out downloadFileName, out downloadContent))
                        {
                            success = true; message = "Benchmark sample CSV ready.";
                        }
                        else message = "No completed benchmark sample CSV is available.";
                        break;
                    case "benchmark.exportReport":
                        var reportMonitor = FindFirstObjectByType<PerformanceMonitor>();
                        if (reportMonitor != null && reportMonitor.TryGetSummaryCsv(out downloadFileName, out downloadContent))
                        {
                            success = true; message = "Benchmark summary report ready.";
                        }
                        else message = "No completed benchmark summary report is available.";
                        break;
                    case "video.disable":
                        if (VideoInputAdapter.Instance != null)
                        {
                            VideoInputAdapter.Instance.isVideoModeActive = false;
                            VideoInputAdapter.Instance.Stop();
                            
                            var ptzLive = FindFirstObjectByType<PanTiltTracker>();
                            if (ptzLive != null) ptzLive.bypassActuator = false;

                            var activeMonitor = FindFirstObjectByType<PerformanceMonitor>();
                            if (activeMonitor != null) activeMonitor.StopBenchmark();
                            
                            success = true; message = "Video mode disabled. Live unity mode restored.";
                        }
                        break;

                    // TRAJECTORY
                    case "trajectory.setMode":
                        var uav = FindFirstObjectByType<UAVTrajectoryController>();
                        if (uav != null && Enum.TryParse(command.payload.mode, out UAVTrajectoryController.TrajectoryMode mode))
                        {
                            uav.currentMode = mode;
                            success = true; message = $"Trajectory mode set to {mode}";
                        }
                        break;
                        
                    case "trajectory.setStraightLine":
                        var uavS = FindFirstObjectByType<UAVTrajectoryController>();
                        if (uavS != null)
                        {
                            uavS.straightLineStart = new Vector3(command.payload.straightLineStartX, command.payload.straightLineStartY, command.payload.straightLineStartZ);
                            uavS.straightLineDirection = new Vector3(command.payload.straightLineDirX, command.payload.straightLineDirY, command.payload.straightLineDirZ);
                            uavS.straightLineSpeed = command.payload.straightLineSpeed;
                            uavS.straightLineRange = command.payload.straightLineRange;
                            success = true; message = "Straight line parameters applied.";
                        }
                        break;

                    case "trajectory.setCircular":
                        var uavC = FindFirstObjectByType<UAVTrajectoryController>();
                        if (uavC != null)
                        {
                            uavC.orbitCenter = new Vector3(command.payload.orbitCenterX, command.payload.orbitCenterY, command.payload.orbitCenterZ);
                            uavC.orbitRadius = command.payload.orbitRadius;
                            uavC.altitude = command.payload.orbitAltitude;
                            uavC.angularSpeed = command.payload.orbitAngularSpeed;
                            uavC.direction = (UAVTrajectoryController.OrbitDirection)command.payload.orbitDirection;
                            // Ensure valid radius
                            if (uavC.orbitRadius < 1f) uavC.orbitRadius = 1f;
                            success = true; message = "Circular parameters applied.";
                        }
                        break;
                        
                    case "trajectory.setFigure8":
                        var uavF = FindFirstObjectByType<UAVTrajectoryController>();
                        if (uavF != null)
                        {
                            uavF.figure8Amplitude = new Vector2(command.payload.figure8AmplitudeX, command.payload.figure8AmplitudeY);
                            uavF.figure8Period = command.payload.figure8Period;
                            uavF.figure8MaxSpeed = command.payload.figure8MaxSpeed;
                            success = true; message = "Figure-8 parameters applied.";
                        }
                        break;

                    case "trajectory.setRandom":
                        var uavR = FindFirstObjectByType<UAVTrajectoryController>();
                        if (uavR != null)
                        {
                            uavR.randomBounds = new Vector2(command.payload.randomBoundsX, command.payload.randomBoundsY);
                            uavR.randomSpeed = command.payload.randomSpeed;
                            uavR.randomSeed = command.payload.randomSeed;
                            success = true; message = "Random parameters applied.";
                        }
                        break;

                    // DISTURBANCE
                    case "disturbance.setConfig":
                        var dist = FindFirstObjectByType<DisturbanceProcessor>();
                        if (dist != null)
                        {
                            dist.enableDisturbances = command.payload.disturbanceEnabled;
                            if (Enum.TryParse(command.payload.noiseType, out DisturbanceType nType))
                            {
                                dist.disturbanceType = nType;
                                if (nType == DisturbanceType.Gaussian)
                                    dist.gaussianStandardDeviation = Mathf.Clamp(command.payload.noiseStrength, 0f, 1f);
                                else if (nType == DisturbanceType.SaltAndPepper)
                                    dist.saltAndPepperIntensity = Mathf.Clamp(command.payload.noiseStrength, 0f, 1f);
                                else if (nType == DisturbanceType.Poisson)
                                    dist.poissonStrength = Mathf.Clamp(command.payload.noiseStrength, 0f, 1f);
                            }
                            success = true; message = "Disturbance config applied.";
                        }
                        
                        var jitter = FindFirstObjectByType<CameraJitterController>();
                        if (jitter != null)
                        {
                            jitter.enableCameraJitter = command.payload.cameraJitterEnabled;
                            float magnitude = Mathf.Clamp(command.payload.cameraJitterMagnitude, 0f, 20f);
                            jitter.maxJitterXPixels = magnitude;
                            jitter.maxJitterYPixels = magnitude;
                        }
                        
                        var plat = FindFirstObjectByType<PlatformMotionController>();
                        if (plat != null)
                        {
                            plat.enablePlatformMotion = (command.payload.platformMotionMode != "Static");
                            if (Enum.TryParse(command.payload.platformMotionMode, out FSC.Core.PlatformMotionMode pMode))
                            {
                                plat.motionMode = pMode;
                            }
                            float platMag = Mathf.Clamp(command.payload.platformMotionMagnitude, 0f, 20f);
                            plat.maxPlatformMotionXPixels = platMag;
                            plat.maxPlatformMotionYPixels = platMag;
                        }
                        break;

                    // ATMOSPHERE
                    case "atmosphere.setConfig":
                        var atmos = FindFirstObjectByType<AtmosphereProcessor>();
                        if (atmos != null)
                        {
                            atmos.enableAtmosphere = command.payload.atmosphereEnabled;
                            if (Enum.TryParse(command.payload.atmosphereMode, out AtmosphereMode aMode))
                            {
                                atmos.atmosphereMode = aMode;
                            }
                            atmos.intensity = command.payload.atmosphereIntensity;
                            atmos.contrast = command.payload.atmosphereContrast;
                            atmos.brightness = command.payload.atmosphereBrightness;
                            atmos.hazeAmount = command.payload.hazeAmount;
                            atmos.fogAmount = command.payload.fogAmount;
                            atmos.rainAmount = command.payload.rainAmount;
                            atmos.randomSeed = command.payload.atmosphereSeed;
                            success = true; message = "Atmosphere config applied.";
                        }
                        break;

                    // PTZ
                    case "ptz.setConfig":
                        var ptz = FindFirstObjectByType<PanTiltTracker>();
                        if (ptz != null)
                        {
                            float absMax = 10f; // Global fallback limit
                            if (PSConfiguration.Instance != null) absMax = PSConfiguration.Instance.MaximumAllowedPTZSpeed;
                            
                            ptz.panSpeed = Mathf.Clamp(command.payload.maxPanSpeed, 0f, absMax);
                            ptz.tiltSpeed = Mathf.Clamp(command.payload.maxTiltSpeed, 0f, absMax);
                            success = true; message = "PTZ config applied.";
                        }
                        break;
                }

                if (success)
                {
                    status = "APPLIED";
                    _stateRevision++;
                }
            }
            catch (Exception ex)
            {
                status = "ERROR";
                message = ex.Message;
            }

            Debug.Log($"[UNITY] command {status}: {command.commandId} - {message}");

            // Send ACK back
            SendAck(command.commandId, status, message, downloadFileName, downloadContent);
        }

        private void ResetBenchmarkTrackingState()
        {
            var monitor = FindFirstObjectByType<PerformanceMonitor>();
            if (monitor != null) monitor.ResetBenchmark();

            var supervisor = FindFirstObjectByType<TrackingSupervisor>();
            if (supervisor != null) supervisor.ResetState();

            var metrics = FindFirstObjectByType<TrackingMetrics>();
            if (metrics != null) metrics.ResetAll();
        }

        private async void SendAck(string commandId, string status, string message, string downloadFileName, string downloadContent)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;

            var res = new CommandResponse
            {
                commandId = commandId,
                status = status,
                message = message,
                downloadFileName = downloadFileName,
                downloadContent = downloadContent,
                stateRevision = _stateRevision
            };

            string json = JsonUtility.ToJson(res);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);
            
            try
            {
                await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CommandClient] Failed to send ACK: {ex.Message}");
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
                _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client stopped", CancellationToken.None);
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
    }
}
