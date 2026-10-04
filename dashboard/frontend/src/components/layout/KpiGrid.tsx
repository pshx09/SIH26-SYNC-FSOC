import { useTelemetryData } from '../../services/TelemetryStore';

export function KpiGrid() {
  const t = useTelemetryData();

  const isOffline = !t || t.runState === 'UNKNOWN' || t.runState === 'OFFLINE';
  const isWaiting = t?.runState === 'WAITING';
  
  // if waiting or offline, we do not show measured values. if stopped, we show final values.
  const shouldShow = !isOffline && !isWaiting;

  const isMeasured = (val: number | undefined) => shouldShow && val !== undefined && val >= 0;
  const isStarted = shouldShow && t && t.lockRetention >= 0;

  // Rule checks
  const getAcqColor = (val: number) => val <= 2.0 ? 'var(--color-status-pass)' : (val <= 2.5 ? 'var(--color-status-warn)' : 'var(--color-status-fail)');
  const getReacqColor = (val: number) => val <= 1.0 ? 'var(--color-status-pass)' : (val <= 1.5 ? 'var(--color-status-warn)' : 'var(--color-status-fail)');
  const getErrorColor = (val: number) => val <= 8.0 ? 'var(--color-status-pass)' : (val <= 10.0 ? 'var(--color-status-warn)' : 'var(--color-status-fail)');
  const getFpsColor = (val: number) => val >= 20.0 ? 'var(--color-status-pass)' : 'var(--color-status-fail)';

  const tileStyle = {
    backgroundColor: 'var(--color-bg-base)',
    border: '1px solid var(--color-border-subtle)',
    borderRadius: 'var(--radius-sm)',
    display: 'flex',
    flexDirection: 'column' as const,
    justifyContent: 'space-between',
    padding: '6px 8px',
    position: 'relative' as const,
    overflow: 'hidden'
  };

  const MetricTile = ({ label, value, color, unit, status }: { label: string, value: string, color: string, unit: string, status: string }) => (
    <div style={{ ...tileStyle, borderLeft: color !== 'var(--color-text-muted)' && color !== 'var(--color-text-primary)' ? `3px solid ${color}` : tileStyle.border }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
        <div style={{ fontSize: '10px', fontWeight: 600, letterSpacing: '0.05em', color: 'var(--color-text-secondary)', textTransform: 'uppercase' }}>
          {label}
        </div>
        <div style={{ fontSize: '9px', fontWeight: 600, color: color === 'var(--color-text-muted)' || color === 'var(--color-text-primary)' ? 'var(--color-text-muted)' : color, textTransform: 'uppercase', backgroundColor: color === 'var(--color-text-muted)' || color === 'var(--color-text-primary)' ? 'transparent' : `${color}20`, padding: '2px 4px', borderRadius: '2px' }}>
          {status}
        </div>
      </div>
      
      <div style={{ display: 'flex', alignItems: 'baseline', gap: '4px' }}>
        <div style={{ fontSize: '26px', fontFamily: 'var(--font-mono)', fontWeight: 'bold', color: color, lineHeight: 1 }}>
          {value}
        </div>
        <div style={{ fontSize: '12px', fontFamily: 'var(--font-mono)', color: 'var(--color-text-muted)', fontWeight: 600 }}>
          {unit}
        </div>
      </div>
    </div>
  );

  return (
    <div className="panel" style={{ flex: 1, backgroundColor: 'var(--color-bg-panel)' }}>
      <div className="panel-header">REAL-TIME TELEMETRY & PERFORMANCE</div>
      <div style={{ padding: '8px', flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', gap: '8px' }}>
        
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', flex: 1 }}>
          <MetricTile 
            label="ACQ TIME" 
            value={isMeasured(t?.acquisitionTime) ? t!.acquisitionTime.toFixed(2) : '--.--'} 
            unit="s"
            color={isMeasured(t?.acquisitionTime) ? getAcqColor(t!.acquisitionTime) : 'var(--color-text-muted)'}
            status={isMeasured(t?.acquisitionTime) ? (t!.acquisitionTime <= 2.0 ? 'NOMINAL' : 'WARN') : 'WAIT'}
          />
          <MetricTile 
            label="LOCK RETENTION" 
            value={isStarted ? t!.lockRetention.toFixed(1) : '--.-'} 
            unit="%"
            color={isStarted ? 'var(--color-text-primary)' : 'var(--color-text-muted)'}
            status={isStarted ? 'ACTIVE' : 'WAIT'}
          />
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', flex: 1 }}>
          <MetricTile 
            label="TRACKING ERR" 
            value={t?.isDetected ? t.meanError.toFixed(2) : '--.--'} 
            unit="px"
            color={t?.isDetected ? getErrorColor(t.meanError) : 'var(--color-text-muted)'}
            status={t?.isDetected ? (t.meanError <= 8.0 ? 'NOMINAL' : 'WARN') : 'WAIT'}
          />
          <MetricTile 
            label="MAX ERROR" 
            value={isMeasured(t?.maxError) ? t!.maxError.toFixed(2) : '--.--'} 
            unit="px"
            color={isMeasured(t?.maxError) ? 'var(--color-text-primary)' : 'var(--color-text-muted)'}
            status={isMeasured(t?.maxError) ? 'ACTIVE' : 'WAIT'}
          />
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', flex: 1 }}>
          <MetricTile 
            label="RE-ACQUISITION" 
            value={isMeasured(t?.reacquisitionTime) ? t!.reacquisitionTime.toFixed(2) : '--.--'} 
            unit="s"
            color={isMeasured(t?.reacquisitionTime) ? getReacqColor(t!.reacquisitionTime) : 'var(--color-text-muted)'}
            status={isMeasured(t?.reacquisitionTime) ? (t!.reacquisitionTime <= 1.0 ? 'NOMINAL' : 'WARN') : 'NO EVT'}
          />
          <MetricTile 
            label="PROCESS SPEED" 
            value={isMeasured(t?.fps) ? t!.fps.toFixed(1) : '--.-'} 
            unit="Hz"
            color={isMeasured(t?.fps) ? getFpsColor(t!.fps) : 'var(--color-text-muted)'}
            status={isMeasured(t?.fps) ? (t!.fps >= 20.0 ? 'NOMINAL' : 'WARN') : 'WAIT'}
          />
        </div>

      </div>
    </div>
  );
}
