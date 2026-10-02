// The editor's one composition, sized and timed by the edit it's given (calculateMetadata).
// `npm run studio` previews the sample; `node render.mjs <edit.json>` renders any edit.
import React from 'react';
import { Composition } from 'remotion';
import sample from '../samples/short_catch.json';
import { EditVideo } from './EditVideo.tsx';
import { SIZES, totalFrames } from './lib/timeline.ts';
import type { Props } from './lib/types.ts';

const defaults: Props = { edit: sample as unknown as Props['edit'], lang: 'en' };

export const RemotionRoot: React.FC = () => (
  <Composition
    id="Edit"
    component={EditVideo}
    defaultProps={defaults}
    durationInFrames={300}
    fps={30}
    width={1080}
    height={1920}
    calculateMetadata={({ props }) => ({
      durationInFrames: Math.max(1, totalFrames(props.edit)),
      fps: props.edit.fps,
      ...SIZES[props.edit.format],
    })}
  />
);
