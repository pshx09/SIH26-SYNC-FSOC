import { useSyncExternalStore } from 'react';
import type { RawUnityTelemetry, DashboardTelemetry, TrackingStatus } from '../types/telemetry';
import { backendWebSocketUrl } from './backendUrls';

type Listener = () => void;

function translateTrackingState(stateCode: number): TrackingStatus {
  switch (stateCode) {
    case 1: return 'ACQUIRING';
    case 2: return 'LOCKED';
    case 3: return 'PREDICTING';
    case 4: return 'REACQUIRING'; // 4 in C# is Reacquiring
    case 5: return 'LOST'; // 5 in C# is Lost
    case 0:
    default: return 'SEARCHING';
  }
}

function mapUnityTelemetryToUI(rawUnityData: RawUnityTelemetry): DashboardTelemetry {
  return {
    timestamp: rawUnityData.Timestamp || 0,
    runId: rawUnityData.RunId || 0,
    runState: rawUnityData.RunState || 'UNKNOWN',
    isDetected: Boolean(rawUnityData.IsDetected),
    detectionConfidence: rawUnityData.DetectionConfidence || 0,
    centroidX: rawUnityData.CentroidX || 0,
    centroidY: rawUnityData.CentroidY || 0,
    boundingBoxWidth: rawUnityData.BoundingBoxWidth || 0,
    boundingBoxHeight: rawUnityData.BoundingBoxHeight || 0,
    trackingState: translateTrackingState(rawUnityData.TrackingState),
    
    errorX: rawUnityData.ErrorX || 0,
    errorY: rawUnityData.ErrorY || 0,
    radialError: rawUnityData.RadialError || 0,
    meanError: rawUnityData.MeanError || 0,
    rmse: rawUnityData.RMSE || 0,
    maxError: rawUnityData.MaxError || 0,
    
    targetLossRate: rawUnityData.TargetLossRate || 0,
    lockRetention: rawUnityData.LockRetention || 0,
    acquisitionTime: rawUnityData.AcquisitionTime || 0,
    reacquisitionTime: rawUnityData.ReacquisitionTime || 0,
    
    fps: rawUnityData.FPS || 0,
    ptzUpdateRate: rawUnityData.PTZUpdateRate || 0,
    
    panAngle: rawUnityData.PanAngle || 0,
    tiltAngle: rawUnityData.TiltAngle || 0,
    panVelocity: rawUnityData.PanVelocity || 0,
    tiltVelocity: rawUnityData.TiltVelocity || 0,
    maxPanLimit: rawUnityData.MaxPanLimit || 0,
    maxTiltLimit: rawUnityData.MaxTiltLimit || 0,
    
    inputMode: rawUnityData.InputMode || 0,
    videoFrame: rawUnityData.VideoFrame || 0,
    videoTime: rawUnityData.VideoTime || 0,
    videoDuration: rawUnityData.VideoDuration || 0,
    videoFrameRate: rawUnityData.VideoFrameRate || 0,
    videoFrameCount: rawUnityData.VideoFrameCount || 0,
    videoIsPlaying: Boolean(rawUnityData.VideoIsPlaying),
    trajectoryScenario: rawUnityData.TrajectoryScenario || '',
    resolution: rawUnityData.Resolution || '',
    hfov: rawUnityData.HFOV || 0,
    vfov: rawUnityData.VFOV || 0,
    cameraFrameRate: rawUnityData.CameraFrameRate || 0,
    noiseType: rawUnityData.NoiseType || '',
    noiseStrength: rawUnityData.NoiseStrength || 0,
    atmosphere: rawUnityData.Atmosphere || '',
    cameraJitter: rawUnityData.CameraJitter || '',
    platformMotion: rawUnityData.PlatformMotion || '',
    recentEvents: rawUnityData.RecentEvents || [],
    configSnapshot: rawUnityData.ConfigSnapshot
  };
}

class TelemetryStore {
  private ws: WebSocket | null = null;
  private state: DashboardTelemetry | null = null;
  private history: DashboardTelemetry[] = [];
  private listeners: Set<Listener> = new Set();
  private isConnecting = false;
  
  // Maximum number of snapshots to keep in memory (e.g., 15 seconds at 15Hz = 225)
  private readonly MAX_HISTORY = 225;

  public connect(url: string = backendWebSocketUrl('/ws/dashboard')) {
    if (this.ws || this.isConnecting) return;
    this.isConnecting = true;

    this.ws = new WebSocket(url);

    this.ws.onopen = () => {
      this.isConnecting = false;
      this.notifyListeners();
    };

    this.ws.onmessage = (event) => {
      try {
        const rawData = JSON.parse(event.data) as RawUnityTelemetry;
        
        const incomingRunId = rawData.RunId || 0;
        const isOfflineMsg = rawData.RunState === 'OFFLINE';
        
        if (this.state && !isOfflineMsg && this.state.runState !== 'OFFLINE' && incomingRunId < this.state.runId) {
            return; // Reject stale snapshots from older RunIds
        }

        const translatedData = mapUnityTelemetryToUI(rawData);
        (translatedData as any).receiveTimestamp = Date.now();
        
        this.state = translatedData;
        
        this.history.push(this.state);
        if (this.history.length > this.MAX_HISTORY) {
          this.history.shift();
        }
        
        this.notifyListeners();
      } catch (e) {
        console.error('Error parsing telemetry:', e);
      }
    };

    this.ws.onclose = () => {
      this.ws = null;
      this.isConnecting = false;
      setTimeout(() => this.connect(url), 2000);
    };

    this.ws.onerror = () => {
      // Handled by onclose
    };
  }

  public getState = () => {
    return this.state;
  };

  public getHistory = () => {
    return this.history;
  };

  public getStatus = () => {
    return this.ws !== null && this.ws.readyState === WebSocket.OPEN;
  };

  public subscribe = (listener: Listener) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  private notifyListeners() {
    for (const listener of this.listeners) {
      listener();
    }
  }
}

export const telemetryStore = new TelemetryStore();

export function useTelemetryData() {
  return useSyncExternalStore(
    telemetryStore.subscribe,
    telemetryStore.getState,
    telemetryStore.getState
  );
}

export function useTelemetryHistory() {
  return useSyncExternalStore(
    telemetryStore.subscribe,
    telemetryStore.getHistory,
    telemetryStore.getHistory
  );
}

export function useTelemetryStatus() {
  return useSyncExternalStore(
    telemetryStore.subscribe,
    telemetryStore.getStatus,
    telemetryStore.getStatus
  );
}
