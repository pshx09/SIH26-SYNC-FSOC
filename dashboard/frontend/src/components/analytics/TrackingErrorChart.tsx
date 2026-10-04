import { useMemo } from 'react';
import UplotReact from 'uplot-react';
import 'uplot/dist/uPlot.min.css';
import { useTelemetryHistory, useTelemetryData } from '../../services/TelemetryStore';

import { useResizeObserver } from '../../hooks/useResizeObserver';

export function TrackingErrorChart({ windowSeconds }: { windowSeconds: number }) {
  const history = useTelemetryHistory();
  const t = useTelemetryData(); // Subscribe to latest data to force re-render
  const { ref, width, height } = useResizeObserver<HTMLDivElement>();

  const data = useMemo(() => {
    if (history.length === 0) return [[], []] as unknown as uPlot.AlignedData;

    const latestTime = history[history.length - 1].timestamp;
    const windowStart = windowSeconds === 0 ? 0 : latestTime - windowSeconds;

    const filtered = windowSeconds === 0 ? history : history.filter(h => h.timestamp >= windowStart);

    // Downsample if over 1000 points to save rendering time
    let sampleStep = 1;
    if (filtered.length > 1000) {
      sampleStep = Math.ceil(filtered.length / 1000);
    }

    const times: number[] = [];
    const radialError: (number | null)[] = [];
    const threshold: number[] = [];

    let validCount = 0;
    let firstValue: number | null = null;
    let lastValue: number | null = null;

    for (let i = 0; i < filtered.length; i += sampleStep) {
      const snap = filtered[i];
      times.push(snap.timestamp ?? 0);
      
      const isValid = snap.isDetected;
      const rErr = isValid ? snap.radialError : null;
      radialError.push(rErr);
      threshold.push(10.0);

      if (isValid && rErr != null) {
        validCount++;
        if (firstValue === null) firstValue = rErr;
        lastValue = rErr;
      }
    }

    console.log(`[Tracking Error Graph] sampleCount = ${filtered.length}, xLength = ${times.length}, yLength = ${radialError.length}, validCount = ${validCount}, firstValue = ${firstValue}, lastValue = ${lastValue}`);

    return [times, radialError, threshold] as unknown as uPlot.AlignedData;
  }, [history, windowSeconds, t]);

  const options: uPlot.Options = useMemo(() => ({
    width: width > 0 ? width : 600,
    height: height > 0 ? height : 300,
    scales: {
      x: { time: false },
      y: { 
        auto: true,
        range: (_u: uPlot, min: number, max: number) => {
          if (min == null || max == null || isNaN(min) || isNaN(max)) return [0, 10];
          return [0, Math.max(10, max * 1.2)];
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
        label: "Radial Error (px)",
        stroke: "#39D353", // GREEN tracking-error line
        width: 2,          // ~2px stroke
        points: { show: false }, // disabled points
        spanGaps: false
      },
      {
        label: "Threshold (10 px)",
        stroke: "#E0A832", // AMBER threshold line
        width: 2,
        dash: [5, 5],
        points: { show: false }
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
