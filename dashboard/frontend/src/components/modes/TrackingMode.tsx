import { LiveSensorView } from '../LiveSensorView';
import { KpiStrip } from '../layout/KpiStrip';
import { MissionRail } from '../layout/MissionRail';
import { TrackingErrorChart } from '../analytics/TrackingErrorChart';
import { PtzVelocityChart } from '../analytics/PtzVelocityChart';
import { useTelemetryData } from '../../services/TelemetryStore';

export function TrackingMode() {
  const telemetry = useTelemetryData();



  const getStatusColor = (state: number | undefined) => {
    switch (state) {
      case 2: return 'text-green';
      case 1:
      case 3:
      case 4: return 'text-amber';
      case 5: return 'text-amber'; // Fallback for lost, using amber as a neutral/warning tone since text-fail isn't standard
      default: return 'text-muted';
    }
  };

  return (
    <div style={{ display: 'flex', width: '100%', height: '100%', padding: '4px', gap: '4px', overflow: 'hidden' }}>
      {/* Main Workspace (Left ~75%) */}
      <div style={{ flex: '3 1 0', display: 'flex', flexDirection: 'column', gap: '4px', minWidth: '800px', overflow: 'hidden' }}>
        
        {/* Row 1: Primary Sensor Stream & Target Status */}
        <div style={{ display: 'flex', gap: '4px', flex: '1 1 auto', minHeight: 0 }}>
          <div className="panel" style={{ flex: 2, display: 'flex', flexDirection: 'column' }}>
            <div className="panel-content" style={{ padding: 0, flex: 1, backgroundColor: '#000', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
              <LiveSensorView telemetry={telemetry} />
            </div>
          </div>
          
          <div className="panel" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
            <div className="panel-header" style={{ padding: '4px 8px', fontSize: '10px', textTransform: 'uppercase', fontWeight: 600, color: 'var(--color-text-secondary)', borderBottom: '1px solid var(--color-border-subtle)', backgroundColor: 'transparent' }}>TARGET STATE VECTOR</div>
            <div className="panel-content" style={{ display: 'flex', flexDirection: 'column', gap: '4px', padding: '6px 8px' }}>
              <div className="data-row" style={{ border: 'none', padding: 0 }}><span className="data-label">Status</span><span className={`data-value ${getStatusColor(telemetry?.trackingState === 'LOCKED' ? 2 : telemetry?.trackingState === 'ACQUIRING' ? 1 : 0)}`} style={{ fontWeight: 'bold' }}>{telemetry?.trackingState || 'N/A'}</span></div>
              <div className="data-row" style={{ border: 'none', padding: 0 }}><span className="data-label">X Error (px)</span><span className="data-value">{telemetry?.errorX !== undefined ? telemetry.errorX.toFixed(2) : '---'}</span></div>
              <div className="data-row" style={{ border: 'none', padding: 0 }}><span className="data-label">Y Error (px)</span><span className="data-value">{telemetry?.errorY !== undefined ? telemetry.errorY.toFixed(2) : '---'}</span></div>
              <div className="data-row" style={{ border: 'none', padding: 0 }}><span className="data-label">Total Error</span><span className="data-value">{telemetry?.radialError !== undefined ? telemetry.radialError.toFixed(2) : '---'}</span></div>
              <div className="data-row" style={{ border: 'none', padding: 0 }}><span className="data-label">Centroid X</span><span className="data-value">{telemetry?.centroidX !== undefined ? telemetry.centroidX.toFixed(1) : '---'}</span></div>
              <div className="data-row" style={{ border: 'none', padding: 0 }}><span className="data-label">Centroid Y</span><span className="data-value">{telemetry?.centroidY !== undefined ? telemetry.centroidY.toFixed(1) : '---'}</span></div>
              <div className="data-row" style={{ border: 'none', padding: 0 }}><span className="data-label">Confidence</span><span className="data-value">{telemetry?.detectionConfidence !== undefined ? (telemetry.detectionConfidence * 100).toFixed(1) + '%' : '---'}</span></div>
            </div>
          </div>
        </div>
        
        {/* Row 2: KPI Strip */}
        <div style={{ flex: '0 0 auto' }}>
          <KpiStrip />
        </div>
        
        {/* Row 3: Tracking Analytics */}
        <div style={{ display: 'flex', gap: '4px', flex: '1 1 auto', minHeight: 0 }}>
          <div className="panel" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
            <div className="panel-header" style={{ padding: '4px 8px', fontSize: '10px', textTransform: 'uppercase', fontWeight: 600, color: 'var(--color-text-secondary)', borderBottom: '1px solid var(--color-border-subtle)', backgroundColor: 'transparent' }}>TRACKING ERROR DYNAMICS</div>
            <div className="panel-content" style={{ display: 'flex', flexDirection: 'column', padding: '4px', flex: 1 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', marginBottom: '4px', paddingLeft: '4px', paddingRight: '4px' }}>
                <span style={{ color: 'var(--color-text-secondary)', textTransform: 'uppercase' }}>Real-time Error Magnitude (px)</span>
                <span className="text-green">Avg: {telemetry ? telemetry.meanError.toFixed(2) : '---'} px</span>
              </div>
              <div style={{ flex: 1, position: 'relative' }}>
                <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, display: 'flex', flexDirection: 'column' }}>
                  <TrackingErrorChart windowSeconds={30} />
                </div>
              </div>
            </div>
          </div>
          
          <div className="panel" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
            <div className="panel-header" style={{ padding: '4px 8px', fontSize: '10px', textTransform: 'uppercase', fontWeight: 600, color: 'var(--color-text-secondary)', borderBottom: '1px solid var(--color-border-subtle)', backgroundColor: 'transparent' }}>GIMBAL KINEMATICS</div>
            <div className="panel-content" style={{ display: 'flex', flexDirection: 'column', padding: '4px', flex: 1 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', marginBottom: '4px', paddingLeft: '4px', paddingRight: '4px' }}>
                <span style={{ color: 'var(--color-text-secondary)', textTransform: 'uppercase' }}>PTZ Angular Velocity (°/s)</span>
                <div style={{ display: 'flex', gap: '8px' }}>
                  <span className="text-blue">Pan: {telemetry ? (telemetry.panVelocity > 0 ? '+' : '') + telemetry.panVelocity.toFixed(1) : '---'}°/s</span>
                  <span className="text-amber">Tilt: {telemetry ? (telemetry.tiltVelocity > 0 ? '+' : '') + telemetry.tiltVelocity.toFixed(1) : '---'}°/s</span>
                </div>
              </div>
              <div style={{ flex: 1, position: 'relative' }}>
                <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, display: 'flex', flexDirection: 'column' }}>
                  <PtzVelocityChart windowSeconds={30} />
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>

      {/* Mission Rail (Right ~25%) */}
      <div style={{ flex: '1 0 0', display: 'flex', flexDirection: 'column', gap: '4px', minWidth: '320px', overflow: 'hidden' }}>
        <MissionRail />
      </div>
    </div>
  );
}
