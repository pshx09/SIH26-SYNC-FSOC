import { useTelemetryData } from '../../services/TelemetryStore';

export function MissionRail() {
  const t = useTelemetryData();

  const isLocked = t && t.trackingState === 'LOCKED';
  const isLive = t && t.inputMode === 0;

  const trackingStateText = t ? t.trackingState : '—';

  // Data Freshness
  const nowMs = Date.now();
  const isStale = t && t.receiveTimestamp ? (nowMs - t.receiveTimestamp) > 2000 : false;
  const stateText = !t ? 'OFFLINE' : (isStale ? 'STALE' : 'LIVE');
  const stateColor = !t ? 'var(--color-status-offline)' : (isStale ? 'var(--color-status-warn)' : 'var(--color-status-pass)');

  const formatAngle = (val: number) => `${val > 0 ? '+' : ''}${val.toFixed(2)}°`;
  const formatVelocity = (val: number) => `${val > 0 ? '+' : ''}${val.toFixed(1)}°/s`;

  const DataRow = ({ label, value, highlight, badge }: { label: string, value: string | React.ReactNode, highlight?: string, badge?: boolean }) => (
    <div className="data-row">
      <span className="data-label">{label}</span>
      {badge ? (
        <span className="data-value" style={{ 
          backgroundColor: highlight ? `${highlight}20` : 'transparent',
          color: highlight || 'var(--color-text-primary)',
          padding: '2px 6px',
          borderRadius: 'var(--radius-sm)',
          border: `1px solid ${highlight ? `${highlight}40` : 'var(--color-border-subtle)'}`,
          fontSize: '10px'
        }}>
          {value}
        </span>
      ) : (
        <span className="data-value" style={{ color: highlight || 'var(--color-text-primary)' }}>
          {value}
        </span>
      )}
    </div>
  );

  const SectionHeader = ({ title }: { title: string }) => (
    <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 600, color: 'var(--color-text-secondary)', padding: '6px 8px', borderBottom: '1px solid var(--color-border)', borderTop: '1px solid var(--color-border)', backgroundColor: 'var(--color-bg-panel-header)', letterSpacing: '0.05em' }}>
      {title}
    </div>
  );

  return (
    <div style={{ flex: '1 1 auto', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--color-bg-panel)', borderLeft: '1px solid var(--color-border)', height: '100%', overflowY: 'auto' }}>
      
      <div className="panel-header" style={{ borderTop: 'none' }}>MISSION LOGS & TELEMETRY</div>

      {/* SYSTEM & TELEMETRY */}
      <div style={{ flex: '0 0 auto', padding: '4px 8px 12px 8px' }}>
        <DataRow label="System State" value={`${stateText} ●`} highlight={stateColor} badge />
        <DataRow label="Tracking State" value={`${trackingStateText} ${isLocked ? '●' : ''}`} highlight={isLocked ? 'var(--color-status-pass)' : (t?.trackingState === 'SEARCHING' ? 'var(--color-status-warn)' : undefined)} badge />
        <DataRow label="Input Source" value={t ? (isLive ? 'SIMULATION' : 'MP4') : '—'} />
        <DataRow label="Uptime" value={t ? new Date(t.timestamp * 1000).toISOString().substr(11, 8) : '—'} />
      </div>

      {/* TARGET INFORMATION */}
      <SectionHeader title="TARGET INFORMATION" />
      <div style={{ flex: '0 0 auto', padding: '4px 8px 12px 8px' }}>
        <DataRow label="Target ID" value={t ? 'TGT-1' : '—'} />
        <DataRow label="Detected" value={t ? (t.isDetected ? 'YES ●' : 'NO') : '—'} highlight={t?.isDetected ? 'var(--color-status-pass)' : undefined} />
        <DataRow label="Centroid" value={t?.isDetected ? `(${t.centroidX.toFixed(0)}, ${t.centroidY.toFixed(0)})` : '—'} />
        <DataRow label="Bounding Box" value={t?.isDetected ? `${t.boundingBoxWidth.toFixed(0)} × ${t.boundingBoxHeight.toFixed(0)} px` : '—'} />
        <DataRow label="Confidence" value={t?.isDetected ? `${(t.detectionConfidence * 100).toFixed(1)} %` : '—'} />
        <DataRow label="Tracking Error" value={t?.isDetected ? `${t.radialError.toFixed(1)} px` : '—'} highlight={t?.isDetected ? (t.radialError > 10 ? 'var(--color-status-warn)' : 'var(--color-status-pass)') : undefined} />
      </div>

      {/* POINTING & TRACKING */}
      <SectionHeader title="POINTING & TRACKING" />
      <div style={{ flex: '0 0 auto', padding: '4px 8px 12px 8px' }}>
        <DataRow label="Pan Angle" value={t ? formatAngle(t.panAngle) : '—'} />
        <DataRow label="Tilt Angle" value={t ? formatAngle(t.tiltAngle) : '—'} />
        <DataRow label="Pan Velocity" value={t ? formatVelocity(t.panVelocity) : '—'} />
        <DataRow label="Tilt Velocity" value={t ? formatVelocity(t.tiltVelocity) : '—'} />
        <DataRow label="Max PTZ Speed" value={t ? `${t.maxPanLimit.toFixed(0)}°/s` : '—'} />
        <DataRow label="PTZ Update Rate" value={t ? (t.ptzUpdateRate > 0 ? `${t.ptzUpdateRate.toFixed(0)} Hz` : 'WAITING') : '—'} />
      </div>

      {/* ENVIRONMENT */}
      <SectionHeader title="ENVIRONMENT" />
      <div style={{ flex: '0 0 auto', padding: '4px 8px 12px 8px' }}>
        <DataRow label="Noise Type" value={t ? t.noiseType : '—'} />
        <DataRow label="Noise Strength" value={t ? (t.noiseType !== 'None' ? t.noiseStrength.toFixed(2) : 'N/A') : '—'} />
        <DataRow label="Atmosphere" value={t ? t.atmosphere : '—'} />
        <DataRow label="Platform Motion" value={t ? t.platformMotion : '—'} />
        <DataRow label="Camera Jitter" value={t ? t.cameraJitter : '—'} />
      </div>

      {/* CAMERA CONFIGURATION */}
      <SectionHeader title="CAMERA CONFIGURATION" />
      <div style={{ flex: '0 0 auto', padding: '4px 8px 16px 8px' }}>
        <DataRow label="Resolution" value={t && t.resolution ? t.resolution : '—'} />
        <DataRow label="Horizontal FOV" value={t && t.hfov > 0 ? `${t.hfov.toFixed(1)}°` : '—'} />
        <DataRow label="Vertical FOV" value={t && t.vfov > 0 ? `${t.vfov.toFixed(1)}°` : '—'} />
        <DataRow label="Frame Rate" value={t && t.cameraFrameRate > 0 ? `≥${t.cameraFrameRate} Hz` : '—'} />
      </div>

    </div>
  );
}
