import { useBinaryImageStream } from '../services/useBinaryImageStream';
import { backendWebSocketUrl } from '../services/backendUrls';
import { commandChannel } from '../services/CommandChannel';
import { useTelemetryData } from '../services/TelemetryStore';
import { useState } from 'react';

export function LiveSimulationView() {
  const { imageSrc, status, fps, frameSize } = useBinaryImageStream(backendWebSocketUrl('/ws/simulation-view'));
  const telemetry = useTelemetryData();
  const runState = telemetry?.runState || 'UNKNOWN';
  const [pendingCmd, setPendingCmd] = useState('');

  const handleCommand = async (cmd: string) => {
    setPendingCmd(cmd);
    try {
      await commandChannel.sendCommand(`simulation.${cmd}`, {});
    } catch (e) {
      console.error(e);
    }
    setPendingCmd('');
  };

  return (
    <div style={{ height: '100%', width: '100%', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)' }}>
      <div style={{ padding: '4px 8px', borderBottom: '1px solid var(--color-border)', backgroundColor: 'var(--color-bg-panel-header)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '10px', fontWeight: 600, color: 'var(--color-text-secondary)', letterSpacing: '0.1em' }}>
          <span>3D VIRTUAL ENVIRONMENT</span>
          <div style={{ display: 'flex', gap: '4px', marginLeft: '12px' }}>
            <button 
              className={`mission-button ${runState === 'RUNNING' ? 'active-run' : ''}`}
              onClick={() => handleCommand('start')} 
              disabled={pendingCmd !== '' || runState === 'RUNNING'}
            >▶ RUN</button>
            <button 
              className={`mission-button ${runState === 'PAUSED' ? 'active-pause' : ''}`}
              onClick={() => handleCommand('pause')} 
              disabled={pendingCmd !== '' || runState === 'PAUSED' || runState === 'STOPPED' || runState === 'OFFLINE' || runState === 'UNKNOWN'}
            >⏸ PAUSE</button>
            <button 
              className={`mission-button ${runState === 'STOPPED' ? 'active-stop' : ''}`}
              onClick={() => handleCommand('stop')} 
              disabled={pendingCmd !== '' || runState === 'STOPPED' || runState === 'OFFLINE' || runState === 'UNKNOWN'}
            >⏹ STOP</button>
            <button 
              className="mission-button" 
              onClick={() => handleCommand('reset')} 
              disabled={pendingCmd !== ''}
            >↺ RESET</button>
          </div>
        </div>
        
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', fontSize: '9px', fontFamily: 'var(--font-mono)' }}>
          <span className="text-muted">SOURCE <span className="text-primary">UNITY</span></span>
          <span className="text-muted">STATUS {status === 'LIVE' ? <span className="text-green">LIVE</span> : (status === 'STALE' ? <span className="text-amber">STALE</span> : <span className="text-red">OFFLINE</span>)}</span>
          <span className="text-muted">FPS <span className="text-primary">{fps}</span></span>
          <span className="text-muted">FRAME <span className="text-primary">{(frameSize / 1024).toFixed(1)} KB</span></span>
        </div>
      </div>
      
      <div style={{ padding: 0, backgroundColor: 'transparent', display: 'flex', justifyContent: 'center', alignItems: 'center', position: 'relative', overflow: 'hidden', flex: 1 }}>
          <div style={{ position: 'relative', width: '100%', height: '100%', display: 'flex', justifyContent: 'center', alignItems: 'center', padding: '4px' }}>
          <div style={{ position: 'relative', maxWidth: '100%', maxHeight: '100%', aspectRatio: '16/9', display: 'flex', border: '1px solid var(--color-border)' }}>
            {imageSrc ? (
              <img 
                src={imageSrc} 
                alt="Live 3D Simulation" 
                style={{ width: '100%', height: '100%', objectFit: 'contain', display: 'block' }}
              />
            ) : (
              <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, display: 'flex', justifyContent: 'center', alignItems: 'center' }} className="mono text-muted">
                WAITING FOR STREAM...
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
