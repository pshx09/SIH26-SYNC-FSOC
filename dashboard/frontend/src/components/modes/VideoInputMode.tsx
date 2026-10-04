import { useState, useRef, useEffect } from 'react';
import { useTelemetryData } from '../../services/TelemetryStore';
import { commandChannel } from '../../services/CommandChannel';
import { psRequirements } from '../verification/psRequirements';

export function VideoInputMode() {
  const t = useTelemetryData();
  const videoRef = useRef<HTMLVideoElement>(null);

  const [videoFile, setVideoFile] = useState<File | null>(null);
  const [videoUrl, setVideoUrl] = useState<string | null>(null);
  const [videoMeta, setVideoMeta] = useState({ duration: 0, width: 0, height: 0 });
  
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [playbackSpeed, setPlaybackSpeed] = useState(1);

  useEffect(() => {
    // Cleanup when leaving Video Input Mode
    return () => {
      commandChannel.sendCommand('video.disable', {}).catch(() => {});
    };
  }, []);

  useEffect(() => {
    const video = videoRef.current;
    if (!video || t?.inputMode !== 1) return;

    const tolerance = t.videoFrameRate > 0 ? Math.max(0.1, 2 / t.videoFrameRate) : 0.1;
    if (Math.abs(video.currentTime - t.videoTime) > tolerance) {
      video.currentTime = t.videoTime;
    }
  }, [t?.inputMode, t?.videoTime, t?.videoFrameRate]);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setVideoFile(file);
      const url = URL.createObjectURL(file);
      setVideoUrl(url);
      
      try {
        const formData = new FormData();
        formData.append('file', file);
        
        const response = await fetch('http://localhost:8000/upload_video', {
            method: 'POST',
            body: formData
        });
        const data = await response.json();
        
        await commandChannel.sendCommand('video.load', { filePath: data.path });
        setIsPlaying(false);
      } catch (err) {
        console.error("Failed to upload video to backend", err);
      }
    }
  };

  const handleVideoLoaded = () => {
    if (videoRef.current) {
      setVideoMeta({
        duration: videoRef.current.duration,
        width: videoRef.current.videoWidth,
        height: videoRef.current.videoHeight
      });
    }
  };

  const handleTimeUpdate = () => {
    if (videoRef.current) {
      setCurrentTime(videoRef.current.currentTime);
    }
  };

  // Commands
  const startBenchmark = async () => {
    if (!videoUrl) {
      console.error("Select an MP4 before starting Benchmark-2");
      return;
    }
    try {
      await commandChannel.sendCommand('benchmark.start', {});
      await commandChannel.sendCommand('video.play', {});
      await videoRef.current?.play();
      setIsPlaying(true);
    } catch (err) {
      console.error("Failed to start Benchmark-2 playback", err);
    }
  };

  const pauseBenchmark = async () => {
    try {
      await Promise.all([
        commandChannel.sendCommand('benchmark.pause', {}),
        commandChannel.sendCommand('video.pause', {})
      ]);
      videoRef.current?.pause();
      setIsPlaying(false);
    } catch (err) {
      console.error("Failed to pause Benchmark-2 playback", err);
    }
  };
  
  const stopBenchmark = async () => {
    try {
      await Promise.all([
        commandChannel.sendCommand('benchmark.stop', {}),
        commandChannel.sendCommand('video.stop', {})
      ]);
      videoRef.current?.pause();
      if (videoRef.current) videoRef.current.currentTime = 0;
      setCurrentTime(0);
      setIsPlaying(false);
    } catch (err) {
      console.error("Failed to stop Benchmark-2 playback", err);
    }
  };

  const resetBenchmark = async () => {
    try {
      await commandChannel.sendCommand('video.reset', {});
      await commandChannel.sendCommand('benchmark.reset', {});
    } catch (err) {
      console.error("Failed to reset Benchmark-2", err);
    }
    if (videoRef.current) {
      videoRef.current.pause();
      videoRef.current.currentTime = 0;
      setCurrentTime(0);
    }
    setIsPlaying(false);
  };

  const exportBenchmark = async (commandType: 'benchmark.exportCsv' | 'benchmark.exportReport') => {
    try {
      const response = await commandChannel.sendCommand(commandType, {});
      if (!response.downloadFileName || response.downloadContent === undefined) {
        throw new Error('Unity did not return a completed benchmark export.');
      }

      const blob = new Blob([response.downloadContent], { type: 'text/csv;charset=utf-8' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = response.downloadFileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error("Failed to export Benchmark-2 data", err);
    }
  };

  const setSpeed = (speed: number) => {
    setPlaybackSpeed(speed);
    if (videoRef.current) videoRef.current.playbackRate = speed;
    commandChannel.sendCommand('video.setSpeed', { speed }).catch(() => {});
  };

  // Overlay projection math
  const isTargetDetected = t?.isDetected || false;
  // Unity coordinates (0,0 center, Y up). 
  // Convert to percentage (0,0 top-left).
  const cx = 50 + (t?.centroidX || 0) / 640 * 100;
  const cy = 50 - (t?.centroidY || 0) / 480 * 100;
  const boxW = (t?.boundingBoxWidth || 0) / 640 * 100;
  const boxH = (t?.boundingBoxHeight || 0) / 480 * 100;

  const unityVideoTelemetry = t?.inputMode === 1 ? t : null;
  const displayTime = unityVideoTelemetry ? unityVideoTelemetry.videoTime : currentTime;
  const displayDuration = unityVideoTelemetry ? unityVideoTelemetry.videoDuration : videoMeta.duration;
  const currentFrame = unityVideoTelemetry && unityVideoTelemetry.videoFrame >= 0
    ? unityVideoTelemetry.videoFrame
    : null;
  const totalFrames = unityVideoTelemetry ? unityVideoTelemetry.videoFrameCount : null;

  const getStatusColor = (status: string) => {
    if (status === 'PASS') return 'var(--color-status-pass)';
    if (status === 'FAIL') return 'var(--color-status-fail)';
    if (status === 'NOT TESTED') return 'var(--color-status-warn)';
    return 'var(--color-text-muted)';
  };

  // PS Requirements
  const fpsReq = psRequirements.find(r => r.id === 'FPS');
  const fpsStatus = fpsReq ? (fpsReq.evaluate(t).status as string) : 'N/A';
  
  const errReq = psRequirements.find(r => r.id === 'TRACKING_ACCURACY');
  const errStatus = errReq ? (errReq.evaluate(t).status as string) : 'N/A';

  const acqReq = psRequirements.find(r => r.id === 'ACQUISITION_TIME');
  const acqStatus = acqReq ? (acqReq.evaluate(t).status as string) : 'N/A';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', width: '100%', backgroundColor: 'var(--color-bg-base)' }}>
      {/* HEADER */}
      <div style={{ padding: '16px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', backgroundColor: 'var(--color-bg-panel)', borderBottom: '1px solid var(--color-border)', flexShrink: 0 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '16px', fontWeight: 700, letterSpacing: '1px', color: 'var(--color-text-primary)' }}>PS-26169 VIDEO INPUT / BENCHMARK-2</h1>
          <div style={{ fontSize: '11px', color: 'var(--color-text-secondary)', letterSpacing: '0.5px', marginTop: '4px' }}>
            MP4-BASED OPTICAL TRACKING VALIDATION • PTZ BYPASSED
          </div>
        </div>
        
        <div style={{ display: 'flex', gap: '16px' }}>
          <div style={{ padding: '8px 16px', border: '1px solid var(--color-border-subtle)', backgroundColor: 'var(--color-bg-panel-header)', borderRadius: '2px', display: 'flex', flexDirection: 'column' }}>
            <span style={{ fontSize: '9px', color: 'var(--color-text-muted)', fontWeight: 600, letterSpacing: '0.5px' }}>INPUT</span>
            <span style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontWeight: 'bold' }}>MP4</span>
          </div>
          <div style={{ padding: '8px 16px', border: '1px solid var(--color-status-warn)', backgroundColor: 'rgba(210, 153, 34, 0.05)', borderRadius: '2px', display: 'flex', flexDirection: 'column' }}>
            <span style={{ fontSize: '9px', color: 'var(--color-status-warn)', fontWeight: 600, letterSpacing: '0.5px' }}>PTZ</span>
            <span style={{ fontSize: '12px', color: 'var(--color-status-warn)', fontWeight: 'bold' }}>BYPASSED</span>
          </div>
          <div style={{ padding: '8px 16px', border: '1px solid var(--color-border-subtle)', backgroundColor: 'var(--color-bg-panel-header)', borderRadius: '2px', display: 'flex', flexDirection: 'column' }}>
            <span style={{ fontSize: '9px', color: 'var(--color-text-muted)', fontWeight: 600, letterSpacing: '0.5px' }}>PROCESSING</span>
            <span style={{ fontSize: '12px', color: t?.runState === 'RUNNING' ? 'var(--color-status-pass)' : 'var(--color-text-muted)', fontWeight: 'bold' }}>{t?.runState || 'WAITING'}</span>
          </div>
        </div>
      </div>

      <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>
        
        {/* LEFT COLUMN: VIDEO & CONTROLS */}
        <div style={{ flex: '1 1 65%', display: 'flex', flexDirection: 'column', borderRight: '1px solid var(--color-border)', backgroundColor: 'var(--color-bg-base)', padding: '24px', overflowY: 'auto' }}>
          
          {/* SOURCE PANEL */}
          <div className="panel" style={{ marginBottom: '24px' }}>
            <div className="panel-header">TEST SOURCE</div>
            <div style={{ padding: '16px', display: 'flex', gap: '24px', alignItems: 'center' }}>
              <div>
                <label className="eng-button" style={{ cursor: 'pointer', display: 'inline-block' }}>
                  SELECT MP4
                  <input type="file" accept="video/mp4" onChange={handleFileChange} style={{ display: 'none' }} />
                </label>
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase' }}>File Name</div>
                <div style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)' }}>{videoFile?.name || 'WAITING FOR MP4 INPUT'}</div>
              </div>
              <div>
                <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase' }}>Resolution</div>
                <div style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)' }}>{videoMeta.width > 0 ? `${videoMeta.width}x${videoMeta.height}` : 'N/A'}</div>
              </div>
              <div>
                <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase' }}>Duration</div>
                <div style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)' }}>{displayDuration > 0 ? displayDuration.toFixed(2) + 's' : 'N/A'}</div>
              </div>
            </div>
          </div>

          {/* MAIN VIDEO VIEWPORT */}
          <div style={{ flex: 1, backgroundColor: '#000', border: '1px solid var(--color-border)', position: 'relative', minHeight: '400px', display: 'flex', justifyContent: 'center', alignItems: 'center', overflow: 'hidden' }}>
            {videoUrl ? (
               <video 
                  ref={videoRef}
                  src={videoUrl}
                  style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                  onLoadedMetadata={handleVideoLoaded}
                  onTimeUpdate={handleTimeUpdate}
                  onPlay={() => setIsPlaying(true)}
                  onPause={() => setIsPlaying(false)}
                  muted
               />
               
            ) : (
               <div style={{ color: 'var(--color-text-muted)', fontFamily: 'var(--font-mono)', fontSize: '12px' }}>NO VIDEO SOURCE SELECTED</div>
            )}
            
            <div style={{ position: 'absolute', bottom: '8px', left: '8px', color: 'rgba(255,255,255,0.65)', fontFamily: 'var(--font-mono)', fontSize: '9px', pointerEvents: 'none' }}>
              BENCHMARK TIME/FRAME FROM UNITY VIDEO PLAYER; BROWSER PREVIEW IS INDEPENDENT AND MAY NOT MATCH EXACTLY
            </div>

            {/* HUD OVERLAY */}
            {videoUrl && isPlaying && t && (
              <div style={{ position: 'absolute', inset: 0, pointerEvents: 'none' }}>
                <div style={{ position: 'absolute', top: '16px', left: '16px', color: 'var(--color-status-pass)', fontFamily: 'var(--font-mono)', fontSize: '10px' }}>
                  REC MP4-BENCH-02 &nbsp;&nbsp; UNITY FRAME {currentFrame === null ? '—' : currentFrame.toString().padStart(4, '0')}
                </div>
                <div style={{ position: 'absolute', bottom: '16px', right: '16px', color: 'var(--color-status-pass)', fontFamily: 'var(--font-mono)', fontSize: '12px' }}>
                  {Math.floor(displayTime / 60).toString().padStart(2, '0')}:{(displayTime % 60).toFixed(2).padStart(5, '0')}
                </div>
                
                <div style={{ position: 'absolute', top: '16px', right: '16px', color: 'var(--color-status-pass)', fontFamily: 'var(--font-mono)', fontSize: '10px', textAlign: 'right' }}>
                  TRACKING: {t.trackingState}<br/>
                  ERR: {t.meanError.toFixed(2)} px<br/>
                  CONF: {(t.detectionConfidence * 100).toFixed(1)}%
                </div>

                {/* Crosshair Center */}
                <div style={{ position: 'absolute', top: '50%', left: '50%', width: '20px', height: '1px', backgroundColor: 'rgba(255,255,255,0.3)', transform: 'translate(-50%, -50%)' }} />
                <div style={{ position: 'absolute', top: '50%', left: '50%', width: '1px', height: '20px', backgroundColor: 'rgba(255,255,255,0.3)', transform: 'translate(-50%, -50%)' }} />

                {/* Target Bounding Box */}
                {isTargetDetected && (
                   <div style={{ 
                     position: 'absolute', 
                     left: `${cx}%`, 
                     top: `${cy}%`, 
                     width: `${boxW}%`, 
                     height: `${boxH}%`, 
                     border: '1px solid var(--color-status-pass)',
                     transform: 'translate(-50%, -50%)',
                     boxShadow: '0 0 4px var(--color-status-pass)'
                   }}>
                     <div style={{ position: 'absolute', top: '-16px', left: '0', fontSize: '9px', color: 'var(--color-status-pass)', whiteSpace: 'nowrap' }}>TGT-1</div>
                   </div>
                )}
              </div>
            )}
          </div>

          {/* PLAYBACK CONTROL & TIMELINE */}
          <div className="panel" style={{ marginTop: '24px' }}>
            <div style={{ padding: '16px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', gap: '8px' }}>
                  <button className="eng-button" onClick={startBenchmark}>▶ PLAY</button>
                  <button className="eng-button" onClick={pauseBenchmark}>Ⅱ PAUSE</button>
                  <button className="eng-button" onClick={stopBenchmark}>■ STOP</button>
                  <button className="eng-button" onClick={resetBenchmark}>↻ RESET</button>
                </div>
                <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
                  <div style={{ fontSize: '10px', color: 'var(--color-text-muted)', fontFamily: 'var(--font-mono)' }}>
                    UNITY FRAME {currentFrame === null ? '—' : currentFrame} / {totalFrames || '—'}
                    {unityVideoTelemetry && unityVideoTelemetry.videoFrameRate > 0
                      ? ` • ${unityVideoTelemetry.videoFrameRate.toFixed(2)} FPS`
                      : ''}
                  </div>
                  <select className="eng-select" value={playbackSpeed} onChange={(e) => setSpeed(parseFloat(e.target.value))}>
                    <option value={0.25}>0.25×</option>
                    <option value={0.5}>0.5×</option>
                    <option value={1}>1.0×</option>
                    <option value={2}>2.0×</option>
                  </select>
                </div>
              </div>
              
              {/* TIMELINE VISUAL */}
              <div style={{ height: '24px', backgroundColor: 'var(--color-bg-panel-header)', border: '1px solid var(--color-border-subtle)', position: 'relative', borderRadius: '2px' }}>
                 <div style={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: `${totalFrames && currentFrame !== null && currentFrame >= 0 ? (currentFrame / totalFrames) * 100 : 0}%`, backgroundColor: 'rgba(88, 166, 255, 0.2)', borderRight: '1px solid var(--color-text-link)' }} />
                 {/* Decorative markers */}
                 <div style={{ position: 'absolute', left: '25%', top: 0, bottom: 0, width: '1px', backgroundColor: 'var(--color-status-warn)', opacity: 0.5 }} />
                 <div style={{ position: 'absolute', left: '25%', top: '-14px', fontSize: '8px', color: 'var(--color-status-warn)' }}>ACQUISITION</div>
              </div>
            </div>
          </div>
          
        </div>

        {/* RIGHT COLUMN: METRICS & ANALYSIS */}
        <div style={{ flex: '1 1 35%', display: 'flex', flexDirection: 'column', padding: '24px', overflowY: 'auto', gap: '24px' }}>
          
          {/* PTZ BYPASS ALERT */}
          <div style={{ padding: '16px', border: '1px solid var(--color-status-warn)', backgroundColor: 'rgba(210, 153, 34, 0.05)', borderRadius: '2px' }}>
             <div style={{ fontSize: '12px', color: 'var(--color-status-warn)', fontWeight: 600, marginBottom: '8px', letterSpacing: '0.5px' }}>PTZ / ACTUATOR</div>
             <div style={{ fontSize: '11px', color: 'var(--color-text-secondary)', marginBottom: '12px' }}>
               Benchmark-2 uses direct video input and evaluates the tracking pipeline independently of virtual PTZ actuation.
             </div>
             <div style={{ display: 'flex', gap: '24px', fontFamily: 'var(--font-mono)', fontSize: '11px' }}>
                <div>
                  <span style={{ color: 'var(--color-text-muted)' }}>Pan Command: </span>
                  <span style={{ color: 'var(--color-status-warn)' }}>BYPASSED</span>
                </div>
                <div>
                  <span style={{ color: 'var(--color-text-muted)' }}>Tilt Command: </span>
                  <span style={{ color: 'var(--color-status-warn)' }}>BYPASSED</span>
                </div>
             </div>
          </div>

          {/* TRACKING STATUS */}
          <div className="panel">
            <div className="panel-header">TRACKING STATUS</div>
            <div className="data-grid" style={{ gridTemplateColumns: '1fr 1fr' }}>
              <div className="data-item">
                <div className="data-label">STATE</div>
                <div className="data-value" style={{ color: t?.trackingState === 'LOCKED' ? 'var(--color-status-pass)' : (t?.trackingState === 'LOST' ? 'var(--color-status-fail)' : 'var(--color-status-warn)') }}>
                  {t?.trackingState || 'WAITING'}
                </div>
              </div>
              <div className="data-item">
                <div className="data-label">TARGET ID</div>
                <div className="data-value">TGT-1</div>
              </div>
              <div className="data-item">
                <div className="data-label">CENTROID (PX)</div>
                <div className="data-value">{t?.centroidX.toFixed(1)}, {t?.centroidY.toFixed(1)}</div>
              </div>
              <div className="data-item">
                <div className="data-label">CONFIDENCE</div>
                <div className="data-value">{(t?.detectionConfidence ? t.detectionConfidence * 100 : 0).toFixed(1)}%</div>
              </div>
            </div>
          </div>

          {/* BENCHMARK-2 PERFORMANCE */}
          <div className="panel">
            <div className="panel-header">BENCHMARK-2 PERFORMANCE</div>
            <div style={{ padding: '16px' }}>
              <table style={{ width: '100%', fontSize: '11px', textAlign: 'left', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ color: 'var(--color-text-muted)', textTransform: 'uppercase', borderBottom: '1px solid var(--color-border-subtle)' }}>
                    <th style={{ paddingBottom: '8px' }}>METRIC</th>
                    <th style={{ paddingBottom: '8px' }}>ACTUAL</th>
                    <th style={{ paddingBottom: '8px' }}>REFERENCE</th>
                  </tr>
                </thead>
                <tbody style={{ fontFamily: 'var(--font-mono)' }}>
                  <tr>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-secondary)', fontFamily: 'var(--font-sans)' }}>Centroid Error</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-primary)' }}>{t?.meanError.toFixed(2)} px</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-muted)' }}>≤ 10.0 px</td>
                  </tr>
                  <tr style={{ borderTop: '1px solid rgba(255,255,255,0.02)' }}>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-secondary)', fontFamily: 'var(--font-sans)' }}>RMSE</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-primary)' }}>{t?.rmse.toFixed(2)} px</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-muted)' }}>-</td>
                  </tr>
                  <tr style={{ borderTop: '1px solid rgba(255,255,255,0.02)' }}>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-secondary)', fontFamily: 'var(--font-sans)' }}>Acquisition Time</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-primary)' }}>{t?.acquisitionTime.toFixed(2)} s</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-muted)' }}>≤ 2.0 s</td>
                  </tr>
                  <tr style={{ borderTop: '1px solid rgba(255,255,255,0.02)' }}>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-secondary)', fontFamily: 'var(--font-sans)' }}>Re-acquisition Time</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-primary)' }}>{t?.reacquisitionTime.toFixed(2)} s</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-muted)' }}>≤ 1.0 s</td>
                  </tr>
                  <tr style={{ borderTop: '1px solid rgba(255,255,255,0.02)' }}>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-secondary)', fontFamily: 'var(--font-sans)' }}>Lock Retention</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-primary)' }}>{t?.lockRetention.toFixed(1)} %</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-muted)' }}>≥ 95.0 %</td>
                  </tr>
                  <tr style={{ borderTop: '1px solid rgba(255,255,255,0.02)' }}>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-secondary)', fontFamily: 'var(--font-sans)' }}>Processing FPS</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-primary)' }}>{t?.fps.toFixed(1)} Hz</td>
                    <td style={{ padding: '10px 0', color: 'var(--color-text-muted)' }}>≥ 30.0 Hz</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          {/* PS-26169 REQUIREMENT CHECK */}
          <div className="panel">
            <div className="panel-header">PS-26169 REQUIREMENT CHECK</div>
            <div style={{ padding: '12px 16px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid var(--color-border-subtle)', fontSize: '11px' }}>
                <div style={{ color: 'var(--color-text-secondary)' }}>Tracking Accuracy</div>
                <div style={{ fontFamily: 'var(--font-mono)' }}><span style={{ color: getStatusColor(errStatus) }}>[{errStatus}]</span></div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid var(--color-border-subtle)', fontSize: '11px' }}>
                <div style={{ color: 'var(--color-text-secondary)' }}>Acquisition Time</div>
                <div style={{ fontFamily: 'var(--font-mono)' }}><span style={{ color: getStatusColor(acqStatus) }}>[{acqStatus}]</span></div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', fontSize: '11px' }}>
                <div style={{ color: 'var(--color-text-secondary)' }}>Processing Framerate</div>
                <div style={{ fontFamily: 'var(--font-mono)' }}><span style={{ color: getStatusColor(fpsStatus) }}>[{fpsStatus}]</span></div>
              </div>
            </div>
          </div>

          {/* EXPORT OPTIONS */}
          <div style={{ display: 'flex', gap: '8px', marginTop: 'auto' }}>
            <button className="eng-button" style={{ flex: 1 }} onClick={() => exportBenchmark('benchmark.exportCsv')}>EXPORT CSV</button>
            <button className="eng-button" style={{ flex: 1 }} onClick={() => exportBenchmark('benchmark.exportReport')}>EXPORT REPORT</button>
          </div>

        </div>
      </div>
    </div>
  );
}
