import { useBinaryImageStream } from '../services/useBinaryImageStream';
import type { DashboardTelemetry } from '../types/telemetry';

export function LiveSensorView({ telemetry }: { telemetry: DashboardTelemetry | null }) {
  const { imageSrc, status, fps, frameSize } = useBinaryImageStream('ws://127.0.0.1:8000/ws/sensor-view');

  const isDetectionValid = telemetry && telemetry.isDetected;
  
  // State colors
  let colorStr = '#5f758a'; // Default SEARCHING
  if (telemetry) {
    switch (telemetry.trackingState) {
      case 'LOCKED': colorStr = '#39D353'; break; // Green
      case 'ACQUIRING':
      case 'REACQUIRING':
      case 'PREDICTING': colorStr = '#E0A832'; break; // Amber
      case 'SEARCHING': colorStr = '#5f758a'; break; // Muted blue-gray
      case 'LOST': colorStr = '#333e4f'; break; // Dim blue-gray
      default: colorStr = '#5f758a'; break;
    }
  }

  // Dashboard-only angular error derived calculation
  let angularErrorStr = '0.00°';
  if (telemetry && isDetectionValid) {
    const dx = telemetry.centroidX - 320;
    const dy = telemetry.centroidY - 240;
    const hDegPerPx = 4 / 640;
    const vDegPerPx = 3 / 480;
    const angularError = Math.sqrt(Math.pow(dx * hDegPerPx, 2) + Math.pow(dy * vDegPerPx, 2));
    angularErrorStr = angularError.toFixed(2) + '°';
  }

  // Coordinates
  const svgX = telemetry?.centroidX ?? 320;
  const svgY = telemetry ? 480 - telemetry.centroidY : 240;
  
  // Dynamic scaling with much larger minimum visibility bounds
  const boxW = telemetry ? Math.max(80, telemetry.boundingBoxWidth * 2) : 80;
  const boxH = telemetry ? Math.max(80, telemetry.boundingBoxHeight * 2) : 80;
  const tickRadius = Math.max(100, boxW * 0.7);

  return (
    <div style={{ height: '100%', width: '100%', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)' }}>
      <div style={{ padding: '4px 8px', borderBottom: '1px solid var(--color-border)', backgroundColor: 'var(--color-bg-panel-header)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '10px', fontWeight: 600, color: 'var(--color-text-secondary)', letterSpacing: '0.1em' }}>
          <span>640×480 MONOCHROME SENSOR</span>
        </div>
        
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', fontSize: '9px', fontFamily: 'var(--font-mono)' }}>
          <span className="text-muted">SOURCE <span className="text-primary">SENSOR</span></span>
          <span className="text-muted">STATUS {status === 'LIVE' ? <span className="text-green">LIVE</span> : (status === 'STALE' ? <span className="text-amber">STALE</span> : <span className="text-red">OFFLINE</span>)}</span>
          <span className="text-muted">FPS <span className="text-primary">{fps}</span></span>
          <span className="text-muted">FRAME <span className="text-primary">{(frameSize / 1024).toFixed(1)} KB</span></span>
        </div>
      </div>
      
      <div style={{ padding: 0, backgroundColor: '#000', display: 'flex', justifyContent: 'center', alignItems: 'center', position: 'relative', overflow: 'hidden', flex: 1 }}>
        <div style={{ position: 'relative', width: '100%', height: '100%', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
          <div style={{ position: 'relative', maxWidth: '100%', maxHeight: '100%', aspectRatio: '4/3', display: 'flex' }}>
            {imageSrc ? (
              <img 
                src={imageSrc} 
                alt="Live Sensor Stream" 
                style={{ width: '100%', height: '100%', objectFit: 'contain', display: 'block' }}
              />
            ) : (
              <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, display: 'flex', justifyContent: 'center', alignItems: 'center' }} className="mono text-muted">
                WAITING FOR SENSOR STREAM...
              </div>
            )}

            {imageSrc && (
              <svg 
                viewBox="0 0 640 480" 
                preserveAspectRatio="xMidYMid meet"
                style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%', pointerEvents: 'none' }}
              >
                {/* 1. Center Boresight Reticle */}
                <g opacity="0.15" stroke="#ffffff" strokeWidth="1.5">
                  <line x1="320" y1="120" x2="320" y2="360" />
                  <line x1="200" y1="240" x2="440" y2="240" />
                  <circle cx="320" cy="240" r="120" fill="none" />
                  <circle cx="320" cy="240" r="80" fill="none" strokeDasharray="4 8" />
                </g>

                {/* 2. Target Acquisition Bracket */}
                {isDetectionValid && telemetry?.trackingState !== 'LOST' && (
                  <g transform={`translate(${svgX}, ${svgY})`}>
                    
                    {/* Angular Ticks around the target */}
                    <g opacity="0.4" stroke={colorStr} strokeWidth="1.5">
                      <circle cx="0" cy="0" r={tickRadius} fill="none" />
                      {/* Main axes ticks */}
                      <line x1="0" y1={-tickRadius} x2="0" y2={-tickRadius - 12} />
                      <line x1="0" y1={tickRadius} x2="0" y2={tickRadius + 12} />
                      <line x1={-tickRadius} y1="0" x2={-tickRadius - 12} y2="0" />
                      <line x1={tickRadius} y1="0" x2={tickRadius + 12} y2="0" />
                      {/* Minor diagonal ticks */}
                      <line x1={-tickRadius * 0.707} y1={-tickRadius * 0.707} x2={-tickRadius * 0.707 - 8} y2={-tickRadius * 0.707 - 8} />
                      <line x1={tickRadius * 0.707} y1={-tickRadius * 0.707} x2={tickRadius * 0.707 + 8} y2={-tickRadius * 0.707 - 8} />
                      <line x1={-tickRadius * 0.707} y1={tickRadius * 0.707} x2={-tickRadius * 0.707 - 8} y2={tickRadius * 0.707 + 8} />
                      <line x1={tickRadius * 0.707} y1={tickRadius * 0.707} x2={tickRadius * 0.707 + 8} y2={tickRadius * 0.707 + 8} />
                    </g>
                    
                    {/* Bounding Box Brackets */}
                    <path
                      d={`
                        M ${-boxW/2 + 15} ${-boxH/2} L ${-boxW/2} ${-boxH/2} L ${-boxW/2} ${-boxH/2 + 15}
                        M ${boxW/2 - 15} ${-boxH/2} L ${boxW/2} ${-boxH/2} L ${boxW/2} ${-boxH/2 + 15}
                        M ${-boxW/2 + 15} ${boxH/2} L ${-boxW/2} ${boxH/2} L ${-boxW/2} ${boxH/2 - 15}
                        M ${boxW/2 - 15} ${boxH/2} L ${boxW/2} ${boxH/2} L ${boxW/2} ${boxH/2 - 15}
                      `}
                      fill="none"
                      stroke={colorStr}
                      strokeWidth="2.5"
                    />
                    
                    {/* Central Dot */}
                    <circle cx="0" cy="0" r="2.5" fill={colorStr} />
                    
                    {/* Telemetry Labels */}
                    <g fill={colorStr} textAnchor="middle" style={{ fontFamily: 'var(--font-mono)', fontWeight: 'bold', letterSpacing: '0.05em' }}>
                      <text x="0" y={-tickRadius - 20} fontSize="14px">BEACON TGT-1</text>
                      <text x="0" y={tickRadius + 30} fontSize="13px" opacity="0.9">ERR: {angularErrorStr} | CONF: {(telemetry.detectionConfidence * 100).toFixed(1)}%</text>
                    </g>
                  </g>
                )}

                {/* SEARCHING / NOT DETECTED Text */}
                {(!isDetectionValid || telemetry?.trackingState === 'LOST') && (
                  <text x="320" y="380" textAnchor="middle" fill={colorStr} opacity="0.8" style={{ fontFamily: 'var(--font-mono)', fontSize: '16px', fontWeight: 'bold', letterSpacing: '0.15em' }}>
                    {telemetry?.trackingState === 'LOST' ? 'TARGET LOST' : 'TARGET NOT ACQUIRED'}
                  </text>
                )}
              </svg>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
