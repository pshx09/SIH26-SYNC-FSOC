import type { DashboardMode } from '../../types/ui';
import { MonitorPlay, Target, Waves, Video, CheckSquare, LineChart, ScrollText, Settings } from 'lucide-react';

interface ModeBarProps {
  currentMode: DashboardMode;
  onModeChange: (mode: DashboardMode) => void;
}

const MODES: { id: DashboardMode; label: string; icon: any }[] = [
  { id: 'SIMULATION', label: 'SIMULATION', icon: MonitorPlay },
  { id: 'TRACKING', label: 'TRACKING', icon: Target },
  { id: 'DISTURBANCES', label: 'DISTURBANCES', icon: Waves },
  { id: 'VIDEO_INPUT', label: 'VIDEO INPUT (MP4)', icon: Video },
  { id: 'VERIFICATION', label: 'VERIFICATION', icon: CheckSquare },
  { id: 'ANALYSIS', label: 'ANALYSIS', icon: LineChart },
  { id: 'LOGS', label: 'LOGS', icon: ScrollText },
  { id: 'SETTINGS', label: 'SETTINGS', icon: Settings },
];

export function ModeBar({ currentMode, onModeChange }: ModeBarProps) {
  return (
    <div style={{
      display: 'flex',
      backgroundColor: 'var(--color-bg-base)',
      borderBottom: '1px solid var(--color-border)',
      padding: '0',
    }}>
      {MODES.map((mode) => {
        const isActive = currentMode === mode.id;
        return (
          <button
            key={mode.id}
            onClick={() => onModeChange(mode.id)}
            className={`nav-button ${isActive ? 'active' : ''}`}
          >
            {mode.label}
          </button>
        );
      })}
    </div>
  );
}
