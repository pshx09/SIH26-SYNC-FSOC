import type { UnityConfigSnapshot } from './commands';

export type TrackingStatus = 'SEARCHING' | 'ACQUIRING' | 'LOCKED' | 'PREDICTING' | 'LOST' | 'REACQUIRING';

// 1. The Raw Unity Payload (PascalCase)
export interface RawUnityTelemetry {
  Timestamp: number;
  RunId: number;
  RunState: string;
  DetectorType: number;
  IsDetected: boolean;
  DetectionConfidence: number;
  CentroidX: number;
  CentroidY: number;
  BoundingBoxWidth: number;
  BoundingBoxHeight: number;
  TrackingState: number; // 0..5
  
  ErrorX: number;
  ErrorY: number;
  RadialError: number;
  MeanError: number;
  RMSE: number;
  MaxError: number;
  
  FramesWithin10px: number;
  TargetLossRate: number;
  LockRetention: number;
  AcquisitionTime: number;
  ReacquisitionTime: number;

  FPS: number;
  FrameTimeMs: number;
  DetectorInferenceTimeMs: number;
  PTZUpdateRate: number;

  PanAngle: number;
  TiltAngle: number;
  PanVelocity: number;
  TiltVelocity: number;
  MaxPanLimit: number;
  MinPanLimit: number;
  MaxTiltLimit: number;
  MinTiltLimit: number;
  
  InputMode: number;
  VideoFrame: number;
  VideoTime: number;
  VideoDuration: number;
  VideoFrameRate: number;
  VideoFrameCount: number;
  VideoIsPlaying: boolean;
  TrajectoryScenario: string;
  Resolution: string;
  HFOV: number;
  VFOV: number;
  CameraFrameRate: number;
  BeaconSize: number;
  NoiseType: string;
  NoiseStrength: number;
  Atmosphere: string;
  CameraJitter: string;
  PlatformMotion: string;
  RecentEvents?: string[];
  ConfigSnapshot?: UnityConfigSnapshot;
}

export interface DashboardTelemetry {
  timestamp: number;
  receiveTimestamp?: number; // Added locally by TelemetryStore
  runId: number;
  runState: string;
  
  isDetected: boolean;
  detectionConfidence: number;
  centroidX: number;
  centroidY: number;
  boundingBoxWidth: number;
  boundingBoxHeight: number;
  trackingState: TrackingStatus;
  
  errorX: number;
  errorY: number;
  radialError: number;
  meanError: number;
  rmse: number;
  maxError: number;
  
  targetLossRate: number;
  lockRetention: number;
  acquisitionTime: number;
  reacquisitionTime: number;

  fps: number;
  ptzUpdateRate: number;

  panAngle: number;
  tiltAngle: number;
  panVelocity: number;
  tiltVelocity: number;
  maxPanLimit: number;
  maxTiltLimit: number;
  
  inputMode: number;
  videoFrame: number;
  videoTime: number;
  videoDuration: number;
  videoFrameRate: number;
  videoFrameCount: number;
  videoIsPlaying: boolean;
  trajectoryScenario: string;
  resolution: string;
  hfov: number;
  vfov: number;
  cameraFrameRate: number;
  noiseType: string;
  noiseStrength: number;
  atmosphere: string;
  cameraJitter: string;
  platformMotion: string;
  recentEvents?: string[];
  configSnapshot?: UnityConfigSnapshot;
}
