// One scene: its picture, a picture in picture, what's drawn over it, and its sound effects.
import React from 'react';
import { AbsoluteFill, Audio, Sequence, staticFile, useVideoConfig } from 'remotion';
import { HEADROOM, toFrames } from '../lib/timeline.ts';
import type { Language, Scene } from '../lib/types.ts';
import { OverlayView } from './Overlays.tsx';
import { PipView } from './Pip.tsx';
import { VisualView } from './Visuals.tsx';

export const SceneView: React.FC<{ scene: Scene; lang: Language }> = ({ scene, lang }) => {
  const { fps } = useVideoConfig();
  const length = toFrames(scene.duration, fps);
  const span = (from: number | undefined, to: number | undefined) => {
    const a = toFrames(from ?? 0, fps);
    const b = Math.min(length, toFrames(to ?? scene.duration, fps));
    return { from: a, durationInFrames: Math.max(1, b - a) };
  };
  return (
    <AbsoluteFill>
      <VisualView visual={scene.visual} duration={scene.duration} lang={lang} />
      {scene.pip && (
        <Sequence {...span(scene.pip.from, scene.pip.to)} layout="absolute-fill">
          <PipView pip={scene.pip} lang={lang} />
        </Sequence>
      )}
      {(scene.overlays ?? []).map((o, i) => (
        <Sequence key={i} {...span(o.from, o.to)} layout="absolute-fill">
          <OverlayView overlay={o} lang={lang} visual={scene.visual} />
        </Sequence>
      ))}
      {(scene.sfx ?? []).map((s, i) => (
        <Sequence key={`sfx${i}`} from={toFrames(s.at, fps)}>
          <Audio src={staticFile(s.src)} volume={HEADROOM * (s.volume ?? 1)} />
        </Sequence>
      ))}
    </AbsoluteFill>
  );
};
