import { useEffect, useState } from 'react';
import type { DashboardMode } from '../../types/ui';
import { useTelemetryData } from '../../services/TelemetryStore';

interface GlobalHeaderProps {
  currentMode: DashboardMode;
}

export function GlobalHeader({ currentMode }: GlobalHeaderProps) {
  const t = useTelemetryData();
  const [theme, setTheme] = useState<'dark' | 'light'>('dark');

  useEffect(() => {
    // Initialize theme from local storage or default to dark
    const storedTheme = localStorage.getItem('fsoc-theme') as 'dark' | 'light' | null;
    const initialTheme = storedTheme || 'dark';
    setTheme(initialTheme);
    document.documentElement.setAttribute('data-theme', initialTheme);
  }, []);

  const toggleTheme = () => {
    const newTheme = theme === 'dark' ? 'light' : 'dark';
    setTheme(newTheme);
    localStorage.setItem('fsoc-theme', newTheme);
    document.documentElement.setAttribute('data-theme', newTheme);
  };

  const isOffline = !t || t.runState === 'UNKNOWN' || t.runState === 'OFFLINE';
  const isWaiting = t?.runState === 'WAITING';
  const isSimulating = !isOffline && !isWaiting && t && t.timestamp > 0;
  const isLocked = isSimulating && t?.trackingState === 'LOCKED';
  const isLive = t && t.inputMode === 0;

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      backgroundColor: 'var(--color-header-bg, var(--color-bg-base))',
      borderBottom: '1px solid var(--color-border)',
      padding: '4px 16px',
      color: 'var(--color-text-primary)',
      fontFamily: 'var(--font-sans)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
        <div>
          <div style={{ fontSize: '13px', fontWeight: 700, letterSpacing: '1px', marginBottom: '2px', color: 'var(--color-text-primary)' }}>
            FSOC GROUND SEGMENT — VIRTUAL TRACKING CONSOLE
          </div>
          <div style={{ fontSize: '9px', color: 'var(--color-text-muted)', display: 'flex', gap: '8px', letterSpacing: '0.05em', fontWeight: 600 }}>
            <span>COARSE ALIGNMENT</span>
            <span>|</span>
            <span className={currentMode === 'SIMULATION' ? 'text-primary' : ''}>SIMULATION</span>
            <span>|</span>
            <span className={currentMode === 'TRACKING' ? 'text-primary' : ''}>DETECTION</span>
            <span>|</span>
            <span className={currentMode === 'VERIFICATION' ? 'text-primary' : ''}>PERFORMANCE EVALUATION</span>
          </div>
        </div>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '24px' }}>
        
        <div style={{ display: 'flex', gap: '8px', fontFamily: 'var(--font-mono)', fontSize: '10px' }}>
          
          <div className="status-indicator">
            <div className="status-indicator-header">SYSTEM</div>
            <div className="status-indicator-value">
              <span style={{ color: isSimulating ? 'var(--color-status-pass)' : 'var(--color-status-offline)', fontSize: '8px' }}>●</span>
              <span className={isSimulating ? "text-green" : "text-muted"}>{isSimulating ? 'RUNNING' : 'OFFLINE'}</span>
            </div>
          </div>

          <div className="status-indicator">
            <div className="status-indicator-header">TRACKING</div>
            <div className="status-indicator-value">
              <span style={{ color: isLocked ? 'var(--color-status-pass)' : (isSimulating ? 'var(--color-status-warn)' : 'var(--color-status-offline)'), fontSize: '8px' }}>●</span>
              <span className={isLocked ? "text-green" : (isSimulating ? "text-amber" : "text-muted")}>{isLocked ? 'LOCKED' : (isSimulating ? 'SEARCHING' : 'OFFLINE')}</span>
            </div>
          </div>

          <div className="status-indicator">
            <div className="status-indicator-header">FEED</div>
            <div className="status-indicator-value">
              <span style={{ color: isSimulating ? 'var(--color-status-pass)' : 'var(--color-status-offline)', fontSize: '8px' }}>●</span>
              <span className={isSimulating ? "text-green" : "text-muted"}>{isSimulating ? 'LIVE' : 'OFFLINE'}</span>
            </div>
          </div>

          <div className="status-indicator">
            <div className="status-indicator-header">INPUT</div>
            <div className="status-indicator-value">
              <span style={{ color: isLive ? 'var(--color-status-pass)' : (t ? 'var(--color-status-info)' : 'var(--color-status-offline)'), fontSize: '8px' }}>●</span>
              <span style={{ color: 'var(--color-text-primary)' }}>{isLive ? 'SIMULATION' : (t ? 'MP4' : 'N/A')}</span>
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          <div className="status-indicator">
            <div className="status-indicator-header">SIM TIME</div>
            <div className="status-indicator-value" style={{ fontWeight: 'normal' }}>
              {isSimulating ? new Date(t.timestamp * 1000).toISOString().substr(11, 8) : '--:--:--'}
            </div>
          </div>
          <div className="status-indicator">
            <div className="status-indicator-header">UTC</div>
            <div className="status-indicator-value" style={{ fontWeight: 'normal' }}>
              {new Date().toISOString().replace('T', ' ').substr(0, 19)}
            </div>
          </div>
          
          {/* Theme Toggle */}
          <div 
            onClick={toggleTheme}
            style={{ 
              display: 'flex', 
              alignItems: 'center', 
              cursor: 'pointer',
              marginLeft: '12px',
              padding: '4px 8px',
              borderRadius: 'var(--radius-sm)',
              backgroundColor: 'var(--color-bg-panel)',
              border: '1px solid var(--color-border)',
              userSelect: 'none'
            }}
            title={`Switch to ${theme === 'dark' ? 'Light' : 'Dark'} Mode`}
          >
            {theme === 'dark' ? (
              <span style={{ fontSize: '14px', lineHeight: 1 }}>☀</span> // Sun for switching to light
            ) : (
              <span style={{ fontSize: '14px', lineHeight: 1 }}>☾</span> // Moon for switching to dark
            )}
          </div>
        </div>
        
      </div>
    </div>
  );
}
