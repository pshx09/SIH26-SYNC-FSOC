import { useMemo } from 'react';
import UplotReact from 'uplot-react';
import 'uplot/dist/uPlot.min.css';
import { useTelemetryHistory, useTelemetryData } from '../../services/TelemetryStore';

import { useResizeObserver } from '../../hooks/useResizeObserver';

export function PtzVelocityChart({ windowSeconds }: { windowSeconds: number }) {
  const history = useTelemetryHistory();
  const t = useTelemetryData();
  const { ref, width, height } = useResizeObserver<HTMLDivElement>();

  const data = useMemo(() => {
    if (history.length === 0) return [[], [], []] as unknown as uPlot.AlignedData;

    const latestTime = history[history.length - 1].timestamp;
    const windowStart = windowSeconds === 0 ? 0 : latestTime - windowSeconds;

    const filtered = windowSeconds === 0 ? history : history.filter(h => h.timestamp >= windowStart);

    let sampleStep = 1;
    if (filtered.length > 1000) {
      sampleStep = Math.ceil(filtered.length / 1000);
    }

    const times: number[] = [];
    const panVel: (number | null)[] = [];
    const tiltVel: (number | null)[] = [];

    for (let i = 0; i < filtered.length; i += sampleStep) {
      const snap = filtered[i];
      times.push(snap.timestamp ?? 0);
      
      // These are independent of beacon detection. Even while SEARCHING, 
      // if the PTZ controller is producing real velocity samples, record and plot them.
      const isPanValid = snap.panVelocity != null && !isNaN(snap.panVelocity);
      const isTiltValid = snap.tiltVelocity != null && !isNaN(snap.tiltVelocity);
      
      panVel.push(isPanValid ? snap.panVelocity : null);
      tiltVel.push(isTiltValid ? snap.tiltVelocity : null);
    }

    if (filtered.length > 0) {
      const last = filtered[filtered.length - 1];
      console.log(`[PTZ Velocity Graph] sampleCount = ${filtered.length}, xLength = ${times.length}, panLength = ${panVel.length}, tiltLength = ${tiltVel.length}`);
      console.log(`[Graph Data Proof] History Length: ${history.length}, Timestamp: ${last.timestamp.toFixed(2)}, RadialError: ${last.radialError?.toFixed(2) ?? 'null'}, PanVel: ${last.panVelocity?.toFixed(2) ?? 'null'}, TiltVel: ${last.tiltVelocity?.toFixed(2) ?? 'null'}, Detected: ${last.isDetected}`);
    }

    return [times, panVel, tiltVel] as unknown as uPlot.AlignedData;
  }, [history, windowSeconds, t]);

  const options: uPlot.Options = useMemo(() => ({
    width: width > 0 ? width : 600,
    height: height > 0 ? height : 300,
    scales: {
      x: { time: false },
      y: { 
        auto: true,
        range: (_u: uPlot, min: number, max: number) => {
          if (min == null || max == null || isNaN(min) || isNaN(max)) return [-10, 10];
          const absMax = Math.max(Math.abs(min), Math.abs(max), 10);
          return [-absMax * 1.2, absMax * 1.2];
        }
      }
    },
    axes: [
      { 
        stroke: 'var(--color-text-secondary)', 
        grid: { stroke: 'var(--color-border-subtle)' },
        values: (_u, vals) => vals.map(v => v.toFixed(1) + 's')
      },
      { 
        stroke: 'var(--color-text-secondary)', 
        grid: { stroke: 'var(--color-border-subtle)' }
      }
    ],
    series: [
      {}, // X-axis
      {
        label: "Pan Vel (°/s)",
        stroke: "#36C7FF", // blue/cyan
        width: 2,
        points: { show: false },
        spanGaps: false
      },
      {
        label: "Tilt Vel (°/s)",
        stroke: "#E0A832", // orange/amber
        width: 2,
        points: { show: false },
        spanGaps: false
      }
    ],
    cursor: {
      y: false
    }
  }), [width, height]);

  return (
    <div ref={ref} style={{ width: '100%', height: '100%' }}>
      {width > 0 && height > 0 && <UplotReact options={options} data={data} />}
    </div>
  );
}
