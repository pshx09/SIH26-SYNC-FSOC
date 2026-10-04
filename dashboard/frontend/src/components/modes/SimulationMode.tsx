import { LiveSimulationView } from '../LiveSimulationView';
import { LiveSensorView } from '../LiveSensorView';
import { KpiGrid } from '../layout/KpiGrid';
import { MissionRail } from '../layout/MissionRail';
import { ConfigurationPanel } from '../layout/ConfigurationPanel';
import { TrackingErrorChart } from '../analytics/TrackingErrorChart';
import { PtzVelocityChart } from '../analytics/PtzVelocityChart';
import { useTelemetryData } from '../../services/TelemetryStore';

export function SimulationMode() {
  const telemetry = useTelemetryData();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', width: '100%', height: '100%', padding: '8px', gap: '8px', overflow: 'hidden', backgroundColor: 'var(--color-bg-base)' }}>
      
      {/* TOP WORKSPACE (approx 60% height) */}
      <div style={{ display: 'flex', gap: '8px', flex: '60 1 0', minHeight: 0 }}>
        
        {/* Left: 3D Virtual Environment */}
        <div style={{ flex: '55 1 0', display: 'flex' }}>
          <LiveSimulationView />
        </div>
        
        {/* Middle: 640x480 Sensor View */}
        <div style={{ flex: '25 1 0', display: 'flex' }}>
          <LiveSensorView telemetry={telemetry} />
        </div>
        
        {/* Right: Mission Rail (System & Telemetry) */}
        <div style={{ flex: '20 1 0', display: 'flex' }}>
          <MissionRail />
        </div>
        
      </div>

      {/* BOTTOM WORKSPACE (approx 40% height) */}
      <div style={{ display: 'flex', gap: '8px', flex: '40 1 0', minHeight: 0 }}>
        
        {/* Left: KPIs */}
        <div style={{ flex: '25 1 0', display: 'flex' }}>
          <KpiGrid />
        </div>
        
        {/* Middle: Performance Graphs */}
        <div style={{ flex: '35 1 0', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)' }}>
          <div className="panel-header">PERFORMANCE GRAPHS</div>
          {telemetry && telemetry.timestamp > 0 ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1px', flex: 1, minHeight: 0 }}>
              
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', borderBottom: '1px solid var(--color-border)', backgroundColor: 'var(--color-bg-base)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', padding: '4px 8px', backgroundColor: 'var(--color-bg-panel)' }}>
                  <span style={{ color: 'var(--color-text-secondary)', textTransform: 'uppercase' }}>1. Coordinate Tracking Error (pixels vs Time)</span>
                  <span className="text-green">Avg: {telemetry.meanError.toFixed(2)} px</span>
                </div>
                <div style={{ flex: 1, position: 'relative' }}>
                  <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, display: 'flex', flexDirection: 'column' }}>
                    <TrackingErrorChart windowSeconds={60} />
                  </div>
                </div>
              </div>
              
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', backgroundColor: 'var(--color-bg-base)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', padding: '4px 8px', backgroundColor: 'var(--color-bg-panel)' }}>
                  <span style={{ color: 'var(--color-text-secondary)', textTransform: 'uppercase' }}>2. Pan/Tilt Velocity (deg/s vs Time)</span>
                  <div style={{ display: 'flex', gap: '12px' }}>
                    <span className="text-blue">Pan: {(telemetry.panVelocity > 0 ? '+' : '') + telemetry.panVelocity.toFixed(1)}°/s</span>
                    <span className="text-amber">Tilt: {(telemetry.tiltVelocity > 0 ? '+' : '') + telemetry.tiltVelocity.toFixed(1)}°/s</span>
                  </div>
                </div>
                <div style={{ flex: 1, position: 'relative' }}>
                  <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, display: 'flex', flexDirection: 'column' }}>
                    <PtzVelocityChart windowSeconds={60} />
                  </div>
                </div>
              </div>
              
            </div>
          ) : (
            <div style={{ flex: 1, display: 'flex', justifyContent: 'center', alignItems: 'center', color: 'var(--color-text-muted)', fontSize: '12px', letterSpacing: '0.1em', textTransform: 'uppercase' }}>
              WAITING FOR TELEMETRY
            </div>
          )}
        </div>
        
        {/* Right: Configuration / Disturbances / PTZ */}
        <div style={{ flex: '40 1 0', display: 'flex' }}>
          <ConfigurationPanel />
        </div>
        
      </div>
      
    </div>
  );
}
