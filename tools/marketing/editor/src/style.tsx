// Shared look: sizes that scale with the frame, a soft outline for text over footage, safe
// areas for vertical video, and *emphasis* in crimson.
import React from 'react';
import { useVideoConfig } from 'remotion';
import { brand } from './brand.ts';

// One unit is a pixel on a 1080-pixel short side, so both formats size text alike.
export function useUnit(): number {
  const { width, height } = useVideoConfig();
  return Math.min(width, height) / 1080;
}

export function useVertical(): boolean {
  const { width, height } = useVideoConfig();
  return height > width;
}

// Where platforms put their buttons and captions on vertical video: keep text out of it.
export const SAFE = { top: 0.1, bottom: 0.24, side: 0.07 };

export function outline(px: number, colour = brand.colours.ink): string {
  const s: string[] = [];
  for (let a = 0; a < 16; a++) {
    const r = (a / 16) * Math.PI * 2;
    s.push(`${(Math.cos(r) * px).toFixed(1)}px ${(Math.sin(r) * px).toFixed(1)}px 0 ${colour}`);
  }
  s.push(`0 ${px * 1.5}px ${px * 3}px rgba(0,0,0,0.45)`);
  return s.join(', ');
}

// "She *moved* when you blinked": the starred words in crimson (or `accent`).
export const Emphasis: React.FC<{ text: string; accent?: string }> = ({ text, accent = brand.colours.crimson }) => {
  const parts = text.split(/(\*[^*]+\*)/g).filter((p) => p.length > 0);
  return (
    <>
      {parts.map((p, i) =>
        p.startsWith('*') && p.endsWith('*') ? <span key={i} style={{ color: accent }}>{p.slice(1, -1)}</span> : <span key={i}>{p}</span>,
      )}
    </>
  );
};
