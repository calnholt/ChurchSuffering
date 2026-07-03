import enemies from '../data/enemies.json';
import medals from '../data/medals.json';
import equipment from '../data/equipment.json';
import cards from '../data/cards.json';
import events from '../data/events.json';
import { randInt, pick } from './random.js';
import { randomCost, randomRewards } from './resources.js';

export const SHOP_SLOT_TYPES = ['medal', 'equipment', 'cardUpgrade', 'cardReplace'];
export const SHOP_REFRESH_INTERVAL = 8;

export function getNextShopRefreshAt(currentTime) {
  return (Math.floor(currentTime / SHOP_REFRESH_INTERVAL) + 1) * SHOP_REFRESH_INTERVAL;
}

export function getShopRefreshMarkerTimes(maxTime = 40) {
  const markers = [];
  for (let time = SHOP_REFRESH_INTERVAL; time < maxTime; time += SHOP_REFRESH_INTERVAL) {
    markers.push(time);
  }
  return markers;
}

export function rollShopRefresh() {
  return {
    duration: SHOP_REFRESH_INTERVAL,
    lastRefreshAt: 0,
  };
}

let slotCounter = 0;

export function nextSlotId(prefix) {
  slotCounter += 1;
  return `${prefix}-${slotCounter}`;
}

export function rollShopSlot(slotType, spawnTime = 0) {
  let itemType = slotType;
  let item = null;
  let label = '';

  switch (slotType) {
    case 'medal':
      item = pick(medals);
      label = 'Medal';
      break;
    case 'equipment':
      item = pick(equipment);
      label = 'Equipment';
      break;
    case 'cardUpgrade':
      item = pick(cards);
      label = 'Upgrade Card';
      break;
    case 'cardReplace':
      item = pick(cards);
      label = 'Replace Any Card';
      break;
    default:
      itemType = 'medal';
      item = pick(medals);
      label = 'Medal';
  }

  return {
    id: nextSlotId('shop'),
    kind: 'shop',
    slotType: itemType,
    label,
    item,
    cost: randomCost(),
    clickCost: randInt(1, 3),
    spawnTime,
  };
}

export function rollAllShopSlots(spawnTime = 0) {
  return SHOP_SLOT_TYPES.map((slotType) => rollShopSlot(slotType, spawnTime));
}

export function rollEnemySlot(spawnTime = 0) {
  return {
    id: nextSlotId('enemy'),
    kind: 'enemy',
    enemy: pick(enemies),
    rewards: randomRewards(2, 3),
    clickCost: randInt(1, 3),
    duration: randInt(3, 5),
    spawnTime,
  };
}

export function rollMysterySlot(currentTime = 0) {
  const windowStart = currentTime + randInt(3, 8);
  const windowEnd = windowStart + randInt(4, 10);
  const contentTypes = ['medal', 'specialEnemy', 'narrativeEvent'];
  const contentType = pick(contentTypes);

  let payload;
  switch (contentType) {
    case 'medal':
      payload = pick(medals);
      break;
    case 'specialEnemy':
      payload = pick(enemies);
      break;
    default:
      payload = pick(events);
  }

  return {
    id: nextSlotId('mystery'),
    kind: 'mystery',
    windowStart,
    windowEnd,
    contentType,
    payload,
    clickCost: randInt(1, 2),
    duration: windowEnd - currentTime + randInt(1, 3),
    spawnTime: currentTime,
  };
}

export function rollInitialShopSlots() {
  return rollAllShopSlots(0);
}

export function rollInitialEnemySlots() {
  return Array.from({ length: 3 }, () => rollEnemySlot(0));
}

export function rollInitialMysterySlots(currentTime = 0) {
  return Array.from({ length: 3 }, () => rollMysterySlot(currentTime));
}

export function getMysteryContentLabel(slot) {
  switch (slot.contentType) {
    case 'medal':
      return `Medal: ${slot.payload.name}`;
    case 'specialEnemy':
      return `Special Encounter: ${slot.payload.name}`;
    default:
      return slot.payload.name;
  }
}

export function getMysteryContentText(slot) {
  if (slot.contentType === 'narrativeEvent') {
    return slot.payload.text ?? '';
  }
  if (slot.contentType === 'medal') {
    return 'A saintly medal appears from the mist.';
  }
  return 'A formidable foe blocks the path.';
}
