import { builderStore, flyStore } from '../../state/stores';
import { useStore } from '../../state/store';
import './hud.css';

export function FlyHud() {
  const fly = useStore(flyStore);
  const builder = useStore(builderStore);
  if (!fly.on || (builder.on && builder.panelFocus)) return null;
  return (
    <div className="fly-hud" aria-label="Wolna kamera">
      <span className="fly-hud__title">Wolna kamera</span>
      <span>
        prędkość <b>{Math.round(fly.speed)}</b> <small>(kółko myszy)</small>
      </span>
      <span>
        obrót <b>{Math.round(fly.yaw)}°</b> pochylenie <b>{Math.round(fly.pitch)}°</b>
      </span>
      <span className="fly-hud__keys">
        <kbd>W</kbd>
        <kbd>A</kbd>
        <kbd>S</kbd>
        <kbd>D</kbd> <kbd>Spacja</kbd>/<kbd>X</kbd> góra/dół · <kbd>Shift</kbd> szybciej · <kbd>F7</kbd> wyjście
      </span>
    </div>
  );
}
