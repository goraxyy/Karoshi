// What a scene's shots need to know to sit in the mix: the voice they duck under, and where
// the scene starts on the video's clock (frames).
import { createContext, useContext } from 'react';
import type { VoiceClip } from './lib/types.ts';

export const MixContext = createContext<{ voice: VoiceClip[]; sceneStart: number }>({ voice: [], sceneStart: 0 });

export const useMix = () => useContext(MixContext);
