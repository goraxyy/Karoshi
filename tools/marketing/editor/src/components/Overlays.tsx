// Everything drawn over a scene for a while: the hook, labels, lower thirds, pictures (images,
// GIFs, Lottie), arrows and rings that draw themselves, and meme formats. Each lives in its
// own Sequence, so its frame counts from when it appears.
import React, { useEffect, useState } from 'react';
import { AbsoluteFill, Img, continueRender, delayRender, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig } from 'remotion';
import { Gif } from '@remotion/gif';
import { Lottie, type LottieAnimationData } from '@remotion/lottie';
import { brand } from '../brand.ts';
import { FONT } from '../fonts.ts';
import { colour, textFor } from '../lib/timeline.ts';
import type { Arrow, Circle, Hook, Label, Language, LowerThird, Meme, Overlay, Picture, Visual } from '../lib/types.ts';
import { Emphasis, SAFE, outline, useUnit, useVertical } from '../style.tsx';

export const OverlayView: React.FC<{ overlay: Overlay; lang: Language; visual: Visual }> = ({ overlay, lang, visual }) => {
  switch (overlay.type) {
    case 'hook': return <HookView o={overlay} lang={lang} />;
    case 'label': return <LabelView o={overlay} lang={lang} />;
    case 'lowerThird': return <LowerThirdView o={overlay} lang={lang} />;
    case 'image': case 'gif': case 'lottie': return <PictureView o={overlay} />;
    case 'arrow': return <ArrowView o={overlay} />;
    case 'circle': return <CircleView o={overlay} />;
    case 'meme': return <MemeView o={overlay} lang={lang} visual={visual} />;
  }
};

function usePop(delay = 0): number {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  return spring({ frame: frame - delay, fps, config: { damping: 13, stiffness: 170, mass: 0.7 } });
}

// ---- words -------------------------------------------------------------------------------------

const HookView: React.FC<{ o: Hook; lang: Language }> = ({ o, lang }) => {
  const unit = useUnit();
  const vertical = useVertical();
  const { height } = useVideoConfig();
  const pop = usePop();
  return (
    <AbsoluteFill style={{ alignItems: 'center', paddingTop: vertical ? height * SAFE.top : 60 * unit }}>
      <div style={{
        maxWidth: vertical ? '86%' : '70%', padding: `${18 * unit}px ${30 * unit}px`, borderRadius: 30 * unit,
        backgroundColor: brand.colours.paper, color: brand.colours.ink, fontFamily: FONT, fontWeight: 900,
        fontSize: (vertical ? 76 : 64) * unit, lineHeight: 1.1, textAlign: 'center',
        transform: `scale(${0.7 + 0.3 * pop}) rotate(${(1 - pop) * -3}deg)`, opacity: Math.min(1, pop * 1.4),
        boxShadow: `0 ${10 * unit}px 0 ${brand.colours.crimson}`,
      }}>
        <Emphasis text={textFor(o.text, lang)} />
      </div>
    </AbsoluteFill>
  );
};

const LabelView: React.FC<{ o: Label; lang: Language }> = ({ o, lang }) => {
  const unit = useUnit();
  const { width, height } = useVideoConfig();
  const pop = usePop();
  const size = { s: 32, m: 44, l: 60 }[o.size ?? 'm'] * unit;
  const style = o.style ?? 'crimson';
  const bg = style === 'crimson' ? brand.colours.crimson : style === 'ink' ? brand.colours.ink : brand.colours.paper;
  const fg = style === 'paper' ? brand.colours.ink : brand.colours.paper;
  return (
    <div style={{
      position: 'absolute', left: o.x * width, top: o.y * height, transform: `translate(-50%, -50%) scale(${pop})`,
      padding: `${size * 0.22}px ${size * 0.5}px`, borderRadius: 999, backgroundColor: bg, color: fg,
      fontFamily: FONT, fontWeight: 900, fontSize: size, whiteSpace: 'nowrap', boxShadow: '0 8px 24px rgba(0,0,0,0.35)',
    }}>
      <Emphasis text={textFor(o.text, lang)} accent={style === 'crimson' ? brand.colours.ink : brand.colours.crimson} />
    </div>
  );
};

const LowerThirdView: React.FC<{ o: LowerThird; lang: Language }> = ({ o, lang }) => {
  const unit = useUnit();
  const vertical = useVertical();
  const { height } = useVideoConfig();
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const inX = spring({ frame, fps, config: { damping: 18, stiffness: 120 } });
  const sub = textFor(o.subtitle, lang);
  return (
    <div style={{
      // Above the captions: on 16:9 they take the bottom ~200 px, on 9:16 the safe area's foot.
      position: 'absolute', left: 48 * unit, top: vertical ? height * 0.6 : undefined, bottom: vertical ? undefined : 215 * unit,
      transform: `translateX(${(1 - inX) * -120}%)`, display: 'flex', alignItems: 'stretch',
      boxShadow: '0 10px 30px rgba(0,0,0,0.45)', borderRadius: 18 * unit, overflow: 'hidden', maxWidth: vertical ? '80%' : '50%',
    }}>
      <div style={{ width: 14 * unit, backgroundColor: brand.colours.crimson }} />
      <div style={{ backgroundColor: brand.colours.ink, padding: `${16 * unit}px ${26 * unit}px` }}>
        <div style={{ color: brand.colours.paper, fontFamily: FONT, fontWeight: 900, fontSize: 48 * unit, lineHeight: 1.1 }}>
          <Emphasis text={textFor(o.title, lang)} />
        </div>
        {sub && <div style={{ color: brand.colours.mist, fontFamily: FONT, fontWeight: 700, fontSize: 32 * unit, marginTop: 6 * unit }}>{sub}</div>}
      </div>
    </div>
  );
};

// ---- pictures ------------------------------------------------------------------------------------

const PictureView: React.FC<{ o: Picture }> = ({ o }) => {
  const { width, height } = useVideoConfig();
  const frame = useCurrentFrame();
  const pop = usePop();
  const w = o.width * width;
  const enter = o.enter ?? 'pop';
  const scale = enter === 'pop' ? pop : 1;
  const opacity = enter === 'fade' ? interpolate(frame, [0, 10], [0, 1], { extrapolateRight: 'clamp' }) : enter === 'pop' ? Math.min(1, pop * 1.5) : 1;
  const lift = enter === 'slide-up' ? (1 - pop) * 0.2 * height : 0;
  return (
    <div style={{
      position: 'absolute', left: o.x * width, top: o.y * height + lift, width: w,
      transform: `translate(-50%, -50%) rotate(${o.rotate ?? 0}deg) scale(${scale})`, opacity,
    }}>
      {o.type === 'image' && <Img src={staticFile(o.src)} style={{ width: '100%', display: 'block' }} />}
      {o.type === 'gif' && <Gif src={staticFile(o.src)} width={w} height={w} fit="contain" />}
      {o.type === 'lottie' && <LottieFile src={o.src} size={w} />}
    </div>
  );
};

const LottieFile: React.FC<{ src: string; size: number }> = ({ src, size }) => {
  const [data, setData] = useState<LottieAnimationData | null>(null);
  const [handle] = useState(() => delayRender(`Loading ${src}`));
  useEffect(() => {
    fetch(staticFile(src))
      .then((r) => r.json())
      .then((json) => { setData(json); continueRender(handle); })
      .catch((e) => { console.error(e); continueRender(handle); });
  }, [src, handle]);
  if (!data) return null;
  return <Lottie animationData={data} loop style={{ width: size, height: size }} />;
};

// ---- marks that draw themselves -----------------------------------------------------------------

const DRAW_SECONDS = 0.5;

function useDraw(): number {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  return interpolate(frame, [0, DRAW_SECONDS * fps], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: (t) => 1 - (1 - t) ** 3 });
}

const ArrowView: React.FC<{ o: Arrow }> = ({ o }) => {
  const { width, height } = useVideoConfig();
  const unit = useUnit();
  const drawn = useDraw();
  const x1 = o.from_x * width, y1 = o.from_y * height, x2 = o.to_x * width, y2 = o.to_y * height;
  const dx = x2 - x1, dy = y2 - y1, length = Math.hypot(dx, dy);
  const bend = (o.curve ?? 0.25) * length;
  const cx = (x1 + x2) / 2 - (dy / Math.max(1, length)) * bend, cy = (y1 + y2) / 2 + (dx / Math.max(1, length)) * bend;
  // The curve's length, near enough, for drawing it on.
  let path = 0;
  let px = x1, py = y1;
  for (let i = 1; i <= 24; i++) {
    const t = i / 24;
    const qx = (1 - t) ** 2 * x1 + 2 * (1 - t) * t * cx + t * t * x2, qy = (1 - t) ** 2 * y1 + 2 * (1 - t) * t * cy + t * t * y2;
    path += Math.hypot(qx - px, qy - py);
    px = qx; py = qy;
  }
  const angle = Math.atan2(y2 - cy, x2 - cx);
  const head = 34 * unit;
  const stroke = colour(o.color, brand, brand.colours.crimson);
  const w = 12 * unit;
  return (
    <AbsoluteFill>
      <svg width={width} height={height} style={{ overflow: 'visible', filter: 'drop-shadow(0 4px 8px rgba(0,0,0,0.5))' }}>
        <path d={`M ${x1} ${y1} Q ${cx} ${cy} ${x2} ${y2}`} fill="none" stroke={stroke} strokeWidth={w} strokeLinecap="round"
          strokeDasharray={path} strokeDashoffset={path * (1 - drawn)} />
        <path d={`M ${x2 - head * Math.cos(angle - 0.5)} ${y2 - head * Math.sin(angle - 0.5)} L ${x2} ${y2} L ${x2 - head * Math.cos(angle + 0.5)} ${y2 - head * Math.sin(angle + 0.5)}`}
          fill="none" stroke={stroke} strokeWidth={w} strokeLinecap="round" strokeLinejoin="round" opacity={drawn >= 0.98 ? 1 : 0} />
      </svg>
    </AbsoluteFill>
  );
};

const CircleView: React.FC<{ o: Circle }> = ({ o }) => {
  const { width, height } = useVideoConfig();
  const unit = useUnit();
  const drawn = useDraw();
  const r = o.radius * width;
  // A little wider than tall and a little tilted, the way a hand draws a ring; it overshoots
  // its start so the ends overlap.
  const rx = r * 1.12, ry = r * 0.92;
  const around = Math.PI * (3 * (rx + ry) - Math.sqrt((3 * rx + ry) * (rx + 3 * ry))) * 1.08;
  return (
    <AbsoluteFill>
      <svg width={width} height={height} style={{ overflow: 'visible', filter: 'drop-shadow(0 4px 8px rgba(0,0,0,0.5))' }}>
        <ellipse cx={o.x * width} cy={o.y * height} rx={rx} ry={ry} transform={`rotate(-8 ${o.x * width} ${o.y * height})`}
          fill="none" stroke={colour(o.color, brand, brand.colours.crimson)} strokeWidth={11 * unit} strokeLinecap="round"
          strokeDasharray={around} strokeDashoffset={around * (1 - drawn)} />
      </svg>
    </AbsoluteFill>
  );
};

// ---- memes -----------------------------------------------------------------------------------------

const MEME_WORDS: Record<Language, { expectation: string; reality: string; nobody: string }> = {
  en: { expectation: 'Expectation', reality: 'Reality', nobody: 'Nobody:' },
  ru: { expectation: 'Ожидание', reality: 'Реальность', nobody: 'Никто:' },
};

const MemeView: React.FC<{ o: Meme; lang: Language; visual: Visual }> = ({ o, lang, visual }) => {
  const unit = useUnit();
  const vertical = useVertical();
  const { width, height } = useVideoConfig();
  const pop = usePop();
  const words = MEME_WORDS[lang];
  const big = (vertical ? 84 : 72) * unit;
  const shout: React.CSSProperties = {
    fontFamily: FONT, fontWeight: 900, fontSize: big, color: brand.colours.paper, textTransform: 'uppercase',
    textShadow: outline(6 * unit), textAlign: 'center', lineHeight: 1.05, maxWidth: '90%',
  };

  switch (o.template) {
    case 'pov':
      return (
        <AbsoluteFill style={{ alignItems: 'center', paddingTop: vertical ? height * SAFE.top : 50 * unit }}>
          <div style={{ ...shout, textTransform: 'none', fontSize: big * 0.8, transform: `scale(${pop})` }}>
            <span style={{ color: brand.colours.crimson }}>POV: </span><Emphasis text={textFor(o.text, lang)} />
          </div>
        </AbsoluteFill>
      );
    case 'top-bottom':
      return (
        <AbsoluteFill style={{ alignItems: 'center', justifyContent: 'space-between',
          paddingTop: vertical ? height * SAFE.top : 40 * unit, paddingBottom: vertical ? height * SAFE.bottom : 40 * unit }}>
          <div style={{ ...shout, transform: `scale(${pop})` }}><Emphasis text={textFor(o.top, lang)} /></div>
          <div style={{ ...shout, transform: `scale(${usePopLate(pop)})` }}><Emphasis text={textFor(o.bottom, lang)} /></div>
        </AbsoluteFill>
      );
    case 'nobody':
    case 'caption-bar': {
      const nobody = o.template === 'nobody';
      const speaker = textFor(o.speaker, lang) || brand.antagonist.name;
      return (
        <div style={{
          position: 'absolute', left: 0, right: 0, top: vertical ? height * SAFE.top * 0.6 : 0,
          backgroundColor: brand.colours.paper, color: brand.colours.ink, fontFamily: FONT, fontWeight: 800,
          fontSize: (vertical ? 56 : 48) * unit, lineHeight: 1.25, padding: `${26 * unit}px ${44 * unit}px`,
          transform: `translateY(${(1 - pop) * -30}%)`, boxShadow: '0 10px 30px rgba(0,0,0,0.35)',
        }}>
          {nobody ? (
            <>
              <div>{words.nobody}</div>
              <div><span style={{ color: brand.colours.crimson }}>{speaker}:</span> <Emphasis text={textFor(o.text, lang)} /></div>
            </>
          ) : (
            <div style={{ textAlign: 'center' }}><Emphasis text={textFor(o.text, lang)} /></div>
          )}
        </div>
      );
    }
    case 'expectation-reality': {
      const row = visual.type === 'split' && visual.direction === 'row';
      const ratio = visual.type === 'split' ? visual.ratio ?? 0.5 : 0.5;
      const tag = (text: string, left: number, top: number) => (
        <div style={{
          position: 'absolute', left, top, padding: `${8 * unit}px ${20 * unit}px`, borderRadius: 14 * unit,
          backgroundColor: brand.colours.paper, color: brand.colours.ink, fontFamily: FONT, fontWeight: 900, fontSize: 46 * unit,
          transform: `scale(${pop})`, transformOrigin: 'left top', boxShadow: '0 8px 20px rgba(0,0,0,0.35)',
        }}>{text}</div>
      );
      const m = 30 * unit;
      const top0 = vertical ? height * SAFE.top * 0.5 : m;
      return (
        <AbsoluteFill>
          {tag(textFor(o.top, lang) || words.expectation, m, top0)}
          {row ? tag(textFor(o.bottom, lang) || words.reality, width * ratio + m, top0)
               : tag(textFor(o.bottom, lang) || words.reality, m, height * ratio + m)}
        </AbsoluteFill>
      );
    }
  }
};

// The bottom line lands a beat after the top one.
function usePopLate(first: number): number {
  const late = usePop(8);
  return first > 0 ? late : 0;
}
