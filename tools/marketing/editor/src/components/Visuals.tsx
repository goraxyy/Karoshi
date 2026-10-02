// What a scene shows underneath everything else: a shot (trimmed, sped up or down, zoomed and
// panned), two shots split, an image, or a flat colour.
import React from 'react';
import { AbsoluteFill, Img, OffthreadVideo, Sequence, staticFile, useCurrentFrame, useVideoConfig } from 'remotion';
import { brand } from '../brand.ts';
import { HEADROOM, colour, gameVolume, speedSegments, textFor, toFrames, zoomAt } from '../lib/timeline.ts';
import { useMix } from '../mix.ts';
import type { Language, Shot, Visual, ZoomKey } from '../lib/types.ts';
import { FONT } from '../fonts.ts';
import { Emphasis, useUnit } from '../style.tsx';

// The frame eases between zoom keys; the zoom centres on (x, y), so moving it pans.
const Framed: React.FC<{ zoom?: ZoomKey[]; children: React.ReactNode }> = ({ zoom, children }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const z = zoomAt(zoom, frame / fps);
  return (
    <AbsoluteFill style={{ overflow: 'hidden' }}>
      <AbsoluteFill style={{ transform: `scale(${z.scale})`, transformOrigin: `${z.x * 100}% ${z.y * 100}%` }}>{children}</AbsoluteFill>
    </AbsoluteFill>
  );
};

export const ShotView: React.FC<{ shot: Shot; duration: number; lang: Language }> = ({ shot, duration, lang }) => {
  const { fps } = useVideoConfig();
  const unit = useUnit();
  const mix = useMix();
  const segments = speedSegments(shot, duration);
  const label = textFor(shot.label, lang);
  return (
    <AbsoluteFill style={{ backgroundColor: brand.colours.ink }}>
      <Framed zoom={shot.zoom}>
        {segments.map((s, i) => {
          const from = toFrames(s.from, fps);
          const length = Math.max(1, toFrames(s.to, fps) - from);
          return (
            <Sequence key={i} from={from} durationInFrames={length}>
              <OffthreadVideo
                src={staticFile(shot.src)}
                trimBefore={Math.round(s.source * fps)}
                playbackRate={s.rate}
                volume={(f) => HEADROOM * gameVolume(shot.volume, mix.voice, (mix.sceneStart + from + f) / fps)}
                muted={shot.volume === 0}
                style={{ width: '100%', height: '100%', objectFit: shot.fit ?? 'cover' }}
              />
            </Sequence>
          );
        })}
      </Framed>
      {label && (
        <div style={{
          position: 'absolute', left: 24 * unit, top: 24 * unit, padding: `${6 * unit}px ${16 * unit}px`, borderRadius: 999,
          backgroundColor: brand.colours.ink, color: brand.colours.paper, fontFamily: FONT, fontWeight: 900, fontSize: 34 * unit,
        }}>
          <Emphasis text={label} />
        </div>
      )}
    </AbsoluteFill>
  );
};

export const VisualView: React.FC<{ visual: Visual; duration: number; lang: Language }> = ({ visual, duration, lang }) => {
  const unit = useUnit();
  switch (visual.type) {
    case 'shot':
      return <ShotView shot={visual} duration={duration} lang={lang} />;
    case 'split': {
      const row = (visual.direction ?? 'column') === 'row';
      const ratio = visual.ratio ?? 0.5;
      const gap = 6 * unit;
      const half = (shot: Shot, share: number) => (
        <div style={{ position: 'relative', flex: `${share} 1 0`, overflow: 'hidden' }}>
          <ShotView shot={shot} duration={duration} lang={lang} />
        </div>
      );
      return (
        <AbsoluteFill style={{ display: 'flex', flexDirection: row ? 'row' : 'column', gap, backgroundColor: brand.colours.crimson }}>
          {half(visual.a, ratio)}
          {half(visual.b, 1 - ratio)}
        </AbsoluteFill>
      );
    }
    case 'image':
      return (
        <AbsoluteFill style={{ backgroundColor: brand.colours.ink }}>
          <Framed zoom={visual.zoom}>
            <Img src={staticFile(visual.src)} style={{ width: '100%', height: '100%', objectFit: visual.fit ?? 'cover' }} />
          </Framed>
        </AbsoluteFill>
      );
    default:
      return <AbsoluteFill style={{ backgroundColor: colour(visual.color, brand, brand.colours.ink) }} />;
  }
};
