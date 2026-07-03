import {
  subscribe,
  interactMystery,
  isSlotExpired,
  isMysteryWindowActive,
  getWouldVanishSlotIds,
} from '../store/gameStore.js';
import { subscribeTimePreview, getTimePreview, clearTimePreview } from '../utils/timePreviewStore.js';
import { spaceSlotMarkup } from '../utils/spaceCardMarkup.js';
import { bindSpaceSlotInteractions } from '../utils/spaceCardInteractions.js';
import { getLeavesTiming } from '../utils/slotLeaveLabel.js';
import { applyLeaveLabel } from '../utils/spaceLeaveLabel.js';
import { spaceTimeBlockMarkup } from '../utils/spaceTimeBlock.js';

export class MysterySpace extends HTMLElement {
  static get observedAttributes() {
    return ['slot-id'];
  }

  connectedCallback() {
    this.unsub = subscribe((state) => this.render(state));
    this.unsubPreview = subscribeTimePreview(() => this.applyPreview());
  }

  disconnectedCallback() {
    this.unsub?.();
    this.unsubPreview?.();
  }

  get slotId() {
    return this.getAttribute('slot-id');
  }

  applyPreview() {
    const preview = getTimePreview();
    const slotEl = this.querySelector('.space-slot');
    if (!slotEl || !this._state) {
      return;
    }
    const slot = this._state.mysterySlots.find((s) => s.id === this.slotId);
    if (!slot) {
      return;
    }
    const vanishIds = getWouldVanishSlotIds(this._state, preview);
    slotEl.classList.toggle('space-slot--would-vanish', vanishIds.has(this.slotId));
    slotEl.classList.toggle('space-slot--preview-source', preview?.slotId === this.slotId);
    applyLeaveLabel(slotEl, slot, this._state);
  }

  render(state) {
    this._state = state;
    const slot = state.mysterySlots.find((s) => s.id === this.slotId);
    if (!slot) {
      this.innerHTML = '';
      return;
    }

    const expired = isSlotExpired(slot);
    const active = isMysteryWindowActive(slot);
    const unavailable = !expired && !active;
    const disabled = expired || !active;
    const leavesTiming = getLeavesTiming(slot, state);

    const compactHtml = `
      <span class="space-card-compact__glyph">?</span>
      <span class="space-card-compact__title">Event</span>
      <span class="space-card-compact__meta">
        ${spaceTimeBlockMarkup(slot.clickCost, leavesTiming)}
      </span>
    `;

    this.innerHTML = spaceSlotMarkup({
      slotId: slot.id,
      kind: 'mystery',
      expired,
      active,
      unavailable,
      compactHtml,
    });

    const slotEl = this.querySelector('.space-slot');
    bindSpaceSlotInteractions(slotEl, {
      slotId: slot.id,
      clickCost: slot.clickCost,
      advancesTime: active && !expired,
      disabled,
    });

    const compact = slotEl.querySelector('.space-card-compact');
    compact.addEventListener('click', () => {
      if (expired) {
        return;
      }
      clearTimePreview();
      interactMystery(this.slotId);
    });

    this.applyPreview();
  }
}

customElements.define('mystery-space', MysterySpace);
