import type { DashboardTelemetry } from '../../types/telemetry';

export type EvalStatus = 'PASS' | 'FAIL' | 'NOT AVAILABLE' | 'NOT TESTED' | 'WAITING' | 'INVALID';

export interface Requirement {
  id: string;
  category: string;
  label: string;
  unit: string;
  evaluate: (telemetry: DashboardTelemetry | null) => { actual: number | string | null; status: EvalStatus; limitLabel: string };
}

export const psRequirements: Requirement[] = [
  // SENSOR / CAMERA
  {
    id: 'SEN-001',
    category: 'SENSOR/CAMERA',
    label: 'Sensor Resolution',
    unit: '',
    evaluate: (t) => {
      if (!t || !t.resolution) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '640x480' };
      return { actual: t.resolution, status: t.resolution === '640x480' ? 'PASS' : 'FAIL', limitLabel: '640x480' };
    }
  },
  {
    id: 'SEN-002',
    category: 'SENSOR/CAMERA',
    label: 'HFOV',
    unit: '°',
    evaluate: (t) => {
      if (!t || t.hfov <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '<= 4.0' };
      return { actual: t.hfov, status: t.hfov <= 4.0 ? 'PASS' : 'FAIL', limitLabel: '<= 4.0' };
    }
  },
  {
    id: 'SEN-003',
    category: 'SENSOR/CAMERA',
    label: 'VFOV',
    unit: '°',
    evaluate: (t) => {
      if (!t || t.vfov <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '<= 3.0' };
      return { actual: t.vfov, status: t.vfov <= 3.0 ? 'PASS' : 'FAIL', limitLabel: '<= 3.0' };
    }
  },
  {
    id: 'SEN-004',
    category: 'SENSOR/CAMERA',
    label: 'Camera Update Rate',
    unit: 'Hz',
    evaluate: (t) => {
      if (!t || t.cameraFrameRate <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '>= 20.0' };
      return { actual: t.cameraFrameRate, status: t.cameraFrameRate >= 20.0 ? 'PASS' : 'FAIL', limitLabel: '>= 20.0' };
    }
  },
  
  // TARGET / BEACON
  {
    id: 'TGT-001',
    category: 'TARGET/BEACON',
    label: 'Target Type',
    unit: '',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: 'Beacon Spot' };
      return { actual: 'Beacon Spot', status: 'PASS', limitLabel: 'Beacon Spot' };
    }
  },
  {
    id: 'TGT-002',
    category: 'TARGET/BEACON',
    label: 'Number of Targets',
    unit: '',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '1 (Mandatory)' };
      return { actual: t.configSnapshot.targetCount || 1, status: t.configSnapshot.targetCount === 1 ? 'PASS' : 'FAIL', limitLabel: '1 (Mandatory)' };
    }
  },
  {
    id: 'TGT-003',
    category: 'TARGET/BEACON',
    label: 'Target Shape',
    unit: '',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: 'Square (Default)' };
      return { actual: t.configSnapshot.targetShape || 'Square', status: 'PASS', limitLabel: 'User-defined' };
    }
  },
  {
    id: 'TGT-004',
    category: 'TARGET/BEACON',
    label: 'Target Size',
    unit: 'px',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '5–20 × 5–20' };
      const size = t.configSnapshot.targetSize || 10;
      return { actual: `${size}x${size}`, status: (size >= 5 && size <= 20) ? 'PASS' : 'FAIL', limitLabel: '5–20 × 5–20' };
    }
  },
  
  // TRAJECTORY
  {
    id: 'TRJ-001',
    category: 'TRAJECTORY',
    label: 'Straight Line',
    unit: '',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: 'Supported' };
      return { actual: t.configSnapshot.trajectoryMode === 'StraightLine' ? 'ACTIVE' : 'INACTIVE', status: t.configSnapshot.trajectoryMode === 'StraightLine' ? 'PASS' : 'NOT TESTED', limitLabel: 'Supported' };
    }
  },
  {
    id: 'TRJ-002',
    category: 'TRAJECTORY',
    label: 'Circular',
    unit: '',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: 'Supported' };
      return { actual: t.configSnapshot.trajectoryMode === 'Circular' ? 'ACTIVE' : 'INACTIVE', status: t.configSnapshot.trajectoryMode === 'Circular' ? 'PASS' : 'NOT TESTED', limitLabel: 'Supported' };
    }
  },
  {
    id: 'TRJ-003',
    category: 'TRAJECTORY',
    label: 'Figure-8',
    unit: '',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: 'Supported' };
      return { actual: t.configSnapshot.trajectoryMode === 'Figure8' ? 'ACTIVE' : 'INACTIVE', status: t.configSnapshot.trajectoryMode === 'Figure8' ? 'PASS' : 'NOT TESTED', limitLabel: 'Supported' };
    }
  },
  {
    id: 'TRJ-004',
    category: 'TRAJECTORY',
    label: 'Random',
    unit: '',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: 'Supported' };
      return { actual: t.configSnapshot.trajectoryMode === 'Random' ? 'ACTIVE' : 'INACTIVE', status: t.configSnapshot.trajectoryMode === 'Random' ? 'PASS' : 'NOT TESTED', limitLabel: 'Supported' };
    }
  },

  // PTZ
  {
    id: 'PTZ-001',
    category: 'PTZ',
    label: 'Max Pan Speed',
    unit: '°/s',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '5–10' };
      const val = t.configSnapshot.maxPanSpeed || 5;
      return { actual: val, status: (val >= 5 && val <= 10) ? 'PASS' : 'FAIL', limitLabel: '5–10' };
    }
  },
  {
    id: 'PTZ-002',
    category: 'PTZ',
    label: 'Max Tilt Speed',
    unit: '°/s',
    evaluate: (t) => {
      if (!t || !t.configSnapshot) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '5–10' };
      const val = t.configSnapshot.maxTiltSpeed || 5;
      return { actual: val, status: (val >= 5 && val <= 10) ? 'PASS' : 'FAIL', limitLabel: '5–10' };
    }
  },
  {
    id: 'PTZ-003',
    category: 'PTZ',
    label: 'PTZ Update Rate',
    unit: 'Hz',
    evaluate: (t) => {
      if (!t || t.ptzUpdateRate <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '>= 20.0' };
      return { actual: t.ptzUpdateRate, status: t.ptzUpdateRate >= 20.0 ? 'PASS' : 'FAIL', limitLabel: '>= 20.0' };
    }
  },

  // TRACKING PERFORMANCE
  {
    id: 'TRK-001',
    category: 'TRACKING PERFORMANCE',
    label: 'Acquisition Time',
    unit: 's',
    evaluate: (t) => {
      if (!t || t.acquisitionTime <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '<= 2.0' };
      return { actual: t.acquisitionTime, status: t.acquisitionTime <= 2.0 ? 'PASS' : 'FAIL', limitLabel: '<= 2.0' };
    }
  },
  {
    id: 'TRK-002',
    category: 'TRACKING PERFORMANCE',
    label: 'Tracking Error',
    unit: 'px',
    evaluate: (t) => {
      if (!t || t.meanError <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '<= 10.0' };
      return { actual: t.meanError, status: t.meanError <= 10.0 ? 'PASS' : 'FAIL', limitLabel: '<= 10.0' };
    }
  },
  {
    id: 'TRK-003',
    category: 'TRACKING PERFORMANCE',
    label: 'Target Loss Rate',
    unit: '%',
    evaluate: (t) => {
      if (!t || t.timestamp <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '< 5.0' };
      return { actual: t.targetLossRate, status: t.targetLossRate < 5.0 ? 'PASS' : 'FAIL', limitLabel: '< 5.0' };
    }
  },
  {
    id: 'TRK-004',
    category: 'TRACKING PERFORMANCE',
    label: 'Re-acquisition Time',
    unit: 's',
    evaluate: (t) => {
      if (!t || t.reacquisitionTime <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '<= 1.0' };
      return { actual: t.reacquisitionTime, status: t.reacquisitionTime <= 1.0 ? 'PASS' : 'FAIL', limitLabel: '<= 1.0' };
    }
  },
  {
    id: 'TRK-005',
    category: 'TRACKING PERFORMANCE',
    label: 'Processing FPS',
    unit: 'Hz',
    evaluate: (t) => {
      if (!t || t.fps <= 0) return { actual: 'WAITING', status: 'NOT AVAILABLE', limitLabel: '>= 20.0' };
      return { actual: t.fps, status: t.fps >= 20.0 ? 'PASS' : 'FAIL', limitLabel: '>= 20.0' };
    }
  }
];
