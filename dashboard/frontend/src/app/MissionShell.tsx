import { useState, useEffect } from 'react';
import type { DashboardMode } from '../types/ui';
import { GlobalHeader } from '../components/layout/GlobalHeader';
import { ModeBar } from '../components/layout/ModeBar';
import { SimulationMode } from '../components/modes/SimulationMode';
import { VerificationMode } from '../components/modes/VerificationMode';
import { AnalysisMode } from '../components/modes/AnalysisMode';
import { TrackingMode } from '../components/modes/TrackingMode';
import { LogsMode } from '../components/modes/LogsMode';
import { VideoInputMode } from '../components/modes/VideoInputMode';
import { telemetryStore } from '../services/TelemetryStore';
import { commandChannel } from '../services/CommandChannel';

export function MissionShell() {
  const [mode, setMode] = useState<DashboardMode>('SIMULATION');

  useEffect(() => {
    // Connect telemetry once at the top level
    telemetryStore.connect('ws://127.0.0.1:8000/ws/dashboard');
    commandChannel.connect('ws://127.0.0.1:8000/ws/commands/frontend');
  }, []);

  const renderMode = () => {
    switch (mode) {
      case 'SIMULATION':
        return <SimulationMode />;
      case 'VERIFICATION':
        return <VerificationMode />;
      case 'ANALYSIS':
        return <AnalysisMode />;
      case 'TRACKING':
        return <TrackingMode />;
      case 'LOGS':
        return <LogsMode />;
      case 'VIDEO_INPUT':
        return <VideoInputMode />;
      case 'DISTURBANCES':
      case 'SETTINGS':
      default:
        return (
          <div style={{ padding: '24px', color: 'var(--color-text-secondary)', display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%' }}>
            <h2>{mode} MODE NOT IMPLEMENTED IN THIS PHASE</h2>
          </div>
        );
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100vh', overflow: 'hidden' }}>
      <GlobalHeader currentMode={mode} />
      <ModeBar currentMode={mode} onModeChange={setMode} />
      
      <div style={{ flex: 1, display: 'flex', overflow: 'hidden', backgroundColor: 'var(--color-bg-base)' }}>
        {renderMode()}
      </div>
    </div>
  );
}
