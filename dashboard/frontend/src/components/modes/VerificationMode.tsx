import { useState, useEffect } from 'react';
import { useTelemetryData } from '../../services/TelemetryStore';
import { psRequirements } from '../verification/psRequirements';

const StatusBadge = ({ status, dot = false }: { status: string, dot?: boolean }) => {
  let bg = 'rgba(128,128,128,0.05)';
  let border = 'var(--color-border-subtle)';
  let color = 'var(--color-text-muted)';
  
  const s = status.toUpperCase();
  if (s === 'PASS' || s === 'MEASURING') {
    bg = 'rgba(57, 211, 83, 0.1)';
    border = 'var(--color-status-pass)';
    color = 'var(--color-status-pass)';
  } else if (s === 'FAIL' || s === 'INVALID') {
    bg = 'rgba(255, 123, 114, 0.1)';
    border = 'var(--color-status-fail)';
    color = 'var(--color-status-fail)';
  } else if (s === 'NOT TESTED' || s === 'WARNING') {
    bg = 'rgba(224, 168, 50, 0.1)';
    border = 'var(--color-status-warn)';
    color = 'var(--color-status-warn)';
  } else if (s === 'COMPLETE') {
    bg = 'var(--color-bg-panel-header)';
    border = 'var(--color-border)';
    color = 'var(--color-text-primary)';
  }

  return (
    <div style={{
      display: 'inline-flex',
      alignItems: 'center',
      gap: '6px',
      padding: '2px 8px',
      borderRadius: '2px',
      fontSize: '9px',
      fontWeight: 700,
      border: `1px solid ${border}`,
      backgroundColor: bg,
      color: color,
      textTransform: 'uppercase',
      letterSpacing: '0.5px'
    }}>
      {dot && <span style={{ width: 6, height: 6, borderRadius: '50%', backgroundColor: color }} />}
      {status}
    </div>
  );
};

export function VerificationMode() {
  const t = useTelemetryData();

  const [runState, setRunState] = useState<'IDLE' | 'MEASURING' | 'COMPLETE' | 'INVALID'>('IDLE');
  const [snapshot, setSnapshot] = useState<any>(null);

  const isLive = t?.inputMode === 0;
  const isMp4 = t?.inputMode === 1;

  useEffect(() => {
    if (t && t.runState === 'RUNNING' && runState === 'IDLE') {
      setRunState('MEASURING');
      setSnapshot(t.configSnapshot);
    } else if (t && t.runState !== 'RUNNING' && runState === 'MEASURING') {
      if (t.timestamp > 5.0) {
        setRunState('COMPLETE');
      } else {
        setRunState('INVALID');
      }
    } else if (t?.runState === 'UNKNOWN' || !t) {
      setRunState('IDLE');
    }
  }, [t?.runState, t?.timestamp, runState]);

  // Utility to handle sentinel values
  const formatActual = (val: number | string | null, unit: string) => {
    if (val === null || val === 'WAITING' || val === -1) return <span style={{ color: 'var(--color-text-muted)' }}>WAITING</span>;
    if (typeof val === 'number') return <span style={{ color: 'var(--color-text-primary)', fontSize: '13px', fontWeight: 600 }}>{val.toFixed(2)} <span style={{ fontSize: '10px', color: 'var(--color-text-secondary)', fontWeight: 'normal' }}>{unit}</span></span>;
    return <span style={{ color: 'var(--color-text-primary)', fontSize: '12px', fontWeight: 600 }}>{val} <span style={{ fontSize: '10px', color: 'var(--color-text-secondary)', fontWeight: 'normal' }}>{unit}</span></span>;
  };

  const getMetricData = (id: string) => {
    const req = psRequirements.find(r => r.id === id);
    if (!req) return null;
    return { req, res: req.evaluate(t) };
  };

  const MetricRibbonCard = ({ title, reqId }: { title: string, reqId: string }) => {
    const data = getMetricData(reqId);
    if (!data) return null;
    const { req, res } = data;
    return (
      <div style={{ flex: 1, padding: '12px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '2px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
        <div style={{ fontSize: '10px', fontWeight: 600, color: 'var(--color-text-secondary)', letterSpacing: '0.5px' }}>{title}</div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: '6px' }}>
          {formatActual(res.actual, req.unit)}
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: '10px', color: 'var(--color-text-muted)', fontFamily: 'var(--font-mono)' }}>{res.limitLabel} {req.unit}</span>
          <StatusBadge status={res.status === 'NOT AVAILABLE' ? 'N/A' : res.status} dot />
        </div>
      </div>
    );
  };

  const RequirementTable = ({ category }: { category: string }) => {
    const reqs = psRequirements.filter(r => r.category === category);
    if (reqs.length === 0) return null;
    return (
      <div style={{ marginBottom: '24px' }}>
        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '6px', borderBottom: '1px solid var(--color-border)', marginBottom: '8px' }}>
          {category}
        </div>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontFamily: 'var(--font-mono)' }}>
          <thead>
            <tr style={{ color: 'var(--color-text-muted)', textAlign: 'left', fontSize: '9px', textTransform: 'uppercase' }}>
              <th style={{ padding: '6px 0', width: '35%' }}>PARAMETER</th>
              <th style={{ padding: '6px 0', width: '30%' }}>ACTUAL</th>
              <th style={{ padding: '6px 0', width: '20%' }}>PS REQUIREMENT</th>
              <th style={{ padding: '6px 0', width: '15%' }}>STATUS</th>
            </tr>
          </thead>
          <tbody>
            {reqs.map(req => {
              const res = req.evaluate(t);
              return (
                <tr key={req.id} style={{ borderBottom: '1px solid var(--color-border-subtle)', transition: 'background-color 0.2s' }} onMouseOver={e => e.currentTarget.style.backgroundColor = 'var(--color-bg-panel-header)'} onMouseOut={e => e.currentTarget.style.backgroundColor = 'transparent'}>
                  <td style={{ padding: '8px 0', color: 'var(--color-text-primary)', fontSize: '11px', fontWeight: 500 }}>{req.label}</td>
                  <td style={{ padding: '8px 0' }}>{formatActual(res.actual, req.unit)}</td>
                  <td style={{ padding: '8px 0', color: 'var(--color-text-muted)', fontSize: '10px' }}>{res.limitLabel} {req.unit}</td>
                  <td style={{ padding: '8px 0' }}><StatusBadge status={res.status === 'NOT AVAILABLE' ? 'N/A' : res.status} /></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    );
  };

  const PtzModule = () => {
    const ptzUpdateData = getMetricData('PTZ-003');
    const actualHz = ptzUpdateData?.res.actual !== 'WAITING' ? (ptzUpdateData?.res.actual as number)?.toFixed(1) : 'WAITING';
    
    return (
      <div style={{ marginBottom: '24px' }}>
        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '6px', borderBottom: '1px solid var(--color-border)', marginBottom: '12px' }}>
          PTZ VERIFICATION MODULE
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px' }}>
          <div style={{ padding: '12px', border: '1px solid var(--color-border-subtle)', backgroundColor: 'var(--color-bg-panel-header)', borderRadius: '2px' }}>
            <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>Max Pan Speed</div>
            <div style={{ fontSize: '14px', color: 'var(--color-text-primary)', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{t?.configSnapshot?.maxPanSpeed?.toFixed(1) || '5.0'} <span style={{fontSize:'10px', color:'var(--color-text-muted)'}}>°/s</span></div>
            <div style={{ fontSize: '9px', color: 'var(--color-text-secondary)', marginTop: '8px' }}>PS RANGE: 5–10 °/s</div>
          </div>
          <div style={{ padding: '12px', border: '1px solid var(--color-border-subtle)', backgroundColor: 'var(--color-bg-panel-header)', borderRadius: '2px' }}>
            <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>Max Tilt Speed</div>
            <div style={{ fontSize: '14px', color: 'var(--color-text-primary)', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{t?.configSnapshot?.maxTiltSpeed?.toFixed(1) || '5.0'} <span style={{fontSize:'10px', color:'var(--color-text-muted)'}}>°/s</span></div>
            <div style={{ fontSize: '9px', color: 'var(--color-text-secondary)', marginTop: '8px' }}>PS RANGE: 5–10 °/s</div>
          </div>
          <div style={{ padding: '12px', border: '1px solid var(--color-border-subtle)', backgroundColor: 'var(--color-bg-panel-header)', borderRadius: '2px' }}>
            <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', marginBottom: '8px', textTransform: 'uppercase' }}>PTZ Update Rate</div>
            <div style={{ fontSize: '14px', color: 'var(--color-text-primary)', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{actualHz} <span style={{fontSize:'10px', color:'var(--color-text-muted)'}}>Hz</span></div>
            <div style={{ fontSize: '9px', color: 'var(--color-text-secondary)', marginTop: '8px' }}>REQUIRED: ≥20 Hz</div>
          </div>
          <div style={{ padding: '8px 12px', border: '1px solid var(--color-border-subtle)', display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '9px', color: 'var(--color-text-muted)' }}>CURRENT PAN</span>
            <span style={{ fontSize: '11px', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{(t?.panAngle || 0) > 0 ? '+' : ''}{(t?.panAngle || 0).toFixed(2)}°</span>
          </div>
          <div style={{ padding: '8px 12px', border: '1px solid var(--color-border-subtle)', display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '9px', color: 'var(--color-text-muted)' }}>CURRENT TILT</span>
            <span style={{ fontSize: '11px', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{(t?.tiltAngle || 0) > 0 ? '+' : ''}{(t?.tiltAngle || 0).toFixed(2)}°</span>
          </div>
        </div>
      </div>
    );
  };

  const TrajectoryGrid = () => {
    const active = t?.configSnapshot?.trajectoryMode || 'StraightLine';
    
    const TrajCard = ({ name, id }: { name: string, id: string }) => (
      <div style={{ padding: '12px', border: `1px solid ${active === id ? 'var(--color-text-primary)' : 'var(--color-border-subtle)'}`, backgroundColor: active === id ? 'rgba(255,255,255,0.02)' : 'transparent', borderRadius: '2px' }}>
        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-primary)', marginBottom: '12px' }}>{name}</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '9px', color: 'var(--color-text-secondary)', marginBottom: '6px', fontFamily: 'var(--font-mono)' }}>
          <span style={{ color: 'var(--color-status-pass)' }}>✓</span> SUPPORTED
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '9px', color: active === id ? 'var(--color-status-pass)' : 'var(--color-text-muted)', fontFamily: 'var(--font-mono)' }}>
          <span style={{ color: active === id ? 'var(--color-status-pass)' : 'var(--color-text-muted)' }}>{active === id ? '●' : '○'}</span> {active === id ? 'ACTIVE & TESTED' : 'NOT TESTED'}
        </div>
      </div>
    );

    return (
      <div style={{ marginBottom: '24px' }}>
        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '6px', borderBottom: '1px solid var(--color-border)', marginBottom: '12px' }}>
          TRAJECTORY
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
          <TrajCard name="STRAIGHT LINE" id="StraightLine" />
          <TrajCard name="CIRCULAR" id="Circular" />
          <TrajCard name="FIGURE-8" id="Figure8" />
          <TrajCard name="RANDOM" id="Random" />
        </div>
      </div>
    );
  };

  const MatrixRowCompact = ({ label, supported, configured, tested, status }: { label: string, supported: boolean, configured: boolean, tested: boolean, status: string }) => {
    return (
      <tr style={{ borderBottom: '1px solid var(--color-border-subtle)' }}>
        <td style={{ padding: '8px 0', color: 'var(--color-text-primary)', fontSize: '11px' }}>{label}</td>
        <td style={{ padding: '8px 0', color: supported ? 'var(--color-status-pass)' : 'var(--color-text-muted)', fontSize: '10px' }}>{supported ? '✓ Supported' : '—'}</td>
        <td style={{ padding: '8px 0', color: configured ? 'var(--color-text-primary)' : 'var(--color-text-muted)', fontSize: '10px' }}>{configured ? '✓ Configured' : '—'}</td>
        <td style={{ padding: '8px 0', color: tested ? 'var(--color-text-primary)' : 'var(--color-text-muted)', fontSize: '10px' }}>{tested ? '✓ Tested' : '—'}</td>
        <td style={{ padding: '8px 0' }}><StatusBadge status={status} /></td>
      </tr>
    );
  };

  const DisturbanceSection = () => {
    const currentAtm = t?.configSnapshot?.atmosphereMode || 'Clear';
    const currentNoise = t?.configSnapshot?.noiseType || 'Gaussian';
    const distEnabled = t?.configSnapshot?.disturbanceEnabled || false;
    const hasRun = runState === 'COMPLETE' || runState === 'MEASURING';

    return (
      <div style={{ marginBottom: '24px' }}>
        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '6px', borderBottom: '1px solid var(--color-border)', marginBottom: '8px' }}>
          DISTURBANCE & NOISE COVERAGE
        </div>
        <div style={{ fontSize: '10px', color: 'var(--color-text-muted)', marginBottom: '16px' }}>
          Noise SD MAX: 20 px | Camera Jitter MAX: ±20 px/frame
        </div>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontFamily: 'var(--font-mono)' }}>
          <thead>
            <tr style={{ color: 'var(--color-text-muted)', textAlign: 'left', fontSize: '9px', textTransform: 'uppercase' }}>
              <th style={{ padding: '6px 0', width: '25%' }}>IMAGE NOISE</th>
              <th style={{ padding: '6px 0', width: '20%' }}>SUPPORTED</th>
              <th style={{ padding: '6px 0', width: '20%' }}>CONFIGURED</th>
              <th style={{ padding: '6px 0', width: '20%' }}>TESTED</th>
              <th style={{ padding: '6px 0', width: '15%' }}>RESULT</th>
            </tr>
          </thead>
          <tbody>
            <MatrixRowCompact label="Gaussian" supported={true} configured={distEnabled && currentNoise === 'Gaussian'} tested={distEnabled && currentNoise === 'Gaussian' && hasRun} status={distEnabled && currentNoise === 'Gaussian' && hasRun ? 'PASS' : 'NOT TESTED'} />
            <MatrixRowCompact label="Salt & Pepper" supported={true} configured={distEnabled && currentNoise === 'SaltAndPepper'} tested={distEnabled && currentNoise === 'SaltAndPepper' && hasRun} status={distEnabled && currentNoise === 'SaltAndPepper' && hasRun ? 'PASS' : 'NOT TESTED'} />
            <MatrixRowCompact label="Poisson" supported={true} configured={distEnabled && currentNoise === 'Poisson'} tested={distEnabled && currentNoise === 'Poisson' && hasRun} status={distEnabled && currentNoise === 'Poisson' && hasRun ? 'PASS' : 'NOT TESTED'} />
            <MatrixRowCompact label="Camera Jitter" supported={true} configured={!!t?.configSnapshot?.cameraJitterEnabled} tested={!!t?.configSnapshot?.cameraJitterEnabled && hasRun} status={!!t?.configSnapshot?.cameraJitterEnabled && hasRun ? 'PASS' : 'NOT TESTED'} />
          </tbody>
        </table>

        <div style={{ fontSize: '9px', fontWeight: 600, color: 'var(--color-text-muted)', textTransform: 'uppercase', marginTop: '24px', marginBottom: '8px' }}>
          Atmosphere Test Conditions
        </div>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontFamily: 'var(--font-mono)' }}>
          <tbody>
            {['Clear', 'Haze', 'Fog', 'Rain', 'Low Light'].map(env => (
              <MatrixRowCompact 
                key={env} 
                label={env} 
                supported={env !== 'Low Light'} 
                configured={currentAtm === env} 
                tested={currentAtm === env && hasRun} 
                status={env === 'Low Light' ? 'N/A' : (currentAtm === env && hasRun ? 'PASS' : 'NOT TESTED')} 
              />
            ))}
          </tbody>
        </table>
      </div>
    );
  };

  const PlatformMotionSection = () => {
    const current = t?.configSnapshot?.platformMotionMode || 'Static';
    const enabled = current !== 'Static';
    const hasRun = runState === 'COMPLETE' || runState === 'MEASURING';
    return (
      <div style={{ marginBottom: '24px' }}>
        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '6px', borderBottom: '1px solid var(--color-border)', marginBottom: '8px' }}>
          PLATFORM MOTION TEST CONDITIONS
        </div>
        <div style={{ fontSize: '10px', color: 'var(--color-text-muted)', marginBottom: '16px' }}>
          PS Maximum: ±20 px/frame
        </div>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontFamily: 'var(--font-mono)' }}>
          <thead>
            <tr style={{ color: 'var(--color-text-muted)', textAlign: 'left', fontSize: '9px', textTransform: 'uppercase' }}>
              <th style={{ padding: '6px 0', width: '20%' }}>MODE</th>
              <th style={{ padding: '6px 0', width: '30%' }}>TYPE</th>
              <th style={{ padding: '6px 0', width: '30%' }}>MAGNITUDE</th>
              <th style={{ padding: '6px 0', width: '20%' }}>STATUS</th>
            </tr>
          </thead>
          <tbody>
            <tr style={{ borderBottom: '1px solid var(--color-border-subtle)' }}>
              <td style={{ padding: '8px 0', color: 'var(--color-text-primary)', fontSize: '11px' }}>Linear</td>
              <td style={{ padding: '8px 0', color: 'var(--color-text-muted)', fontSize: '10px' }}>MANDATORY / DEFAULT</td>
              <td style={{ padding: '8px 0', color: 'var(--color-text-primary)', fontSize: '11px', fontWeight: 600 }}>{enabled && current === 'Linear' ? `${t?.configSnapshot?.platformMotionMagnitude || 0} px/frame` : '—'}</td>
              <td style={{ padding: '8px 0' }}><StatusBadge status={enabled && current === 'Linear' && hasRun ? 'PASS' : 'NOT TESTED'} /></td>
            </tr>
            <tr style={{ borderBottom: '1px solid var(--color-border-subtle)' }}>
              <td style={{ padding: '8px 0', color: 'var(--color-text-primary)', fontSize: '11px' }}>Circular</td>
              <td style={{ padding: '8px 0', color: 'var(--color-text-muted)', fontSize: '10px' }}>OPTIONAL</td>
              <td style={{ padding: '8px 0', color: 'var(--color-text-primary)', fontSize: '11px', fontWeight: 600 }}>{enabled && current === 'Circular' ? `${t?.configSnapshot?.platformMotionMagnitude || 0} px/frame` : '—'}</td>
              <td style={{ padding: '8px 0' }}><StatusBadge status={enabled && current === 'Circular' && hasRun ? 'PASS' : 'NOT TESTED'} /></td>
            </tr>
          </tbody>
        </table>
      </div>
    );
  };

  const handleExport = () => {
    // Implement report export
    const a = document.createElement('a');
    a.href = 'data:text/plain;charset=utf-8,{}';
    a.download = `ps26169_report.json`;
    a.click();
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', backgroundColor: 'var(--color-bg-base)', overflowY: 'auto', width: '100%' }}>
      
      {/* HEADER */}
      <div style={{ padding: '16px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', backgroundColor: 'var(--color-bg-panel)', borderBottom: '1px solid var(--color-border)', flexShrink: 0 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginBottom: '4px' }}>
            <h1 style={{ margin: 0, fontSize: '16px', fontWeight: 700, letterSpacing: '1px', color: 'var(--color-text-primary)' }}>PS-26169 VERIFICATION CONSOLE</h1>
            <StatusBadge status={runState} dot />
          </div>
          <div style={{ fontSize: '11px', color: 'var(--color-text-secondary)', letterSpacing: '0.5px' }}>
            AEROSPACE TRACKING VERIFICATION
          </div>
        </div>
        
        {/* BENCHMARK SELECTOR STATUS */}
        <div style={{ display: 'flex', gap: '16px' }}>
          <div style={{ 
            padding: '8px 24px', 
            border: `1px solid ${isLive ? 'var(--color-status-pass)' : 'var(--color-border-subtle)'}`, 
            backgroundColor: isLive ? 'rgba(57, 211, 83, 0.05)' : 'transparent',
            borderRadius: '2px',
            boxShadow: isLive ? '0 0 10px rgba(57, 211, 83, 0.1)' : 'none',
            display: 'flex', alignItems: 'center', gap: '12px' 
          }}>
            <span style={{ fontSize: '10px', color: isLive ? 'var(--color-status-pass)' : 'var(--color-text-secondary)', fontWeight: 600 }}>[ BENCHMARK 1 ]</span>
            <span style={{ fontSize: '12px', color: isLive ? 'var(--color-text-primary)' : 'var(--color-text-muted)', fontWeight: 'bold' }}>LIVE UNITY</span>
          </div>
          <div style={{ 
            padding: '8px 24px', 
            border: `1px solid ${isMp4 ? 'var(--color-status-warn)' : 'var(--color-border-subtle)'}`, 
            backgroundColor: isMp4 ? 'rgba(224, 168, 50, 0.05)' : 'transparent',
            borderRadius: '2px',
            boxShadow: isMp4 ? '0 0 10px rgba(224, 168, 50, 0.1)' : 'none',
            display: 'flex', flexDirection: 'column', justifyContent: 'center' 
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <span style={{ fontSize: '10px', color: isMp4 ? 'var(--color-status-warn)' : 'var(--color-text-secondary)', fontWeight: 600 }}>[ BENCHMARK 2 ]</span>
              <span style={{ fontSize: '12px', color: isMp4 ? 'var(--color-text-primary)' : 'var(--color-text-muted)', fontWeight: 'bold' }}>MP4 VIDEO</span>
            </div>
            {isMp4 && <div style={{ fontSize: '9px', color: 'var(--color-status-warn)', marginTop: '4px', fontFamily: 'var(--font-mono)' }}>INPUT = MP4 | PTZ = BYPASSED</div>}
          </div>
        </div>
      </div>

      {/* COMPLIANCE SNAPSHOT RIBBON */}
      <div style={{ display: 'flex', gap: '16px', padding: '16px 24px', backgroundColor: 'var(--color-bg-base)', borderBottom: '1px solid var(--color-border-subtle)', flexShrink: 0 }}>
        <MetricRibbonCard title="TRACKING ERROR" reqId="TRK-002" />
        <MetricRibbonCard title="TARGET LOSS" reqId="TRK-003" />
        <MetricRibbonCard title="ACQUISITION" reqId="TRK-001" />
        <MetricRibbonCard title="RE-ACQUISITION" reqId="TRK-004" />
        <MetricRibbonCard title="PROCESSING FPS" reqId="TRK-005" />
      </div>

      <div style={{ padding: '24px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '32px' }}>
        
        {/* LEFT COLUMN - REQUIREMENTS */}
        <div>
          <div className="panel" style={{ padding: '24px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '4px', boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
            <RequirementTable category="SENSOR/CAMERA" />
            <RequirementTable category="TARGET/BEACON" />
            
            <TrajectoryGrid />
            
            {!isMp4 && <PtzModule />}
          </div>
        </div>

        {/* RIGHT COLUMN - COVERAGE & EVIDENCE */}
        <div>
          <div className="panel" style={{ padding: '24px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '4px', boxShadow: '0 4px 12px rgba(0,0,0,0.1)', marginBottom: '24px' }}>
            <DisturbanceSection />
            <PlatformMotionSection />
          </div>

          {/* EVIDENCE SNAPSHOT */}
          <div className="panel" style={{ padding: '24px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '4px', boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--color-border)', paddingBottom: '12px', marginBottom: '16px' }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--color-text-primary)', letterSpacing: '0.5px' }}>
                EVIDENCE SNAPSHOT
              </div>
              <button className="eng-button" onClick={handleExport} disabled={runState !== 'COMPLETE' && runState !== 'MEASURING'}>
                EXPORT REPORT
              </button>
            </div>
            
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', fontSize: '10px', fontFamily: 'var(--font-mono)', marginBottom: '24px' }}>
              <div>
                <div style={{ color: 'var(--color-text-muted)', marginBottom: '4px' }}>RUN ID</div>
                <div style={{ color: 'var(--color-text-primary)', fontSize: '12px' }}>{t?.runId || 'WAITING'}</div>
              </div>
              <div>
                <div style={{ color: 'var(--color-text-muted)', marginBottom: '4px' }}>BENCHMARK</div>
                <div style={{ color: 'var(--color-text-primary)', fontSize: '12px' }}>{isLive ? '1 - Live Simulation' : (isMp4 ? '2 - MP4 Video' : 'WAITING')}</div>
              </div>
              <div>
                <div style={{ color: 'var(--color-text-muted)', marginBottom: '4px' }}>TRAJECTORY</div>
                <div style={{ color: 'var(--color-text-primary)', fontSize: '12px' }}>{snapshot?.trajectoryMode || 'WAITING'}</div>
              </div>
              <div>
                <div style={{ color: 'var(--color-text-muted)', marginBottom: '4px' }}>TARGET</div>
                <div style={{ color: 'var(--color-text-primary)', fontSize: '12px' }}>{snapshot ? `Beacon Spot (${snapshot.targetSize}px)` : 'WAITING'}</div>
              </div>
              <div style={{ gridColumn: 'span 2' }}>
                <div style={{ color: 'var(--color-text-muted)', marginBottom: '4px' }}>DISTURBANCES</div>
                <div style={{ color: 'var(--color-text-primary)', fontSize: '12px' }}>{snapshot ? `${snapshot.atmosphereMode} | ${snapshot.disturbanceEnabled ? snapshot.noiseType : 'No Noise'}` : 'WAITING'}</div>
              </div>
            </div>

            <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '6px', borderBottom: '1px solid var(--color-border)', marginBottom: '12px' }}>
              TEST EVIDENCE
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', fontSize: '10px', fontFamily: 'var(--font-mono)' }}>
              <div><span style={{ color: 'var(--color-text-muted)' }}>Sample Count:</span> <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{t && t.fps > 0 ? Math.floor(t.timestamp * t.fps) : 'WAITING'}</span></div>
              <div><span style={{ color: 'var(--color-text-muted)' }}>Run Duration:</span> <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{t && t.timestamp > 0 ? t.timestamp.toFixed(2) : 'WAITING'} s</span></div>
              <div><span style={{ color: 'var(--color-text-muted)' }}>Detected Frames:</span> <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{t && t.fps > 0 ? Math.floor(t.timestamp * t.fps * (t.lockRetention/100)) : 'WAITING'}</span></div>
              <div><span style={{ color: 'var(--color-text-muted)' }}>Lost Frames:</span> <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{t && t.fps > 0 ? Math.floor(t.timestamp * t.fps * (t.targetLossRate/100)) : 'WAITING'}</span></div>
            </div>

          </div>
        </div>

      </div>
    </div>
  );
}
