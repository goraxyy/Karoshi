// The last seconds: the name, 気配, the line, what to do next, where to find us.
import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { brand } from '../brand.ts';
import { FONT, JAPANESE } from '../fonts.ts';
import { textFor } from '../lib/timeline.ts';
import type { Language, Text } from '../lib/types.ts';
import { Emphasis, useUnit } from '../style.tsx';

export const EndCard: React.FC<{ lang: Language; cta?: Text; showHandles?: boolean }> = ({ lang, cta, showHandles = true }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const unit = useUnit();
  const rise = spring({ frame, fps, config: { damping: 20, stiffness: 90 } });
  const later = spring({ frame: frame - 10, fps, config: { damping: 20, stiffness: 90 } });
  const glow = interpolate(frame % (fps * 2), [0, fps, fps * 2], [0.35, 0.8, 0.35]);
  const handles = showHandles ? Object.entries(brand.handles).filter(([, h]) => h) : [];
  const action = textFor(cta, lang);
  return (
    <AbsoluteFill style={{ backgroundColor: brand.colours.ink, alignItems: 'center', justifyContent: 'center', gap: 26 * unit }}>
      <div style={{ fontFamily: JAPANESE, fontWeight: 800, fontSize: 110 * unit, color: brand.colours.crimson, opacity: rise,
        textShadow: `0 0 ${40 * unit}px rgba(220,20,60,${glow})` }}>{brand.game.japanese}</div>
      <div style={{ fontFamily: FONT, fontWeight: 900, fontSize: 170 * unit, letterSpacing: 24 * unit, color: brand.colours.paper,
        transform: `translateY(${(1 - rise) * 40}px)`, opacity: rise, marginRight: -24 * unit }}>
        {brand.game.name.toUpperCase()}
      </div>
      <div style={{ fontFamily: FONT, fontWeight: 700, fontSize: 44 * unit, color: brand.colours.mist, opacity: later }}>
        {brand.game.tagline[lang]}
      </div>
      {action && (
        <div style={{ marginTop: 20 * unit, padding: `${16 * unit}px ${40 * unit}px`, borderRadius: 999, backgroundColor: brand.colours.crimson,
          color: brand.colours.paper, fontFamily: FONT, fontWeight: 900, fontSize: 50 * unit, transform: `scale(${later})` }}>
          <Emphasis text={action} accent={brand.colours.ink} />
        </div>
      )}
      {handles.length > 0 && (
        <div style={{ fontFamily: FONT, fontWeight: 700, fontSize: 36 * unit, color: brand.colours.paper, opacity: later }}>
          {handles.map(([site, h]) => `${site}: @${h}`).join('   ·   ')}
        </div>
      )}
      <div style={{ position: 'absolute', bottom: 40 * unit, fontFamily: FONT, fontWeight: 700, fontSize: 28 * unit, color: brand.colours.mist, opacity: later * 0.8 }}>
        {brand.studio}
      </div>
    </AbsoluteFill>
  );
};
