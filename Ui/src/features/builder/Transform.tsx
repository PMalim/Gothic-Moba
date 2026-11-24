import { send } from '../../bridge/bridge';
import type { BuilderSetting } from '../../bridge/protocol';
import { builderStore } from '../../state/stores';
import { useStore } from '../../state/store';

const SCALE_MIN = 0.1;
const SCALE_MAX = 10;
const scaleToSlider = (scale: number): number => (Math.log10(scale / SCALE_MIN) / Math.log10(SCALE_MAX / SCALE_MIN)) * 1000;
const sliderToScale = (v: number): number => SCALE_MIN * Math.pow(SCALE_MAX / SCALE_MIN, v / 1000);

export function Transform() {
  const b = useStore(builderStore);
  const set = (key: BuilderSetting, value: number) => send('builder.set', key, value);

  return (
    <section className="transform">
      <Slider label="Obrót" unit="°" value={b.yaw} min={0} max={359.5} step={0.5} nudge={1} exact onChange={(v) => set('yaw', v)} />
      <Slider label="Pochylenie" unit="°" value={b.pitch} min={0} max={359.5} step={0.5} nudge={1} exact onChange={(v) => set('pitch', v)} />
      <Slider label="Przechylenie" unit="°" value={b.roll} min={0} max={359.5} step={0.5} nudge={1} exact onChange={(v) => set('roll', v)} />
      <Slider
        label="Skala"
        unit="%"
        value={scaleToSlider(b.scale)}
        min={0}
        max={1000}
        step={5}
        display={(b.scale * 100).toFixed(0)}
        nudge={50}
        onChange={(v) => set('scale', sliderToScale(v))}
      />
      <Slider label="Wysokość" unit=" cm" value={b.lift} min={-300} max={600} step={5} nudge={10} onChange={(v) => set('lift', v)} />
      <label className="toggle">
        <input type="checkbox" checked={b.collision} onChange={(e) => set('collision', e.target.checked ? 1 : 0)} />
        <span className="toggle__box" />
        Kolizja <kbd>1</kbd>
      </label>
    </section>
  );
}

function Slider(props: {
  label: string;
  unit: string;
  value: number;
  min: number;
  max: number;
  step: number;
  nudge: number;
  display?: string;
  exact?: boolean;
  onChange: (value: number) => void;
}) {
  const { value, min, max, nudge } = props;
  const clamp = (v: number) => Math.min(max, Math.max(min, v));
  const round1 = (v: number) => Math.round(v * 10) / 10;
  const move = (dir: number, shift: boolean) => {
    const amount = props.exact && shift ? 0.1 : nudge;
    const next = round1(value + dir * amount);
    props.onChange(props.exact ? ((next % 360) + 360) % 360 : clamp(next));
  };
  return (
    <div className="slider">
      <span className="slider__label">{props.label}</span>
      <button className="slider__step" onClick={(e) => move(-1, e.shiftKey)} aria-label={`${props.label} mniej`}>
        −
      </button>
      <input
        type="range"
        min={min}
        max={max}
        step={props.step}
        value={value}
        onChange={(e) => props.onChange(Number(e.target.value))}
        onPointerUp={(e) => e.currentTarget.blur()}
      />
      <button className="slider__step" onClick={(e) => move(1, e.shiftKey)} aria-label={`${props.label} więcej`}>
        +
      </button>
      {props.exact ? (
        <input
          key={round1(value)}
          className="slider__input"
          type="number"
          step={0.1}
          defaultValue={round1(value)}
          aria-label={`${props.label} (stopnie)`}
          onBlur={(e) => {
            const v = Number(e.currentTarget.value);
            if (Number.isFinite(v) && round1(v) !== round1(value)) props.onChange(((v % 360) + 360) % 360);
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') e.currentTarget.blur();
            e.stopPropagation();
          }}
        />
      ) : (
        <output className="slider__value">
          {props.display ?? Math.round(value)}
          {props.unit}
        </output>
      )}
    </div>
  );
}
