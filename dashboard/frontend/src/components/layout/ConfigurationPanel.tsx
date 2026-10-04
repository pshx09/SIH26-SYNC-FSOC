import { useState, useEffect } from 'react';
import { useTelemetryData } from '../../services/TelemetryStore';
import { commandChannel } from '../../services/CommandChannel';

export function ConfigurationPanel() {
  const telemetry = useTelemetryData();
  const config = telemetry?.configSnapshot;
  
  // Local state for edits
  const [trajectoryMode, setTrajectoryMode] = useState('Figure8');
  const [targetSpeed, setTargetSpeed] = useState(30);
  
  const [atmosphereMode, setAtmosphereMode] = useState('Clear');
  const [atmosphereEnabled, setAtmosphereEnabled] = useState(false);
  const [atmosphereIntensity, setAtmosphereIntensity] = useState(1.0);
  const [noiseType, setNoiseType] = useState('Gaussian');
  const [noiseStrength, setNoiseStrength] = useState(0.5);
  const [disturbanceEnabled, setDisturbanceEnabled] = useState(false);
  
  const [maxPanSpeed, setMaxPanSpeed] = useState(5.0);
  const [maxTiltSpeed, setMaxTiltSpeed] = useState(5.0);

  // Status states
  const [pendingTraj, setPendingTraj] = useState(false);
  const [pendingSpeed, setPendingSpeed] = useState(false);
  const [pendingEnv, setPendingEnv] = useState(false);
  const [pendingPtz, setPendingPtz] = useState(false);

  const [dirtyTraj, setDirtyTraj] = useState(false);
  const [dirtySpeed, setDirtySpeed] = useState(false);
  const [dirtyEnv, setDirtyEnv] = useState(false);
  const [dirtyPtz, setDirtyPtz] = useState(false);

  // Sync with telemetry config when not pending and not dirty
  useEffect(() => {
    if (!config) return;
    if (!pendingTraj && !dirtyTraj) {
      setTrajectoryMode(config.trajectoryMode || 'Figure8');
    }
    if (!pendingSpeed && !dirtySpeed) {
      // Speed depends on mode
      if (config.trajectoryMode === 'StraightLine') setTargetSpeed(config.straightLineSpeed);
      else if (config.trajectoryMode === 'Circular') setTargetSpeed(config.orbitAngularSpeed);
      else if (config.trajectoryMode === 'Figure8') setTargetSpeed(config.figure8MaxSpeed);
      else if (config.trajectoryMode === 'Random') setTargetSpeed(config.randomSpeed);
    }
    if (!pendingEnv && !dirtyEnv) {
      setAtmosphereMode(config.atmosphereMode || 'Clear');
      setAtmosphereEnabled(config.atmosphereEnabled);
      setAtmosphereIntensity(config.atmosphereIntensity || 1.0);
      setNoiseType(config.noiseType || 'Gaussian');
      setNoiseStrength(config.noiseStrength || 0.5);
      setDisturbanceEnabled(config.disturbanceEnabled);
    }
    if (!pendingPtz && !dirtyPtz) {
      setMaxPanSpeed(config.maxPanSpeed || 5.0);
      setMaxTiltSpeed(config.maxTiltSpeed || 5.0);
    }
  }, [config, pendingTraj, pendingSpeed, pendingEnv, pendingPtz, dirtyTraj, dirtySpeed, dirtyEnv, dirtyPtz]);

  const handleApplyTrajectory = async () => {
    setPendingTraj(true);
    try {
      await commandChannel.sendCommand('trajectory.setMode', { mode: trajectoryMode });
      setTimeout(() => setDirtyTraj(false), 500);
    } catch (e) {
      console.error(e);
      if (config) setTrajectoryMode(config.trajectoryMode || 'Figure8');
      setDirtyTraj(false);
    }
    setPendingTraj(false);
  };

  const handleApplySpeed = async () => {
    setPendingSpeed(true);
    try {
      if (trajectoryMode === 'StraightLine') {
        await commandChannel.sendCommand('trajectory.setStraightLine', { ...config, straightLineSpeed: targetSpeed });
      } else if (trajectoryMode === 'Circular') {
        await commandChannel.sendCommand('trajectory.setCircular', { ...config, orbitAngularSpeed: targetSpeed });
      } else if (trajectoryMode === 'Figure8') {
        await commandChannel.sendCommand('trajectory.setFigure8', { ...config, figure8MaxSpeed: targetSpeed });
      } else if (trajectoryMode === 'Random') {
        await commandChannel.sendCommand('trajectory.setRandom', { ...config, randomSpeed: targetSpeed });
      }
      setTimeout(() => setDirtySpeed(false), 500);
    } catch (e) {
      console.error(e);
      if (config) {
        if (config.trajectoryMode === 'StraightLine') setTargetSpeed(config.straightLineSpeed);
        else if (config.trajectoryMode === 'Circular') setTargetSpeed(config.orbitAngularSpeed);
        else if (config.trajectoryMode === 'Figure8') setTargetSpeed(config.figure8MaxSpeed);
        else if (config.trajectoryMode === 'Random') setTargetSpeed(config.randomSpeed);
      }
      setDirtySpeed(false);
    }
    setPendingSpeed(false);
  };

  const handleApplyEnv = async () => {
    setPendingEnv(true);
    try {
      await commandChannel.sendCommand('atmosphere.setConfig', { 
        ...config, 
        atmosphereEnabled, 
        atmosphereMode,
        atmosphereIntensity 
      });
      await commandChannel.sendCommand('disturbance.setConfig', { 
        ...config, 
        disturbanceEnabled, 
        noiseType,
        noiseStrength 
      });
      setTimeout(() => setDirtyEnv(false), 500);
    } catch (e) {
      console.error(e);
      if (config) {
        setAtmosphereMode(config.atmosphereMode || 'Clear');
        setAtmosphereEnabled(config.atmosphereEnabled);
        setAtmosphereIntensity(config.atmosphereIntensity || 1.0);
        setNoiseType(config.noiseType || 'Gaussian');
        setNoiseStrength(config.noiseStrength || 0.5);
        setDisturbanceEnabled(config.disturbanceEnabled);
      }
      setDirtyEnv(false);
    }
    setPendingEnv(false);
  };

  const handleApplyPtz = async () => {
    setPendingPtz(true);
    try {
      await commandChannel.sendCommand('ptz.setConfig', { 
        ...config, 
        maxPanSpeed, 
        maxTiltSpeed 
      });
      setTimeout(() => setDirtyPtz(false), 500);
    } catch (e) {
      console.error(e);
      if (config) {
        setMaxPanSpeed(config.maxPanSpeed || 5.0);
        setMaxTiltSpeed(config.maxTiltSpeed || 5.0);
      }
      setDirtyPtz(false);
    }
    setPendingPtz(false);
  };

  const SectionHeader = ({ title, onApply, pending, disabled }: { title: string, onApply?: () => void, pending?: boolean, disabled?: boolean }) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '10px', fontWeight: 600, color: 'var(--color-text-secondary)', borderBottom: '1px solid var(--color-border)', paddingBottom: '4px', marginBottom: '8px', letterSpacing: '0.05em' }}>
      <span>{title}</span>
      {onApply && (
        <button className="eng-button" onClick={onApply} disabled={disabled || pending}>
          {pending ? 'WAIT' : 'APPLY'}
        </button>
      )}
    </div>
  );

  return (
    <div className="panel" style={{ flex: 1 }}>
      <div className="panel-header">CONFIGURATION / DISTURBANCES / PTZ</div>
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>
        
        {/* LEFT COLUMN: SCENARIO & ENVIRONMENT */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', borderRight: '1px solid var(--color-border)', padding: '12px', overflowY: 'auto' }}>
          
          <div style={{ marginBottom: '16px' }}>
            <SectionHeader title="SCENARIO CONFIGURATION" />
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px 16px' }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <label className="text-muted" style={{ fontSize: '10px' }}>MOTION PATH</label>
                <div style={{ display: 'flex', gap: '4px' }}>
                  <select className="eng-select" value={config ? trajectoryMode : 'WAITING'} onChange={e => { setTrajectoryMode(e.target.value); setDirtyTraj(true); }} disabled={pendingTraj || !config}>
                    <option value="WAITING" disabled style={{display: 'none'}}>WAITING</option>
                    <option value="StraightLine">Straight Line</option>
                    <option value="Circular">Circular</option>
                    <option value="Figure8">Figure-8</option>
                    <option value="Random">Random</option>
                  </select>
                  <button className="eng-button" onClick={handleApplyTrajectory} disabled={pendingTraj || trajectoryMode === config?.trajectoryMode || !config}>
                    {pendingTraj ? '...' : 'SET'}
                  </button>
                </div>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <label className="text-muted" style={{ fontSize: '10px' }}>TARGET SPEED (px/s)</label>
                <div style={{ display: 'flex', gap: '4px' }}>
                  <input type="number" className="eng-input" value={targetSpeed} onChange={e => { setTargetSpeed(Number(e.target.value)); setDirtySpeed(true); }} disabled={pendingSpeed || !config} />
                  <button className="eng-button" onClick={handleApplySpeed} disabled={pendingSpeed || !config}>
                    {pendingSpeed ? '...' : 'SET'}
                  </button>
                </div>
              </div>
            </div>

            {/* DYNAMIC TRAJECTORY DETAILS */}
            <div style={{ marginTop: '12px', padding: '8px', backgroundColor: 'var(--color-bg-base)', border: '1px solid var(--color-border-subtle)', borderRadius: 'var(--radius-sm)' }}>
              <div style={{ fontSize: '9px', color: 'var(--color-text-secondary)', marginBottom: '8px', letterSpacing: '0.05em' }}>ACTIVE PATH PARAMETERS</div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px 12px', fontSize: '10px', fontFamily: 'var(--font-mono)' }}>
                {config && trajectoryMode === 'Figure8' && (
                  <>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Amplitude X:</span><span>{config.figure8AmplitudeX?.toFixed(1) || '0.0'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Amplitude Y:</span><span>{config.figure8AmplitudeY?.toFixed(1) || '0.0'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Period:</span><span>{config.figure8Period?.toFixed(1) || '0.0'}</span></div>
                  </>
                )}
                {config && trajectoryMode === 'StraightLine' && (
                  <>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Start X:</span><span>{config.straightLineStartX?.toFixed(1) || '0.0'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Dir X:</span><span>{config.straightLineDirX?.toFixed(2) || '0.00'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Start Y:</span><span>{config.straightLineStartY?.toFixed(1) || '0.0'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Dir Y:</span><span>{config.straightLineDirY?.toFixed(2) || '0.00'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Range:</span><span>{config.straightLineRange?.toFixed(1) || '0.0'}</span></div>
                  </>
                )}
                {config && trajectoryMode === 'Circular' && (
                  <>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Center X:</span><span>{config.orbitCenterX?.toFixed(1) || '0.0'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Radius:</span><span>{config.orbitRadius?.toFixed(1) || '0.0'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Center Y:</span><span>{config.orbitCenterY?.toFixed(1) || '0.0'}</span></div>
                  </>
                )}
                {config && trajectoryMode === 'Random' && (
                  <>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Bound X:</span><span>{config.randomBoundsX?.toFixed(0) || '0'}</span></div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Bound Y:</span><span>{config.randomBoundsY?.toFixed(0) || '0'}</span></div>
                  </>
                )}
                {!config && (
                  <div className="text-muted" style={{ gridColumn: 'span 2' }}>WAITING FOR TELEMETRY...</div>
                )}
              </div>
            </div>
          </div>

          <div>
            <SectionHeader title="ENVIRONMENT" onApply={handleApplyEnv} pending={pendingEnv} disabled={!config} />
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input type="checkbox" checked={atmosphereEnabled} onChange={e => { setAtmosphereEnabled(e.target.checked); setDirtyEnv(true); }} disabled={pendingEnv || !config} />
                <div style={{ flex: 1, display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <label className="text-muted" style={{ fontSize: '10px', width: '70px' }}>ATMOSPHERE</label>
                  <select className="eng-select" style={{ flex: 1 }} value={atmosphereMode} onChange={e => { setAtmosphereMode(e.target.value); setDirtyEnv(true); }} disabled={pendingEnv || !atmosphereEnabled || !config}>
                    <option value="Clear">Clear</option>
                    <option value="Haze">Haze</option>
                    <option value="Fog">Fog</option>
                    <option value="Rain">Rain</option>
                  </select>
                  <input type="number" min="0" max="1" step="0.1" className="eng-input" style={{ width: '40px' }} value={atmosphereIntensity} onChange={e => { setAtmosphereIntensity(Number(e.target.value)); setDirtyEnv(true); }} disabled={pendingEnv || !atmosphereEnabled || !config} title="Intensity" />
                </div>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input type="checkbox" checked={disturbanceEnabled} onChange={e => { setDisturbanceEnabled(e.target.checked); setDirtyEnv(true); }} disabled={pendingEnv || !config} />
                <div style={{ flex: 1, display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <label className="text-muted" style={{ fontSize: '10px', width: '70px' }}>IMG NOISE</label>
                  <select className="eng-select" style={{ flex: 1 }} value={noiseType} onChange={e => { setNoiseType(e.target.value); setDirtyEnv(true); }} disabled={pendingEnv || !disturbanceEnabled || !config}>
                    <option value="Gaussian">Gaussian</option>
                    <option value="SaltAndPepper">S&P</option>
                    <option value="Poisson">Poisson</option>
                  </select>
                  <input type="number" min="0" max="1" step="0.1" className="eng-input" style={{ width: '40px' }} value={noiseStrength} onChange={e => { setNoiseStrength(Number(e.target.value)); setDirtyEnv(true); }} disabled={pendingEnv || !disturbanceEnabled || !config} title="Strength" />
                </div>
              </div>

            </div>
          </div>

        </div>

        {/* RIGHT COLUMN: PTZ & SYSTEM STATUS */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', padding: '12px', overflowY: 'auto' }}>
          
          <div style={{ marginBottom: '16px' }}>
            <SectionHeader title="PTZ CONSTRAINTS" onApply={handleApplyPtz} pending={pendingPtz} disabled={!config} />
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px 16px' }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <label className="text-muted" style={{ fontSize: '10px' }}>MAX PAN (°/s)</label>
                <input type="number" className="eng-input" value={maxPanSpeed} onChange={e => { setMaxPanSpeed(Number(e.target.value)); setDirtyPtz(true); }} disabled={pendingPtz || !config} />
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <label className="text-muted" style={{ fontSize: '10px' }}>MAX TILT (°/s)</label>
                <input type="number" className="eng-input" value={maxTiltSpeed} onChange={e => { setMaxTiltSpeed(Number(e.target.value)); setDirtyPtz(true); }} disabled={pendingPtz || !config} />
              </div>
            </div>
          </div>

          <div style={{ flex: 1 }}>
            <SectionHeader title="SYSTEM STATUS OVERVIEW" />
            
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '10px', fontFamily: 'var(--font-mono)' }}>
              
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 8px', backgroundColor: 'var(--color-bg-base)', border: '1px solid var(--color-border-subtle)', borderRadius: 'var(--radius-sm)' }}>
                <span className="text-muted">SCENARIO:</span>
                <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{config ? config.trajectoryMode : 'OFFLINE'}</span>
              </div>
              
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 8px', backgroundColor: 'var(--color-bg-base)', border: '1px solid var(--color-border-subtle)', borderRadius: 'var(--radius-sm)' }}>
                <span className="text-muted">DISTURBANCE:</span>
                <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>
                  {config ? (config.atmosphereEnabled || config.disturbanceEnabled ? 'ACTIVE' : 'INACTIVE') : 'OFFLINE'}
                </span>
              </div>
              
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 8px', backgroundColor: 'var(--color-bg-base)', border: '1px solid var(--color-border-subtle)', borderRadius: 'var(--radius-sm)' }}>
                <span className="text-muted">CAMERA JITTER:</span>
                <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>
                  {config ? (config.cameraJitterEnabled ? `${config.cameraJitterMagnitude?.toFixed(1)} px` : 'NONE') : 'OFFLINE'}
                </span>
              </div>

            </div>
            
          </div>

        </div>

      </div>
    </div>
  );
}
