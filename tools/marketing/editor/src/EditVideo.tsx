// The one composition: an edit.json played as a video in one language. Scenes in a
// TransitionSeries (each overlapping the one before by its transition), the voice and music on
// the video's clock, captions over everything, then the end card.
import React from 'react';
import { AbsoluteFill, Audio, Sequence, staticFile, useVideoConfig } from 'remotion';
import { TransitionSeries, linearTiming, type TransitionPresentation } from '@remotion/transitions';
import { clockWipe } from '@remotion/transitions/clock-wipe';
import { fade } from '@remotion/transitions/fade';
import { flip } from '@remotion/transitions/flip';
import { slide } from '@remotion/transitions/slide';
import { wipe } from '@remotion/transitions/wipe';
import { brand } from './brand.ts';
import { Captions } from './components/Captions.tsx';
import { EndCard } from './components/EndCard.tsx';
import { SceneView } from './components/Scene.tsx';
import { HEADROOM, endCardFrames, musicVolume, sceneFrames, sceneStarts, scenesEnd, toFrames, totalFrames, transitionFrames } from './lib/timeline.ts';
import { MixContext } from './mix.ts';
import type { Props, Transition } from './lib/types.ts';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function presentation(t: Transition, width: number, height: number): TransitionPresentation<any> {
  const direction = t.direction ?? 'from-right';
  switch (t.type) {
    case 'slide': return slide({ direction });
    case 'wipe': return wipe({ direction });
    case 'flip': return flip({ direction });
    case 'clock': return clockWipe({ width, height });
    default: return fade();
  }
}

export const EditVideo: React.FC<Props> = ({ edit, lang }) => {
  const { fps, width, height } = useVideoConfig();
  const total = totalFrames(edit) / fps;
  const voice = edit.voice?.[lang] ?? [];
  const end = scenesEnd(edit);
  const starts = sceneStarts(edit);

  return (
    <AbsoluteFill style={{ backgroundColor: brand.colours.ink }}>
      <TransitionSeries>
        {edit.scenes.flatMap((scene, i) => {
          const parts: React.ReactNode[] = [];
          const overlap = transitionFrames(edit, i);
          if (overlap > 0 && scene.transition)
            parts.push(
              <TransitionSeries.Transition key={`t${i}`} presentation={presentation(scene.transition, width, height)}
                timing={linearTiming({ durationInFrames: overlap })} />,
            );
          parts.push(
            <TransitionSeries.Sequence key={`s${i}`} durationInFrames={sceneFrames(edit, i)}>
              <MixContext.Provider value={{ voice, sceneStart: starts[i] }}>
                <SceneView scene={scene} lang={lang} />
              </MixContext.Provider>
            </TransitionSeries.Sequence>,
          );
          return parts;
        })}
      </TransitionSeries>

      {edit.endCard && (
        <Sequence from={end} durationInFrames={endCardFrames(edit)}>
          <EndCard lang={lang} cta={edit.endCard.cta} showHandles={edit.endCard.showHandles} />
        </Sequence>
      )}

      <Sequence durationInFrames={end}>
        <Captions edit={edit} lang={lang} />
      </Sequence>

      {voice.map((clip, i) => (
        <Sequence key={`v${i}`} from={toFrames(clip.at, fps)}>
          <Audio src={staticFile(clip.src)} volume={HEADROOM * (clip.volume ?? 1)} />
        </Sequence>
      ))}

      {edit.music && (
        <Audio src={staticFile(edit.music.src)} loop trimBefore={toFrames(edit.music.offset ?? 0, fps)}
          volume={(f) => HEADROOM * musicVolume(edit.music!, voice, f / fps, total)} />
      )}
    </AbsoluteFill>
  );
};
