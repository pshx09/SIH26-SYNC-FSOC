import { useState, useEffect, useRef } from 'react';

export function useResizeObserver<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [dimensions, setDimensions] = useState({ width: 0, height: 0 });

  useEffect(() => {
    if (!ref.current) return;
    
    const element = ref.current;
    
    // Set initial size
    setDimensions({
      width: element.clientWidth,
      height: element.clientHeight
    });
    
    const observer = new ResizeObserver((entries) => {
      if (entries.length > 0) {
        const entry = entries[0];
        setDimensions({
          width: entry.contentRect.width,
          height: entry.contentRect.height
        });
      }
    });
    
    observer.observe(element);
    
    return () => {
      observer.unobserve(element);
      observer.disconnect();
    };
  }, []);

  return { ref, ...dimensions };
}
