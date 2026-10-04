import { useState, useSyncExternalStore, useMemo } from 'react';
import { eventLogger } from '../../services/EventLogger';
import type { LogEvent } from '../../services/EventLogger';
import { useTelemetryData, useTelemetryStatus } from '../../services/TelemetryStore';

function useEventLog() {
  return useSyncExternalStore(eventLogger.subscribe, () => eventLogger.getEvents(), () => eventLogger.getEvents());
}

function useRunHistory() {
  return useSyncExternalStore(eventLogger.subscribe, () => eventLogger.getRunHistory(), () => eventLogger.getRunHistory());
}

export function LogsMode() {
  const events = useEventLog();
  const runHistory = useRunHistory();
  const t = useTelemetryData();
  const isConnected = useTelemetryStatus();

  const [activeTab, setActiveTab] = useState<'STREAM' | 'HISTORY'>('STREAM');
  const [isPaused, setIsPaused] = useState(false);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);

  // Filters
  const [filterSeverity, setFilterSeverity] = useState<string>('ALL');
  const [filterSubsystem, setFilterSubsystem] = useState<string>('ALL');
  const [filterTime, setFilterTime] = useState<string>('ALL'); // ALL, 1M, 5M, 15M, CURRENT

  // For the freeze behavior, we need to capture events when paused
  const [frozenEvents, setFrozenEvents] = useState<LogEvent[]>([]);

  const handlePauseToggle = () => {
    if (!isPaused) {
      setFrozenEvents(events);
    }
    setIsPaused(!isPaused);
  };

  const displayedEvents = isPaused ? frozenEvents : events;

  // Apply filters
  const filteredEvents = useMemo(() => {
    const now = Date.now();
    return displayedEvents.filter(e => {
      if (filterSeverity !== 'ALL' && e.severity !== filterSeverity) return false;
      if (filterSubsystem !== 'ALL' && e.subsystem !== filterSubsystem) return false;
      
      if (filterTime === '1M' && now - e.realTime > 60000) return false;
      if (filterTime === '5M' && now - e.realTime > 300000) return false;
      if (filterTime === '15M' && now - e.realTime > 900000) return false;
      if (filterTime === 'CURRENT' && t?.runId && e.runId !== t.runId) return false;

      return true;
    });
  }, [displayedEvents, filterSeverity, filterSubsystem, filterTime, t?.runId]);

  const selectedEvent = events.find(e => e.id === selectedEventId);

  const getSeverityColor = (sev: string) => {
    switch (sev) {
      case 'INFO': return 'var(--color-text-link)';
      case 'WARNING': return 'var(--color-status-warn)';
      case 'ERROR': return 'var(--color-status-fail)';
      case 'SYSTEM': return 'var(--color-text-primary)';
      default: return 'var(--color-text-muted)';
    }
  };

  const formatSimTime = (simTime: number) => {
    const h = Math.floor(simTime / 3600);
    const m = Math.floor((simTime % 3600) / 60);
    const s = simTime % 60;
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toFixed(3).padStart(6, '0')}`;
  };

  const currentRun = runHistory.length > 0 ? runHistory[0] : null;
  const isRunning = t?.runState === 'RUNNING';

  const warningCount = currentRun ? events.filter(e => e.runId === currentRun.runId && e.severity === 'WARNING').length : 0;
  const errorCount = currentRun ? events.filter(e => e.runId === currentRun.runId && e.severity === 'ERROR').length : 0;
  const trackingCount = currentRun ? events.filter(e => e.runId === currentRun.runId && e.subsystem === 'TRACKING').length : 0;

  const exportCsv = () => {
    const header = "Time,SimTime,Severity,Subsystem,Event,Detail,RunId\n";
    const rows = filteredEvents.map(e => `${new Date(e.realTime).toISOString()},${e.simTime.toFixed(3)},${e.severity},${e.subsystem},"${e.event}","${e.detail}",${e.runId}`).join('\n');
    const blob = new Blob([header + rows], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `mission_log_run_${currentRun?.runId || 'all'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', width: '100%', backgroundColor: 'var(--color-bg-base)' }}>
      
      {/* HEADER */}
      <div style={{ padding: '16px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', backgroundColor: 'var(--color-bg-panel)', borderBottom: '1px solid var(--color-border)', flexShrink: 0 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '16px', fontWeight: 700, letterSpacing: '1px', color: 'var(--color-text-primary)' }}>MISSION LOG / EVENT RECORDER</h1>
          <div style={{ fontSize: '11px', color: 'var(--color-text-secondary)', letterSpacing: '0.5px', marginTop: '4px' }}>
            REAL-TIME SYSTEM EVENTS • TRACKING • PTZ • PERFORMANCE
          </div>
        </div>
        
        <div style={{ display: 'flex', gap: '16px' }}>
          <div style={{ padding: '8px 16px', border: '1px solid var(--color-border-subtle)', backgroundColor: 'var(--color-bg-panel-header)', borderRadius: '2px', display: 'flex', flexDirection: 'column' }}>
            <span style={{ fontSize: '9px', color: 'var(--color-text-muted)', fontWeight: 600, letterSpacing: '0.5px' }}>RECORDER</span>
            <span style={{ fontSize: '12px', color: !isConnected ? 'var(--color-status-fail)' : (isPaused ? 'var(--color-status-warn)' : 'var(--color-status-pass)'), fontWeight: 'bold' }}>
              ● {!isConnected ? 'OFFLINE' : (isPaused ? 'PAUSED' : 'LIVE')}
            </span>
          </div>
          <div style={{ padding: '8px 16px', border: '1px solid var(--color-border-subtle)', backgroundColor: 'var(--color-bg-panel-header)', borderRadius: '2px', display: 'flex', flexDirection: 'column' }}>
            <span style={{ fontSize: '9px', color: 'var(--color-text-muted)', fontWeight: 600, letterSpacing: '0.5px' }}>RUN ID: {t?.runId || 'WAITING'}</span>
            <span style={{ fontSize: '12px', color: isRunning ? 'var(--color-text-primary)' : 'var(--color-text-muted)', fontWeight: 'bold' }}>{t?.runState || 'UNKNOWN'}</span>
          </div>
          <div style={{ padding: '8px 16px', border: '1px solid var(--color-border-subtle)', backgroundColor: 'var(--color-bg-panel-header)', borderRadius: '2px', display: 'flex', flexDirection: 'column' }}>
            <span style={{ fontSize: '9px', color: 'var(--color-text-muted)', fontWeight: 600, letterSpacing: '0.5px' }}>INPUT</span>
            <span style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontWeight: 'bold' }}>{t?.inputMode === 1 ? 'MP4 Video' : 'Simulation'}</span>
          </div>
        </div>
      </div>

      {/* TOP SUMMARY STRIP */}
      <div style={{ display: 'flex', gap: '16px', padding: '12px 24px', backgroundColor: 'var(--color-bg-base)', borderBottom: '1px solid var(--color-border-subtle)', flexShrink: 0 }}>
        <div style={{ flex: 1, padding: '12px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '2px' }}>
          <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Current Run</div>
          <div style={{ fontSize: '14px', color: 'var(--color-text-primary)', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{currentRun ? `RUN-${currentRun.runId}` : 'WAITING'}</div>
        </div>
        <div style={{ flex: 1, padding: '12px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '2px' }}>
          <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Events</div>
          <div style={{ fontSize: '14px', color: 'var(--color-text-primary)', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{currentRun ? currentRun.eventCount : 'WAITING'}</div>
        </div>
        <div style={{ flex: 1, padding: '12px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '2px' }}>
          <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Warnings</div>
          <div style={{ fontSize: '14px', color: warningCount > 0 ? 'var(--color-status-warn)' : 'var(--color-text-primary)', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{currentRun ? warningCount : 'WAITING'}</div>
        </div>
        <div style={{ flex: 1, padding: '12px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '2px' }}>
          <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Errors</div>
          <div style={{ fontSize: '14px', color: errorCount > 0 ? 'var(--color-status-fail)' : 'var(--color-text-primary)', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{currentRun ? errorCount : 'WAITING'}</div>
        </div>
        <div style={{ flex: 1, padding: '12px', backgroundColor: 'var(--color-bg-panel)', border: '1px solid var(--color-border)', borderRadius: '2px' }}>
          <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Tracking Events</div>
          <div style={{ fontSize: '14px', color: 'var(--color-text-primary)', fontWeight: 600, fontFamily: 'var(--font-mono)' }}>{currentRun ? trackingCount : 'WAITING'}</div>
        </div>
      </div>

      {/* TOOLBAR */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 24px', backgroundColor: 'var(--color-bg-panel)', borderBottom: '1px solid var(--color-border-subtle)', flexShrink: 0 }}>
        <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
          <div style={{ display: 'flex', border: '1px solid var(--color-border-subtle)', borderRadius: '2px', overflow: 'hidden' }}>
            <button className="eng-button" style={{ border: 'none', backgroundColor: activeTab === 'STREAM' ? 'var(--color-bg-active)' : 'transparent', color: activeTab === 'STREAM' ? 'var(--color-text-primary)' : 'var(--color-text-muted)' }} onClick={() => setActiveTab('STREAM')}>EVENT STREAM</button>
            <button className="eng-button" style={{ border: 'none', borderLeft: '1px solid var(--color-border-subtle)', backgroundColor: activeTab === 'HISTORY' ? 'var(--color-bg-active)' : 'transparent', color: activeTab === 'HISTORY' ? 'var(--color-text-primary)' : 'var(--color-text-muted)' }} onClick={() => setActiveTab('HISTORY')}>RUN HISTORY</button>
          </div>

          {activeTab === 'STREAM' && (
            <>
              <div style={{ width: '1px', height: '16px', backgroundColor: 'var(--color-border)' }}></div>
              <select className="eng-select" value={filterSeverity} onChange={e => setFilterSeverity(e.target.value)} style={{ padding: '4px 8px', fontSize: '11px' }}>
                <option value="ALL">Severity: ALL</option>
                <option value="INFO">INFO</option>
                <option value="WARNING">WARNING</option>
                <option value="ERROR">ERROR</option>
                <option value="SYSTEM">SYSTEM</option>
              </select>
              <select className="eng-select" value={filterSubsystem} onChange={e => setFilterSubsystem(e.target.value)} style={{ padding: '4px 8px', fontSize: '11px' }}>
                <option value="ALL">Subsystem: ALL</option>
                <option value="TRACKING">TRACKING</option>
                <option value="PTZ">PTZ</option>
                <option value="TRAJECTORY">TRAJECTORY</option>
                <option value="DISTURBANCE">DISTURBANCE</option>
                <option value="SYSTEM">SYSTEM</option>
                <option value="RUNTIME">RUNTIME</option>
              </select>
              <select className="eng-select" value={filterTime} onChange={e => setFilterTime(e.target.value)} style={{ padding: '4px 8px', fontSize: '11px' }}>
                <option value="ALL">Time: ALL</option>
                <option value="1M">LAST 1 MIN</option>
                <option value="5M">LAST 5 MIN</option>
                <option value="15M">LAST 15 MIN</option>
                <option value="CURRENT">CURRENT RUN</option>
              </select>
            </>
          )}
        </div>
        
        <div style={{ display: 'flex', gap: '8px' }}>
          {activeTab === 'STREAM' && (
            <button className="eng-button" onClick={handlePauseToggle} style={{ borderColor: isPaused ? 'var(--color-status-warn)' : undefined, color: isPaused ? 'var(--color-status-warn)' : undefined }}>
              {isPaused ? 'RESUME LOG' : 'PAUSE LOG'}
            </button>
          )}
          <button className="eng-button" onClick={exportCsv}>EXPORT CSV</button>
        </div>
      </div>

      {/* MAIN WORKSPACE */}
      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
        {activeTab === 'STREAM' && (
          <>
            {/* EVENT TABLE */}
            <div style={{ flex: 1, display: 'flex', flexDirection: 'column', borderRight: '1px solid var(--color-border)', overflow: 'hidden' }}>
              <div style={{ display: 'flex', padding: '8px 16px', backgroundColor: 'var(--color-bg-panel-header)', borderBottom: '1px solid var(--color-border)', fontSize: '10px', color: 'var(--color-text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>
                <div style={{ width: '90px' }}>TIME</div>
                <div style={{ width: '80px' }}>SEVERITY</div>
                <div style={{ width: '100px' }}>SUBSYSTEM</div>
                <div style={{ width: '150px' }}>EVENT</div>
                <div style={{ flex: 1 }}>DETAIL</div>
                <div style={{ width: '100px', textAlign: 'right' }}>STATE</div>
              </div>
              <div style={{ flex: 1, overflowY: 'auto', padding: '8px 0' }}>
                {!isConnected && filteredEvents.length === 0 && (
                  <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)', fontSize: '12px' }}>
                    WAITING FOR UNITY TELEMETRY
                  </div>
                )}
                {filteredEvents.map((evt, idx) => {
                  const isSelected = selectedEventId === evt.id;
                  const sevColor = getSeverityColor(evt.severity);
                  return (
                    <div 
                      key={evt.id}
                      onClick={() => setSelectedEventId(evt.id)}
                      style={{ 
                        display: 'flex', 
                        padding: '6px 16px', 
                        borderBottom: '1px solid var(--color-border-subtle)',
                        backgroundColor: isSelected ? 'rgba(88, 166, 255, 0.1)' : (idx % 2 === 0 ? 'transparent' : 'rgba(255,255,255,0.01)'),
                        fontSize: '11px',
                        fontFamily: 'var(--font-mono)',
                        cursor: 'pointer',
                        borderLeft: `2px solid ${sevColor}`,
                        color: 'var(--color-text-primary)'
                      }}
                    >
                      <div style={{ width: '90px', color: 'var(--color-text-muted)' }}>{formatSimTime(evt.simTime)}</div>
                      <div style={{ width: '80px', color: sevColor, fontWeight: 600 }}>{evt.severity}</div>
                      <div style={{ width: '100px' }}>
                        <span style={{ padding: '2px 6px', backgroundColor: 'var(--color-bg-panel-header)', border: '1px solid var(--color-border-subtle)', borderRadius: '2px', fontSize: '9px' }}>{evt.subsystem}</span>
                      </div>
                      <div style={{ width: '150px', fontWeight: 600, color: 'var(--color-text-secondary)' }}>{evt.event}</div>
                      <div style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{evt.detail}</div>
                      <div style={{ width: '100px', textAlign: 'right', color: 'var(--color-text-muted)' }}>{evt.state || ''}</div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* DETAILS PANEL & PERFORMANCE */}
            <div style={{ width: '300px', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--color-bg-panel)' }}>
              
              <div style={{ flex: 1, padding: '16px', overflowY: 'auto', borderBottom: '1px solid var(--color-border)' }}>
                <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '6px', borderBottom: '1px solid var(--color-border)', marginBottom: '16px' }}>
                  EVENT DETAILS
                </div>
                {selectedEvent ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                    <div>
                      <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Time (Sim)</div>
                      <div style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)' }}>{formatSimTime(selectedEvent.simTime)}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Severity</div>
                      <div style={{ fontSize: '12px', color: getSeverityColor(selectedEvent.severity), fontWeight: 600 }}>{selectedEvent.severity}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Subsystem</div>
                      <div style={{ fontSize: '12px', color: 'var(--color-text-primary)' }}>{selectedEvent.subsystem}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Event</div>
                      <div style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontWeight: 600 }}>{selectedEvent.event}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Detail</div>
                      <div style={{ fontSize: '12px', color: 'var(--color-text-primary)', whiteSpace: 'pre-wrap', fontFamily: 'var(--font-mono)' }}>{selectedEvent.detail}</div>
                    </div>
                    {selectedEvent.state && (
                      <div>
                        <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>State</div>
                        <div style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)' }}>{selectedEvent.state}</div>
                      </div>
                    )}
                    <div>
                      <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', textTransform: 'uppercase', marginBottom: '4px' }}>Run ID</div>
                      <div style={{ fontSize: '12px', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)' }}>{selectedEvent.runId}</div>
                    </div>
                  </div>
                ) : (
                  <div style={{ color: 'var(--color-text-muted)', fontSize: '11px', textAlign: 'center', marginTop: '32px' }}>
                    Select an event to view details
                  </div>
                )}
              </div>

              {/* CURRENT PERFORMANCE SNAPSHOT */}
              <div style={{ padding: '16px', backgroundColor: 'var(--color-bg-base)' }}>
                 <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '6px', borderBottom: '1px solid var(--color-border)', marginBottom: '12px' }}>
                  CURRENT PERFORMANCE
                 </div>
                 <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '8px', fontSize: '10px', fontFamily: 'var(--font-mono)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: 'var(--color-text-muted)' }}>Mean Error</span>
                      <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{t?.meanError?.toFixed(2) || 'WAIT'} px</span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: 'var(--color-text-muted)' }}>Max Error</span>
                      <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{t?.maxError?.toFixed(2) || 'WAIT'} px</span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: 'var(--color-text-muted)' }}>Lock Retention</span>
                      <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{t?.lockRetention?.toFixed(1) || 'WAIT'} %</span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: 'var(--color-text-muted)' }}>Processing FPS</span>
                      <span style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{t?.fps?.toFixed(1) || 'WAIT'} Hz</span>
                    </div>
                 </div>
              </div>
            </div>
          </>
        )}

        {activeTab === 'HISTORY' && (
          <div style={{ flex: 1, padding: '24px', overflowY: 'auto' }}>
            <div className="panel" style={{ padding: '24px' }}>
              <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--color-text-secondary)', paddingBottom: '8px', borderBottom: '1px solid var(--color-border)', marginBottom: '16px' }}>
                COMPLETED RUNS
              </div>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontFamily: 'var(--font-mono)' }}>
                <thead>
                  <tr style={{ color: 'var(--color-text-muted)', textAlign: 'left', fontSize: '10px', textTransform: 'uppercase' }}>
                    <th style={{ padding: '8px 0', borderBottom: '1px solid var(--color-border-subtle)' }}>RUN ID</th>
                    <th style={{ padding: '8px 0', borderBottom: '1px solid var(--color-border-subtle)' }}>DURATION</th>
                    <th style={{ padding: '8px 0', borderBottom: '1px solid var(--color-border-subtle)' }}>TRAJECTORY</th>
                    <th style={{ padding: '8px 0', borderBottom: '1px solid var(--color-border-subtle)' }}>INPUT</th>
                    <th style={{ padding: '8px 0', borderBottom: '1px solid var(--color-border-subtle)' }}>EVENTS</th>
                    <th style={{ padding: '8px 0', borderBottom: '1px solid var(--color-border-subtle)' }}>STATE</th>
                  </tr>
                </thead>
                <tbody>
                  {runHistory.map(run => (
                    <tr key={run.runId} style={{ borderBottom: '1px solid var(--color-border-subtle)' }}>
                      <td style={{ padding: '12px 0', color: 'var(--color-text-primary)', fontWeight: 600 }}>RUN-{run.runId}</td>
                      <td style={{ padding: '12px 0', color: 'var(--color-text-primary)' }}>{run.durationSim.toFixed(2)}s</td>
                      <td style={{ padding: '12px 0', color: 'var(--color-text-primary)' }}>{run.trajectory}</td>
                      <td style={{ padding: '12px 0', color: 'var(--color-text-primary)' }}>{run.input}</td>
                      <td style={{ padding: '12px 0', color: 'var(--color-text-primary)' }}>{run.eventCount}</td>
                      <td style={{ padding: '12px 0', color: run.finalState === 'COMPLETE' ? 'var(--color-status-pass)' : 'var(--color-status-warn)', fontWeight: 'bold' }}>{run.finalState}</td>
                    </tr>
                  ))}
                  {runHistory.length === 0 && (
                     <tr><td colSpan={6} style={{ padding: '24px 0', textAlign: 'center', color: 'var(--color-text-muted)' }}>No run history available</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

      </div>
    </div>
  );
}
