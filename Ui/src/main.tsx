import { createRoot } from 'react-dom/client';
import { App } from './App';
import { connect, inGame } from './bridge/bridge';
import { installMock } from './bridge/mock';
import { uiStore } from './state/stores';
import { wireStores } from './state/stores';
import { wireBuilder } from './features/builder/builderUi';
import { wireWorld } from './features/world/WorldLabels';
import { wireLobby } from './features/lobby/lobbyStore';
import { wireShop } from './features/shop/shopStore';
import { wireBoard } from './features/board/boardStore';
import { wireFeedback } from './features/feedback/feedbackStore';
import { wireNav } from './features/nav/navStore';
import { wireWpWorld } from './features/nav/WpWorld';
import { wireLoading } from './features/loading/loadingStore';
import './styles/theme.css';

const applyScale = () => {
  const scale = Math.min(1.8, Math.max(0.7, window.innerHeight / 1080));
  document.documentElement.style.setProperty('--ui', scale.toFixed(3));
};
applyScale();
window.addEventListener('resize', applyScale);

wireStores();
wireBuilder();
wireWorld();
wireLobby();
wireShop();
wireBoard();
wireFeedback();
wireNav();
wireWpWorld();
wireLoading();

window.addEventListener('error', (e) => console.error('[ui] error:', e.message, e.filename, e.lineno));
window.addEventListener('unhandledrejection', (e) => console.error('[ui] rejection:', e.reason));

createRoot(document.getElementById('root')!).render(<App />);

if (inGame()) {
  connect();
} else {
  document.body.classList.add('mock');
  uiStore.set({ resX: window.innerWidth, resY: window.innerHeight });
  installMock();
  connect();
}
