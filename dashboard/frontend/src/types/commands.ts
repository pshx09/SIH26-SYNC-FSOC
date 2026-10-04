export interface UnityConfigSnapshot {
  runState: number; // 0=STOPPED, 1=RUNNING, 2=PAUSED
  
  trajectoryMode: string;
  
  straightLineStartX: number;
  straightLineStartY: number;
  straightLineStartZ: number;
  straightLineDirX: number;
  straightLineDirY: number;
  straightLineDirZ: number;
  straightLineSpeed: number;
  straightLineRange: number;

  orbitCenterX: number;
  orbitCenterY: number;
  orbitCenterZ: number;
  orbitRadius: number;
  orbitAltitude: number;
  orbitAngularSpeed: number;
  orbitDirection: number;

  figure8AmplitudeX: number;
  figure8AmplitudeY: number;
  figure8Period: number;
  figure8MaxSpeed: number;

  randomBoundsX: number;
  randomBoundsY: number;
  randomSpeed: number;
  randomSeed: number;

  targetType: string;
  targetCount: number;
  targetShape: string;
  targetSize: number;
  initialPositionX: number;
  initialPositionY: number;
  initialPositionZ: number;

  cameraResolution: string;
  hfov: number;
  vfov: number;
  cameraUpdateRate: number;

  maxPanSpeed: number;
  maxTiltSpeed: number;
  ptzUpdateRate: number;

  atmosphereEnabled: boolean;
  atmosphereMode: string;
  atmosphereIntensity: number;
  atmosphereContrast: number;
  atmosphereBrightness: number;
  hazeAmount: number;
  fogAmount: number;
  rainAmount: number;
  atmosphereSeed: number;

  disturbanceEnabled: boolean;
  noiseType: string;
  noiseStrength: number;
  cameraJitterEnabled: boolean;
  cameraJitterMagnitude: number;
  platformMotionMode: string;
  platformMotionMagnitude: number;
}
