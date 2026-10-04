import { useState } from 'react';
import { TrackingErrorChart } from '../analytics/TrackingErrorChart';
import { PtzVelocityChart } from '../analytics/PtzVelocityChart';
import { useTelemetryData, useTelemetryHistory } from '../../services/TelemetryStore';

export function AnalysisMode() {
  const [windowSize, setWindowSize] = useState<number>(60);
  const t = useTelemetryData();
  const history = useTelemetryHistory();

  const currentWindowStr = windowSize === 0 ? 'FULL RUN' : windowSize === 60 ? '60s' : windowSize === 300 ? '5m' : '10m';

  const windowData = windowSize === 0 ? history : history.filter(h => h.timestamp >= (t ? t.timestamp - windowSize : 0));
  const meanError = windowData.length > 0 ? windowData.reduce((s, h) => s + h.meanError, 0) / windowData.length : 0;
  const maxError = windowData.length > 0 ? Math.max(...windowData.map(h => h.maxError)) : 0;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', padding: '8px', height: '100%', overflowY: 'auto', width: '100%' }}>
      <div className="panel" style={{ padding: '8px 12px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0, fontSize: '12px', letterSpacing: '1px' }}>TECHNICAL ANALYSIS WORKSTATION</h2>
        <div style={{ display: 'flex', gap: '4px' }}>
          {[60, 300, 600, 0].map(val => (
            <button
              key={val}
              onClick={() => setWindowSize(val)}
              style={{
                padding: '2px 8px',
                border: windowSize === val ? '1px solid var(--color-accent-blue)' : '1px solid var(--color-border)',
                backgroundColor: windowSize === val ? 'var(--color-accent-blue-dim)' : 'var(--color-bg-panel)',
                cursor: 'pointer',
                borderRadius: '2px',
                fontFamily: 'var(--font-sans)',
                fontWeight: 600
              }}
            >
              <span style={{ fontSize: '10px', color: windowSize === val ? 'var(--color-text-primary)' : 'var(--color-text-secondary)' }}>
                {val === 0 ? 'FULL RUN' : val === 60 ? '60s' : val === 300 ? '5m' : '10m'}
              </span>
            </button>
          ))}
        </div>
      </div>

      {/* Row 1: Tracking Error (Left) | Statistics (Right) */}
      <div style={{ display: 'flex', gap: '8px', height: '40vh', minHeight: '300px' }}>
        <div className="panel" style={{ flex: '3 1 0', display: 'flex', flexDirection: 'column' }}>
          <div className="panel-header">TRACKING ERROR VS TIME ({currentWindowStr})</div>
          <div className="panel-content" style={{ position: 'relative' }}>
            <div style={{ position: 'absolute', top: 8, left: 8, right: 8, bottom: 8, display: 'flex', flexDirection: 'column' }}>
              <TrackingErrorChart windowSeconds={windowSize} />
            </div>
          </div>
        </div>

        <div className="panel" style={{ flex: '1 1 0', display: 'flex', flexDirection: 'column' }}>
          <div className="panel-header">STATISTICS ({currentWindowStr})</div>
          <div className="panel-content">
            <div className="data-row"><span className="data-label">Samples</span><span className="data-value">{windowData.length}</span></div>
            <div className="data-row"><span className="data-label">Mean Error</span><span className="data-value">{meanError.toFixed(2)} px</span></div>
            <div className="data-row"><span className="data-label">RMSE</span><span className="data-value">{t?.rmse && t.rmse > 0 ? t.rmse.toFixed(2) + ' px' : 'WAITING'}</span></div>
            <div className="data-row"><span className="data-label">Max Error</span><span className="data-value">{maxError.toFixed(2)} px</span></div>
            <div className="data-row"><span className="data-label">Within 10 px</span><span className="data-value">{t?.lockRetention !== undefined ? t.lockRetention.toFixed(1) + '%' : 'WAITING'}</span></div>
            <div className="data-row"><span className="data-label">Target Loss</span><span className="data-value">{t?.targetLossRate !== undefined ? t.targetLossRate.toFixed(1) + '%' : 'WAITING'}</span></div>
          </div>
        </div>
      </div>

      {/* Row 2: Pan/Tilt Velocity (Left) | Event Timeline (Right) */}
      <div style={{ display: 'flex', gap: '8px', height: '40vh', minHeight: '300px' }}>
        <div className="panel" style={{ flex: '3 1 0', display: 'flex', flexDirection: 'column' }}>
          <div className="panel-header">PAN/TILT VELOCITY VS TIME ({currentWindowStr})</div>
          <div className="panel-content" style={{ position: 'relative' }}>
            <div style={{ position: 'absolute', top: 8, left: 8, right: 8, bottom: 8, display: 'flex', flexDirection: 'column' }}>
              <PtzVelocityChart windowSeconds={windowSize} />
            </div>
          </div>
        </div>

        <div className="panel" style={{ flex: '1 1 0', display: 'flex', flexDirection: 'column' }}>
          <div className="panel-header">EVENT TIMELINE</div>
          <div className="panel-content" style={{ padding: 0, overflowY: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '10px', fontFamily: 'var(--font-mono)' }}>
              <tbody>
                {t?.recentEvents && t.recentEvents.length > 0 ? (
                  t.recentEvents.map((ev, i) => (
                    <tr key={i}>
                      <td style={{ padding: '6px 8px', color: 'var(--color-text-secondary)', borderBottom: '1px solid var(--color-border-subtle)' }}>
                        {new Date().toISOString().substr(11, 8)}
                      </td>
                      <td style={{ padding: '6px 8px', borderBottom: '1px solid var(--color-border-subtle)', color: ev.includes('LOCKED') || ev.includes('ACQUIRED') ? 'var(--color-status-pass)' : (ev.includes('LOST') || ev.includes('SEARCHING') || ev.includes('RE-ACQUISITION') ? 'var(--color-status-warn)' : 'var(--color-text-primary)') }}>
                        {ev}
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr><td colSpan={2} style={{ padding: '6px 8px', color: 'var(--color-text-muted)' }}>WAITING FOR EVENTS...</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
