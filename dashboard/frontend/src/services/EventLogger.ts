import { telemetryStore } from './TelemetryStore';
import type { DashboardTelemetry } from '../types/telemetry';

export interface LogEvent {
  id: string;
  realTime: number; // Date.now()
  simTime: number;
  severity: 'INFO' | 'WARNING' | 'ERROR' | 'SYSTEM';
  subsystem: string;
  event: string;
  detail: string;
  state?: string;
  runId: number;
}

export interface RunHistoryItem {
  runId: number;
  startTime: number; // Date.now()
  endTime: number | null;
  durationSim: number;
  trajectory: string;
  input: string;
  target: string;
  eventCount: number;
  finalState: string;
}

class EventLoggerService {
  private events: LogEvent[] = [];
  private runHistory: Map<number, RunHistoryItem> = new Map();
  private cachedRunHistory: RunHistoryItem[] | null = null;
  private listeners: Set<() => void> = new Set();

  private lastState: DashboardTelemetry | null = null;
  private lastUnityEvents: Set<string> = new Set();
  private eventIdCounter = 0;

  constructor() {
    telemetryStore.subscribe(this.handleTelemetry);
    
    // Add initial system startup event
    this.addEvent({
      simTime: 0,
      severity: 'SYSTEM',
      subsystem: 'SYSTEM',
      event: 'Dashboard initialized',
      detail: 'Event logger started',
      runId: 0
    });
  }

  private addEvent(e: Omit<LogEvent, 'id' | 'realTime'>) {
    const newEvent: LogEvent = {
      ...e,
      id: `evt_${this.eventIdCounter++}`,
      realTime: Date.now()
    };
    
    this.events = [newEvent, ...this.events]; // immutable update
    
    if (e.runId > 0) {
      const run = this.runHistory.get(e.runId);
      if (run) {
        run.eventCount++;
        this.runHistory = new Map(this.runHistory); // immutable update for Map
      }
    }
    
    this.cachedRunHistory = null;
    this.notify();
  }

  private handleTelemetry = () => {
    const current = telemetryStore.getState();

    if (!this.lastState && current) {
      this.addEvent({
        simTime: current.timestamp,
        severity: 'SYSTEM',
        subsystem: 'TELEMETRY',
        event: 'Telemetry Connected',
        detail: 'Initial state received from backend',
        runId: current.runId
      });
    }

    if (this.lastState && this.lastState.runState !== 'OFFLINE' && current?.runState === 'OFFLINE') {
      this.addEvent({
        simTime: this.lastState.timestamp,
        severity: 'ERROR',
        subsystem: 'TELEMETRY',
        event: 'Telemetry Connection Lost',
        detail: 'Run state is OFFLINE',
        runId: this.lastState.runId
      });
    }

    if (current && current.runState !== 'OFFLINE') {
      // 1. Check for new Run
      if (current.runId > 0 && (!this.lastState || current.runId !== this.lastState.runId)) {
        this.runHistory.set(current.runId, {
          runId: current.runId,
          startTime: Date.now(),
          endTime: null,
          durationSim: 0,
          trajectory: current.configSnapshot?.trajectoryMode || 'Unknown',
          input: current.inputMode === 1 ? 'MP4 Video' : 'Live Simulation',
          target: 'Beacon Spot',
          eventCount: 0,
          finalState: 'RUNNING'
        });
        this.cachedRunHistory = null;

        this.addEvent({
          simTime: current.timestamp,
          severity: 'SYSTEM',
          subsystem: 'RUNTIME',
          event: 'Run Started',
          detail: `Run ID: ${current.runId}`,
          state: 'RUNNING',
          runId: current.runId
        });
      }

      // Update Run Duration & Final State if Stopped
      if (current.runId > 0 && this.runHistory.has(current.runId)) {
        const run = this.runHistory.get(current.runId)!;
        if (run.durationSim !== current.timestamp) {
           run.durationSim = current.timestamp;
           this.cachedRunHistory = null;
        }
        
        if (current.runState === 'IDLE' && this.lastState?.runState === 'RUNNING') {
           run.endTime = Date.now();
           run.finalState = 'COMPLETE';
           this.addEvent({
             simTime: current.timestamp,
             severity: 'SYSTEM',
             subsystem: 'RUNTIME',
             event: 'Run Stopped',
             detail: `Run ID: ${current.runId} finished`,
             state: 'COMPLETE',
             runId: current.runId
           });
        }
      }

      // 2. State Transitions
      if (this.lastState && current.runId === this.lastState.runId && current.runState !== 'IDLE') {
        // Tracking State
        if (this.lastState.trackingState !== current.trackingState) {
          const isErr = current.trackingState === 'LOST';
          const isWarn = current.trackingState === 'SEARCHING' || current.trackingState === 'REACQUIRING';
          this.addEvent({
            simTime: current.timestamp,
            severity: isErr ? 'ERROR' : (isWarn ? 'WARNING' : 'INFO'),
            subsystem: 'TRACKING',
            event: 'Tracking State Change',
            detail: `${this.lastState.trackingState} → ${current.trackingState}`,
            state: current.trackingState,
            runId: current.runId
          });
        }

        // Config Changes (Trajectory, Disturbance)
        if (this.lastState.configSnapshot && current.configSnapshot) {
          const lConfig = this.lastState.configSnapshot;
          const cConfig = current.configSnapshot;

          if (lConfig.trajectoryMode !== cConfig.trajectoryMode) {
            this.addEvent({
              simTime: current.timestamp,
              severity: 'INFO',
              subsystem: 'TRAJECTORY',
              event: 'Mode Changed',
              detail: `${lConfig.trajectoryMode} → ${cConfig.trajectoryMode}`,
              runId: current.runId
            });
          }
          if (lConfig.atmosphereMode !== cConfig.atmosphereMode) {
            this.addEvent({
              simTime: current.timestamp,
              severity: 'INFO',
              subsystem: 'ATMOSPHERE',
              event: 'Condition Changed',
              detail: `${lConfig.atmosphereMode} → ${cConfig.atmosphereMode}`,
              runId: current.runId
            });
          }
          if (lConfig.noiseType !== cConfig.noiseType || lConfig.disturbanceEnabled !== cConfig.disturbanceEnabled) {
             const from = lConfig.disturbanceEnabled ? lConfig.noiseType : 'None';
             const to = cConfig.disturbanceEnabled ? cConfig.noiseType : 'None';
             if (from !== to) {
               this.addEvent({
                 simTime: current.timestamp,
                 severity: 'WARNING',
                 subsystem: 'DISTURBANCE',
                 event: 'Noise Changed',
                 detail: `${from} → ${to}`,
                 runId: current.runId
               });
             }
          }
        }
      }

      // 3. Process recentEvents from Unity
      if (current.recentEvents && current.recentEvents.length > 0) {
        const currentEvents = new Set(current.recentEvents);
        
        current.recentEvents.forEach(evtStr => {
          if (!this.lastUnityEvents.has(evtStr)) {
            // New event string
            let msg = evtStr;
            let sub = 'SYSTEM';
            let sev: 'INFO' | 'WARNING' | 'ERROR' | 'SYSTEM' = 'INFO';
            
            const match = evtStr.match(/^\[(.*?)\]\s*(.*)$/);
            if (match) {
              msg = match[2];
            }
            
            msg = msg.trim();
            if (msg.toLowerCase().includes('error')) sev = 'ERROR';
            if (msg.toLowerCase().includes('warn')) sev = 'WARNING';
            if (msg.toLowerCase().includes('ptz')) sub = 'PTZ';
            if (msg.toLowerCase().includes('target') || msg.toLowerCase().includes('track')) sub = 'TRACKING';
            if (msg.toLowerCase().includes('motion')) sub = 'DISTURBANCE';

            this.addEvent({
              simTime: current.timestamp,
              severity: sev,
              subsystem: sub,
              event: 'Unity Event',
              detail: msg,
              runId: current.runId
            });
          }
        });
        
        this.lastUnityEvents = currentEvents;
      }
    }

    this.lastState = current;
  };

  public getEvents() {
    return this.events;
  }
  
  public getRunHistory() {
    if (!this.cachedRunHistory) {
      this.cachedRunHistory = Array.from(this.runHistory.values()).reverse();
    }
    return this.cachedRunHistory;
  }

  public subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  private notify() {
    for (const l of this.listeners) {
      l();
    }
  }
}

export const eventLogger = new EventLoggerService();
