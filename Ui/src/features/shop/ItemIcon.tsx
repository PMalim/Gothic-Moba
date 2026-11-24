import { itemByKey } from './shopStore';
import { ITEM_IMAGES } from './itemImages';

export function ItemIcon({ itemKey, fallback = '' }: { itemKey: string; fallback?: string }) {
  const item = itemByKey(itemKey);
  const src = item?.shape ? ITEM_IMAGES[item.shape] : undefined;
  if (!src) return <span aria-hidden>{fallback}</span>;
  return <img className="item-icon item-icon--g2" src={src} alt="" draggable={false} />;
}
