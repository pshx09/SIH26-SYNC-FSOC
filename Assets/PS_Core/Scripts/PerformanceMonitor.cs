using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

namespace FSC.Core
{
    public enum BenchmarkStatus
    {
        Idle,
        Running,
        Completed,
        Paused
    }

    /// <summary>
    /// Measures and logs performance statistics of the FSOC tracking loop independently.
    /// Exports sample data and summary statistics to CSV upon completion.
    /// This module is completely passive and only records benchmark-relative data.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("The core TrackingMetrics module supplying the image-space measurements.")]
        public TrackingMetrics trackingMetrics;

        [Tooltip("The detector module supplying the authoritative detection boolean.")]
        public BeaconDetector beaconDetector;

        [Header("BENCHMARK CONTROL")]
        [Tooltip("Automatically start the benchmark when entering Play Mode, before first lock.")]
        [SerializeField] private bool autoStartBenchmarkOnPlay = true;

        [Tooltip("Current state of the benchmark session.")]
        public BenchmarkStatus benchmarkStatus = BenchmarkStatus.Idle;
        
        [Tooltip("How frequently (in seconds) to record a data sample. Default 0.05s = 20 Hz.")]
        public float samplingInterval = 0.05f;

        [Header("AUTO START DIAGNOSTICS (Read Only)")]
        [SerializeField] private bool autoStartEnabled = false;
        [SerializeField] private string autoStartTime = "N/A";

        [Header("LIVE PERFORMANCE (Read Only)")]
        [SerializeField] private float currentRuntimeFPS = 0f;
        [SerializeField] private float averageRuntimeFPS = 0f;
        [SerializeField] private float minimumRuntimeFPS = float.MaxValue;
        [SerializeField] private float maximumRuntimeFPS = 0f;
        [SerializeField] private TrackingState currentTrackingState;

        [Header("ERROR METRICS (Read Only)")]
        [SerializeField] private float currentErrorX = 0f;
        [SerializeField] private float currentErrorY = 0f;
        [SerializeField] private float currentRadialError = 0f;
        [SerializeField] private float meanRadialError = 0f;
        [SerializeField] private float maximumRadialError = 0f;
        [SerializeField] private float rmseRadialError = 0f;

        [Header("TRACKING PERFORMANCE (Read Only)")]
        [SerializeField] private float benchmarkAcquisitionTime = 0f;
        [SerializeField] private float averageReacquisitionTime = 0f;
        [SerializeField] private float worstReacquisitionTime = 0f;
        [SerializeField] private int benchmarkLostFrames = 0;
        [SerializeField] private int benchmarkDetectedFrames = 0;
        [SerializeField] private float lockRetentionPercent = 0f;
        [SerializeField] private float detectionAvailabilityPercent = 0f;

        // Internal State & Counters
        private struct BenchmarkSample
        {
            public float timestamp;
            public TrackingState state;
            public float errorX;
            public float errorY;
            public float radialError;
            public float runtimeFPS;
        }

        private List<BenchmarkSample> _samples = new List<BenchmarkSample>(10000);
        
        private float _benchmarkStartTime = 0f;
        private float _timeSinceLastSample = 0f;
        private float _pausedAt = 0f;
        private float _totalPausedTime = 0f;
        private float _completedElapsedTime = 0f;
        private TrackingState _previousState;
        
        private bool _hasAchievedFirstLock = false;
        private bool _isCurrentlyLost = false;
        private float _lastLossTime = 0f;
        
        private int _lockEvents = 0;
        private int _lossEvents = 0;
        private int _reacqEvents = 0;
        private float _totalReacqTime = 0f;

        private int _totalFramesCount = 0;
        private float _totalUnscaledTime = 0f;

        private int _totalSamples = 0;
        private int _detectedSamples = 0;
        private int _lostSamples = 0;
        private int _lockedSamples = 0;
        private int _validErrorSamplesCount = 0;
        
        private double _sumOfErrors = 0;
        private double _sumOfSquaredErrors = 0;

        private bool _hasAutoStarted = false;
        private string _latestSamplesPath;
        private string _latestSummaryPath;

        private void Update()
        {
            if (trackingMetrics == null || beaconDetector == null) return;

            if (autoStartBenchmarkOnPlay && !_hasAutoStarted)
            {
                _hasAutoStarted = true;
                autoStartEnabled = true;
                autoStartTime = Time.time.ToString("F2") + "s";
                StartBenchmark();
            }

            if (benchmarkStatus != BenchmarkStatus.Running) return;

            UpdateFPSMetrics();
            MonitorStateEvents();

            _timeSinceLastSample += Time.unscaledDeltaTime;
            if (_timeSinceLastSample >= samplingInterval)
            {
                RecordSample();
                _timeSinceLastSample = 0f;
            }

            UpdateInspectorMetrics();
        }

        private void MonitorStateEvents()
        {
            TrackingState state = trackingMetrics.CurrentState;
            if (state != _previousState)
            {
                // Entering LOCKED
                if (state == TrackingState.LOCKED)
                {
                    _lockEvents++;
                    
                    if (!_hasAchievedFirstLock)
                    {
                        benchmarkAcquisitionTime = GetBenchmarkTime();
                        _hasAchievedFirstLock = true;
                        Debug.Log($"[PerformanceMonitor] Benchmark Event: FIRST LOCK ACHIEVED at {benchmarkAcquisitionTime:F2}s");
                    }
                    else if (_isCurrentlyLost)
                    {
                        float reacqTime = GetBenchmarkTime() - _lastLossTime;
                        _reacqEvents++;
                        _totalReacqTime += reacqTime;
                        averageReacquisitionTime = _totalReacqTime / _reacqEvents;
                        if (reacqTime > worstReacquisitionTime) worstReacquisitionTime = reacqTime;
                        
                        _isCurrentlyLost = false;
                        Debug.Log($"[PerformanceMonitor] Benchmark Event: REACQUIRED LOCK at {GetBenchmarkTime():F2}s (Took: {reacqTime:F2}s)");
                    }
                }
                // Entering LOST
                else if (state == TrackingState.LOST && _previousState != TrackingState.LOST)
                {
                    _lossEvents++;
                    _lastLossTime = GetBenchmarkTime();
                    _isCurrentlyLost = true;
                    Debug.Log($"[PerformanceMonitor] Benchmark Event: TARGET LOST at {GetBenchmarkTime():F2}s");
                }

                _previousState = state;
            }
        }

        private void UpdateFPSMetrics()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                currentRuntimeFPS = 1.0f / dt;
                
                if (currentRuntimeFPS > maximumRuntimeFPS) maximumRuntimeFPS = currentRuntimeFPS;
                if (currentRuntimeFPS < minimumRuntimeFPS && _totalFramesCount > 5) minimumRuntimeFPS = currentRuntimeFPS; 
                
                _totalFramesCount++;
                _totalUnscaledTime += dt;
                averageRuntimeFPS = _totalFramesCount / _totalUnscaledTime;
            }
        }

        private void UpdateInspectorMetrics()
        {
            currentTrackingState = trackingMetrics.CurrentState;
            currentErrorX = trackingMetrics.ErrorX;
            currentErrorY = trackingMetrics.ErrorY;
            currentRadialError = trackingMetrics.RadialError;
        }

        private void RecordSample()
        {
            TrackingState state = trackingMetrics.CurrentState;
            float radial = trackingMetrics.RadialError;
            
            _totalSamples++;

            if (state == TrackingState.LOCKED)
            {
                _lockedSamples++;
            }

            if (beaconDetector != null && beaconDetector.isDetected)
            {
                _detectedSamples++;
            }
            else
            {
                _lostSamples++;
            }

            if (state != TrackingState.SEARCHING && state != TrackingState.LOST)
            {
                _validErrorSamplesCount++;
                _sumOfErrors += radial;
                _sumOfSquaredErrors += (radial * radial);
                
                if (radial > maximumRadialError) maximumRadialError = radial;
            }

            if (_validErrorSamplesCount > 0)
            {
                meanRadialError = (float)(_sumOfErrors / _validErrorSamplesCount);
                rmseRadialError = (float)Math.Sqrt(_sumOfSquaredErrors / _validErrorSamplesCount);
            }
            else
            {
                meanRadialError = 0f;
                rmseRadialError = 0f;
            }

            if (_totalSamples > 0)
            {
                lockRetentionPercent = ((float)_lockedSamples / _totalSamples) * 100f;
                detectionAvailabilityPercent = ((float)_detectedSamples / _totalSamples) * 100f;
            }

            benchmarkLostFrames = _lostSamples;
            benchmarkDetectedFrames = _detectedSamples;

            BenchmarkSample sample = new BenchmarkSample
            {
                timestamp = GetBenchmarkTime(),
                state = state,
                errorX = trackingMetrics.ErrorX,
                errorY = trackingMetrics.ErrorY,
                radialError = radial, // TrackingMetrics sets this to 0 if LOST. Kept raw for CSV.
                runtimeFPS = currentRuntimeFPS
            };
            
            _samples.Add(sample);
        }

        private float GetBenchmarkTime()
        {
            if (benchmarkStatus == BenchmarkStatus.Completed)
            {
                return _completedElapsedTime;
            }

            float endTime = benchmarkStatus == BenchmarkStatus.Paused ? _pausedAt : Time.unscaledTime;
            return Mathf.Max(0f, endTime - _benchmarkStartTime - _totalPausedTime);
        }

        [ContextMenu("Start Benchmark")]
        public void StartBenchmark()
        {
            if (trackingMetrics == null || beaconDetector == null)
            {
                Debug.LogError("[PerformanceMonitor] Cannot start benchmark: TrackingMetrics or BeaconDetector reference is missing.");
                return;
            }

            ResetBenchmarkInternal();
            
            _previousState = trackingMetrics.CurrentState;
            
            if (_previousState == TrackingState.LOCKED)
            {
                _hasAchievedFirstLock = true;
                Debug.LogWarning("[PerformanceMonitor] WARNING: Benchmark started while system is already LOCKED. This is NOT a valid acquisition benchmark. Please start the benchmark before first lock.");
            }

            _benchmarkStartTime = Time.unscaledTime;
            benchmarkStatus = BenchmarkStatus.Running;
            Debug.Log("[PerformanceMonitor] Benchmark STARTED.");
        }

        public void PauseBenchmark()
        {
            if (benchmarkStatus != BenchmarkStatus.Running) return;

            _pausedAt = Time.unscaledTime;
            benchmarkStatus = BenchmarkStatus.Paused;
            Debug.Log("[PerformanceMonitor] Benchmark PAUSED.");
        }

        public void ResumeBenchmark()
        {
            if (benchmarkStatus != BenchmarkStatus.Paused) return;

            _totalPausedTime += Time.unscaledTime - _pausedAt;
            benchmarkStatus = BenchmarkStatus.Running;
            Debug.Log("[PerformanceMonitor] Benchmark RESUMED.");
        }

        [ContextMenu("Stop Benchmark")]
        public void StopBenchmark()
        {
            if (benchmarkStatus != BenchmarkStatus.Running && benchmarkStatus != BenchmarkStatus.Paused) return;
            
            _completedElapsedTime = GetBenchmarkTime();
            benchmarkStatus = BenchmarkStatus.Completed;
            Debug.Log($"[PerformanceMonitor] Benchmark COMPLETED. Duration: {_completedElapsedTime:F2}s. Writing results...");
            
            ExportResults();
        }

        [ContextMenu("Reset Benchmark")]
        public void ResetBenchmark()
        {
            ResetBenchmarkInternal();
            benchmarkStatus = BenchmarkStatus.Idle;
            Debug.Log("[PerformanceMonitor] Benchmark reset to IDLE.");
        }

        private void ResetBenchmarkInternal()
        {
            _samples.Clear();
            _latestSamplesPath = null;
            _latestSummaryPath = null;
            _benchmarkStartTime = 0f;
            _timeSinceLastSample = 0f;
            _pausedAt = 0f;
            _totalPausedTime = 0f;
            _completedElapsedTime = 0f;
            
            _hasAchievedFirstLock = false;
            _isCurrentlyLost = false;
            _lastLossTime = 0f;
            currentTrackingState = TrackingState.SEARCHING;
            currentErrorX = 0f;
            currentErrorY = 0f;
            currentRadialError = 0f;
            _lockEvents = 0;
            _lossEvents = 0;
            _reacqEvents = 0;
            _totalReacqTime = 0f;

            _totalFramesCount = 0;
            _totalUnscaledTime = 0f;
            currentRuntimeFPS = 0f;
            averageRuntimeFPS = 0f;
            minimumRuntimeFPS = float.MaxValue;
            maximumRuntimeFPS = 0f;

            _totalSamples = 0;
            _detectedSamples = 0;
            _lostSamples = 0;
            _lockedSamples = 0;
            _validErrorSamplesCount = 0;
            _sumOfErrors = 0;
            _sumOfSquaredErrors = 0;
            
            meanRadialError = 0f;
            maximumRadialError = 0f;
            rmseRadialError = 0f;
            
            benchmarkAcquisitionTime = 0f;
            averageReacquisitionTime = 0f;
            worstReacquisitionTime = 0f;
            benchmarkLostFrames = 0;
            benchmarkDetectedFrames = 0;
            lockRetentionPercent = 0f;
            detectionAvailabilityPercent = 0f;
        }

        public bool TryGetSamplesCsv(out string fileName, out string content)
        {
            return TryReadExport(_latestSamplesPath, out fileName, out content);
        }

        public bool TryGetSummaryCsv(out string fileName, out string content)
        {
            return TryReadExport(_latestSummaryPath, out fileName, out content);
        }

        private bool TryReadExport(string path, out string fileName, out string content)
        {
            fileName = null;
            content = null;
            if (benchmarkStatus != BenchmarkStatus.Completed || string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            fileName = Path.GetFileName(path);
            content = File.ReadAllText(path);
            return true;
        }

        private void ExportResults()
        {
            if (_samples.Count == 0)
            {
                Debug.LogWarning("[PerformanceMonitor] No data collected to export.");
                return;
            }

            string dir = Path.Combine(Application.persistentDataPath, "FSOC_Benchmarks");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string timestampStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string samplesPath = Path.Combine(dir, $"Benchmark_Samples_{timestampStr}.csv");
            string summaryPath = Path.Combine(dir, $"Benchmark_Summary_{timestampStr}.csv");

            try
            {
                // Write Samples
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Timestamp,State,ErrorX,ErrorY,RadialError,RuntimeFPS");
                foreach (var sample in _samples)
                {
                    sb.AppendLine($"{sample.timestamp:F3},{sample.state},{sample.errorX:F2},{sample.errorY:F2},{sample.radialError:F2},{sample.runtimeFPS:F1}");
                }
                File.WriteAllText(samplesPath, sb.ToString());

                // Write Summary
                StringBuilder sum = new StringBuilder();
                sum.AppendLine("Total Duration,Acquisition Time,Mean Radial Error,Maximum Radial Error,RMSE Radial Error,Detection Availability,Lock Retention,Lost Frames,Average Reacquisition Time,Average Runtime FPS");
                
                sum.AppendLine(
                    $"{GetBenchmarkTime():F3}," +
                    $"{benchmarkAcquisitionTime:F3}," +
                    $"{meanRadialError:F2}," +
                    $"{maximumRadialError:F2}," +
                    $"{rmseRadialError:F2}," +
                    $"{detectionAvailabilityPercent:F2}," +
                    $"{lockRetentionPercent:F2}," +
                    $"{benchmarkLostFrames}," +
                    $"{averageReacquisitionTime:F3}," +
                    $"{averageRuntimeFPS:F2}"
                );
                
                File.WriteAllText(summaryPath, sum.ToString());

                _latestSamplesPath = samplesPath;
                _latestSummaryPath = summaryPath;
                Debug.Log($"[PerformanceMonitor] Benchmark successfully exported to:\n{samplesPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PerformanceMonitor] Failed to write benchmark data: {e.Message}");
            }
        }
    }
}
