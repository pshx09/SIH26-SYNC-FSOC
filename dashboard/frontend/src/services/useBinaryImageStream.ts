import { useState, useEffect, useRef } from 'react';

export type StreamStatus = 'LIVE' | 'STALE' | 'OFFLINE';

export function useBinaryImageStream(url: string) {
  const [imageSrc, setImageSrc] = useState<string | null>(null);
  const [status, setStatus] = useState<StreamStatus>('OFFLINE');
  const [dataAgeMs, setDataAgeMs] = useState<number>(0);
  const [frameSize, setFrameSize] = useState<number>(0);
  const [fps, setFps] = useState<number>(0);

  const wsRef = useRef<WebSocket | null>(null);
  const lastReceivedTimeRef = useRef<number>(0);
  const frameCountRef = useRef<number>(0);
  const fpsIntervalRef = useRef<number>(0);

  useEffect(() => {
    let reconnectTimeout: ReturnType<typeof setTimeout>;
    let isSubscribed = true;

    const connect = () => {
      const ws = new WebSocket(url);
      ws.binaryType = 'blob';

      ws.onopen = () => {
        if (!isSubscribed) return;
        setStatus('LIVE');
      };

      ws.onmessage = (event) => {
        if (!isSubscribed) return;
        
        if (event.data instanceof Blob) {
          const blob = event.data;
          setFrameSize(blob.size);
          
          const newUrl = URL.createObjectURL(blob);
          
          setImageSrc((prevUrl) => {
            if (prevUrl) {
              URL.revokeObjectURL(prevUrl); // Cleanup old frame
            }
            return newUrl;
          });

          lastReceivedTimeRef.current = Date.now();
          frameCountRef.current++;
        }
      };

      ws.onclose = () => {
        if (!isSubscribed) return;
        setStatus('OFFLINE');
        reconnectTimeout = setTimeout(connect, 2000);
      };

      ws.onerror = (err) => {
        console.error(`[Stream] WebSocket error on ${url}:`, err);
        ws.close();
      };

      wsRef.current = ws;
    };

    connect();

    // Data age loop
    const ageInterval = setInterval(() => {
      if (lastReceivedTimeRef.current > 0) {
        const age = Date.now() - lastReceivedTimeRef.current;
        setDataAgeMs(age);
        if (age > 2000 && wsRef.current?.readyState === WebSocket.OPEN) {
          setStatus('STALE');
        } else if (age <= 2000 && wsRef.current?.readyState === WebSocket.OPEN) {
          setStatus('LIVE');
        }
      }
    }, 100);

    // FPS loop
    fpsIntervalRef.current = window.setInterval(() => {
      setFps(frameCountRef.current);
      frameCountRef.current = 0;
    }, 1000);

    return () => {
      isSubscribed = false;
      clearTimeout(reconnectTimeout);
      clearInterval(ageInterval);
      clearInterval(fpsIntervalRef.current);
      if (wsRef.current) {
        wsRef.current.close();
      }
      setImageSrc((prevUrl) => {
        if (prevUrl) URL.revokeObjectURL(prevUrl);
        return null;
      });
    };
  }, [url]);

  return { imageSrc, status, dataAgeMs, frameSize, fps };
}
