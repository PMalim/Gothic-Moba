import { useEffect, useState } from 'react';
import { useStore } from '../../state/store';
import { lobbyStore, meStore, scoreStore } from '../lobby/lobbyStore';
import { AssistIcon, DeathIcon, KillIcon, MinionIcon } from './icons';
import './matchhud.css';

const clock = (seconds: number): string => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;

export function MatchHud() {
  const { phase } = useStore(lobbyStore);
  const score = useStore(scoreStore);
  const me = useStore(meStore);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (phase !== 'game') return;
    setNow(Date.now());
    const timer = window.setInterval(() => setNow(Date.now()), 500);
    return () => window.clearInterval(timer);
  }, [phase]);
  if (phase !== 'game') return null;

  const seconds = score.seconds + Math.max(0, Math.floor((now - score.base) / 1000));
  return (
    <div className="matchhud" aria-label="Wynik gry">
      <div className="matchhud__main">
      <div className="matchhud__team matchhud__team--blue">
        <b key={`b${score.blue}`} className="matchhud__score">
          {score.blue}
        </b>
      </div>
      <div className="matchhud__clock">
        <span>{clock(seconds)}</span>
      </div>
      <div className="matchhud__team matchhud__team--red">
        <b key={`r${score.red}`} className="matchhud__score">
          {score.red}
        </b>
      </div>
      </div>
      <div className="matchhud__stats" aria-label="Twoje statystyki">
        <span title="Zabici gracze (i boty-bohaterowie)">
          <KillIcon />
          <b>{me.kills}</b>
        </span>
        <span title="Twoje śmierci">
          <DeathIcon />
          <b>{me.deaths}</b>
        </span>
        <span title="Asysty: zraniłeś bohatera, którego dobił ktoś inny (złoto i doświadczenie jak w LoL-u)">
          <AssistIcon />
          <b>{me.assists}</b>
        </span>
        <span title="Zabite miniony i inne NPC">
          <MinionIcon />
          <b>{me.cs}</b>
        </span>
      </div>
    </div>
  );
}
