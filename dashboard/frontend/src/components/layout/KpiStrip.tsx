import { useTelemetryData } from '../../services/TelemetryStore';


export function KpiStrip() {
  const t = useTelemetryData();

  const isOffline = !t || t.runState === 'UNKNOWN' || t.runState === 'OFFLINE';
  const isWaiting = t?.runState === 'WAITING';
  const shouldShow = !isOffline && !isWaiting;

  const isMeasured = (val: number | undefined) => shouldShow && val !== undefined && val >= 0;
  const isStarted = shouldShow && t && t.lockRetention >= 0;

  const metrics = [
    { 
      label: 'ACQUISITION', 
      value: isMeasured(t?.acquisitionTime) ? `${t!.acquisitionTime.toFixed(2)}s` : 'WAITING', 
      color: isMeasured(t?.acquisitionTime) ? (t!.acquisitionTime <= 2.0 ? 'var(--color-status-pass)' : (t!.acquisitionTime <= 2.5 ? 'var(--color-status-warn)' : 'var(--color-status-fail)')) : 'var(--color-text-muted)',
      status: isMeasured(t?.acquisitionTime) ? (t!.acquisitionTime <= 2.0 ? 'PASS' : 'FAIL') : ''
    },
    { 
      label: 'RE-ACQUISITION', 
      value: isMeasured(t?.reacquisitionTime) ? `${t!.reacquisitionTime.toFixed(2)}s` : '—', 
      color: isMeasured(t?.reacquisitionTime) ? (t!.reacquisitionTime <= 1.0 ? 'var(--color-status-pass)' : (t!.reacquisitionTime <= 1.5 ? 'var(--color-status-warn)' : 'var(--color-status-fail)')) : 'var(--color-text-muted)',
      status: ''
    },
    { 
      label: 'TRACKING ERROR', 
      value: t?.isDetected ? `${t.radialError.toFixed(1)} px` : 'WAITING', 
      color: t?.isDetected ? (t.radialError <= 10.0 ? 'var(--color-status-pass)' : (t.radialError <= 15.0 ? 'var(--color-status-warn)' : 'var(--color-status-fail)')) : 'var(--color-text-muted)',
      status: t?.isDetected ? (t.radialError <= 10.0 ? 'NOMINAL' : 'WARN') : ''
    },
    { 
      label: 'MAX ERROR', 
      value: isMeasured(t?.maxError) ? `${t!.maxError.toFixed(1)} px` : 'WAITING', 
      color: isMeasured(t?.maxError) ? 'var(--color-text-primary)' : 'var(--color-text-muted)',
      status: ''
    },
    { 
      label: 'LOCK RETENTION', 
      value: isStarted ? `${t!.lockRetention.toFixed(1)}%` : 'WAITING', 
      color: isStarted ? 'var(--color-text-primary)' : 'var(--color-text-muted)',
      status: ''
    },
    { 
      label: 'PROCESSING FPS', 
      value: isMeasured(t?.fps) ? `${t!.fps.toFixed(1)}` : 'WAITING', 
      color: isMeasured(t?.fps) ? (t!.fps >= 20.0 ? 'var(--color-status-pass)' : (t!.fps >= 15.0 ? 'var(--color-status-warn)' : 'var(--color-status-fail)')) : 'var(--color-text-muted)',
      status: isMeasured(t?.fps) ? (t!.fps >= 20.0 ? 'NOMINAL' : 'LOW') : ''
    }
  ];

  return (
    <div style={{ flex: '0 0 auto', borderBottom: '1px solid var(--color-border)', backgroundColor: 'var(--color-bg-base)', padding: '20px 32px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        {metrics.map((m, i) => (
          <div key={i} style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span style={{ fontSize: '12px', color: 'var(--color-text-muted)', letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600 }}>{m.label}</span>
              {m.status && <span style={{ fontSize: '10px', padding: '2px 4px', borderRadius: '4px', backgroundColor: m.color, color: '#000', fontWeight: 'bold' }}>{m.status}</span>}
            </div>
            <span style={{ fontSize: '36px', fontFamily: 'var(--font-mono)', fontWeight: 'bold', color: m.color, lineHeight: 1 }}>{m.value}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
