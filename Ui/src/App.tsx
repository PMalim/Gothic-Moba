import { AdminPanel } from './features/admin/AdminPanel';
import { Auth } from './features/auth/Auth';
import { Loading, MatchFade } from './features/loading/Loading';
import { Builder } from './features/builder/Builder';
import { Chat } from './features/chat/Chat';
import { FlyHud } from './features/hud/FlyHud';
import { HeroHud } from './features/hud/HeroHud';
import { MatchHud } from './features/hud/MatchHud';
import { Minimap } from './features/minimap/Minimap';
import { Lobby } from './features/lobby/Lobby';
import { lobbyStore } from './features/lobby/lobbyStore';
import { GameMenu } from './features/menu/GameMenu';
import { WorldLabels } from './features/world/WorldLabels';
import { GoldPops } from './features/world/GoldPops';
import { DamagePops } from './features/world/DamagePops';
import { Shop } from './features/shop/Shop';
import { Board } from './features/board/Board';
import { NavEditor } from './features/nav/NavEditor';
import { WpWorld } from './features/nav/WpWorld';
import { useStore } from './state/store';

export function App() {
  const { phase } = useStore(lobbyStore);
  const inLobby = phase === 'lobby';
  return (
    <>
      <Lobby />
      <Auth />
      <div hidden={inLobby}>
        <WorldLabels />
        <GoldPops />
        <DamagePops />
        <HeroHud />
        <MatchHud />
        <Minimap />
        <Chat />
        <Shop />
        <Board />
      </div>
      <FlyHud />
      <Builder />
      <NavEditor />
      <WpWorld />
      <AdminPanel />
      <GameMenu />
      <MatchFade />
      <Loading />
    </>
  );
}
