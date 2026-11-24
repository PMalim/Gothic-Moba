/// <reference types="vite/client" />
import leeFace from './lee-portrait.jpg';
import leeCard from './lee-card.jpg';
import diegoFace from './diego-portrait.jpg';
import diegoCard from './diego-card.jpg';

import thorusFace from './thorus-portrait.jpg';
import thorusCard from './thorus-card.jpg';
import cipherFace from './cipher-portrait.jpg';
import cipherCard from './cipher-card.jpg';
import xardasFace from './xardas-portrait.jpg';
import xardasCard from './xardas-card.jpg';
import ravenFace from './raven-portrait.jpg';
import ravenCard from './raven-card.jpg';
import urshakFace from './urshak-portrait.jpg';
import urshakCard from './urshak-card.jpg';

export const PORTRAIT: Record<string, { face: string; card: string }> = {
  kruk: { face: ravenFace, card: ravenCard },
  urshak: { face: urshakFace, card: urshakCard },
  xardas: { face: xardasFace, card: xardasCard },
  cipher: { face: cipherFace, card: cipherCard },
  thorus: { face: thorusFace, card: thorusCard },
  lee: { face: leeFace, card: leeCard },
  diego: { face: diegoFace, card: diegoCard },
};
