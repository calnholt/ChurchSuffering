import './ShopColumn.css';
import { subscribe, getAccessibleMysterySlots } from '../store/gameStore.js';
import { shopIconMarkup } from './ShopIcon.js';

function syncSlots(container, slots, tagName) {
  const ids = slots.map((slot) => slot.id);
  const idsKey = ids.join(',');

  if (container.dataset.slotIds === idsKey) {
    return;
  }

  container.dataset.slotIds = idsKey;
  container.innerHTML = slots
    .map((slot) => `<${tagName} slot-id="${slot.id}"></${tagName}>`)
    .join('');
}

export class ShopColumn extends HTMLElement {
  connectedCallback() {
    this.innerHTML = `
      <section class="column column--shop">
        <h2 class="column__title">${shopIconMarkup('title')} Shop</h2>
        <p class="column__subtitle">Spend resources before the shop refreshes</p>
        <div class="column__slots"></div>
      </section>
    `;
    this.slotsEl = this.querySelector('.column__slots');
    this.unsub = subscribe((state) => this.render(state));
  }

  disconnectedCallback() {
    this.unsub?.();
  }

  render(state) {
    syncSlots(this.slotsEl, state.shopSlots, 'shop-space');
  }
}

customElements.define('shop-column', ShopColumn);

export class EnemyColumn extends HTMLElement {
  connectedCallback() {
    this.innerHTML = `
      <section class="column column--enemies">
        <h2 class="column__title">Encounters</h2>
        <p class="column__subtitle">Fight foes for red, white, and black resources</p>
        <div class="column__slots"></div>
      </section>
    `;
    this.slotsEl = this.querySelector('.column__slots');
    this.unsub = subscribe((state) => this.render(state));
  }

  disconnectedCallback() {
    this.unsub?.();
  }

  render(state) {
    syncSlots(this.slotsEl, state.enemySlots, 'enemy-space');
  }
}

customElements.define('enemy-column', EnemyColumn);

export class MysteryColumn extends HTMLElement {
  connectedCallback() {
    this.innerHTML = `
      <section class="column column--mystery">
        <h2 class="column__title">Events</h2>
        <p class="column__subtitle">Timed windows hide medals, foes, and events</p>
        <div class="column__slots"></div>
      </section>
    `;
    this.slotsEl = this.querySelector('.column__slots');
    this.unsub = subscribe((state) => this.render(state));
  }

  disconnectedCallback() {
    this.unsub?.();
  }

  render(state) {
    const accessibleSlots = getAccessibleMysterySlots(state);
    const visible = accessibleSlots.length > 0;
    this.classList.toggle('mystery-column--visible', visible);

    if (!visible) {
      this.slotsEl.innerHTML = '';
      delete this.slotsEl.dataset.slotIds;
      return;
    }

    syncSlots(this.slotsEl, accessibleSlots, 'mystery-space');
  }
}

customElements.define('mystery-column', MysteryColumn);
