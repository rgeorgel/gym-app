// Shared UI utilities
import { t, getLocale, getWeekdays, getWeekdaysFull } from './i18n.js?v=202603311200';
export { getWeekdays, getWeekdaysFull };

// Toast notifications
let toastContainer;
function getToastContainer() {
  if (!toastContainer) {
    toastContainer = document.createElement('div');
    toastContainer.className = 'toast-container';
    document.body.appendChild(toastContainer);
  }
  return toastContainer;
}

export function showToast(message, type = 'info', duration = 3500) {
  const container = getToastContainer();
  const toast = document.createElement('div');
  toast.className = `toast toast-${type}`;
  toast.textContent = message;
  container.appendChild(toast);
  setTimeout(() => toast.remove(), duration);
}

// Modal
export function openModal(id) { document.getElementById(id)?.classList.remove('hidden'); }
export function closeModal(id) { document.getElementById(id)?.classList.add('hidden'); }
// Expose globally so inline onclick="closeModal(...)" in modal footers works
window.closeModal = closeModal;

export function createModal({ id, title, body, footer = '' }) {
  const existing = document.getElementById(id);
  if (existing) existing.remove();

  const overlay = document.createElement('div');
  overlay.id = id;
  overlay.className = 'modal-overlay hidden';
  overlay.innerHTML = `
    <div class="modal" role="dialog" aria-modal="true" aria-labelledby="${id}-title">
      <div class="modal-header">
        <h2 class="modal-title" id="${id}-title">${title}</h2>
        <button class="modal-close" aria-label="Fechar">✕</button>
      </div>
      <div class="modal-body">${body}</div>
      ${footer ? `<div class="modal-footer">${footer}</div>` : ''}
    </div>
  `;
  document.body.appendChild(overlay);

  // Close via X button or Escape key
  overlay.querySelector('.modal-close').addEventListener('click', () => closeModal(id));
  const onKeyDown = (e) => { if (e.key === 'Escape') { closeModal(id); document.removeEventListener('keydown', onKeyDown); } };
  document.addEventListener('keydown', onKeyDown);
  return overlay;
}

// Loading state
export function setLoading(el, loading, text = 'Carregando...') {
  if (typeof el === 'string') el = document.getElementById(el);
  if (!el) return;
  if (loading) {
    el._originalHTML = el.innerHTML;
    el.innerHTML = `<span class="spinner"></span>`;
    if (el.tagName === 'BUTTON') el.disabled = true;
  } else {
    el.innerHTML = el._originalHTML || '';
    if (el.tagName === 'BUTTON') el.disabled = false;
  }
}

// Format helpers
export function formatDate(dateStr) {
  if (!dateStr) return '—';
  const d = new Date(dateStr);
  return d.toLocaleDateString(getLocale());
}

export function formatDateTime(dateStr) {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleString(getLocale());
}

export function formatTime(timeStr) {
  // "07:00:00" → "07:00"
  return timeStr?.slice(0, 5) ?? '—';
}

export function formatCurrency(value) {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
}

// Kept for backward compat — prefer getWeekdays() / getWeekdaysFull() for locale-aware arrays
export const WEEKDAYS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
export const WEEKDAYS_FULL = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'];

export function badge(text, type = 'gray') {
  return `<span class="badge badge-${type}">${text}</span>`;
}

const STATUS_TYPE = {
  Confirmed: 'success', CheckedIn: 'info', Cancelled: 'danger',
  Active: 'success', Inactive: 'gray', Suspended: 'danger',
};
const STATUS_KEY = {
  Confirmed: 'status.confirmed', CheckedIn: 'status.checkedIn', Cancelled: 'status.cancelled',
  Active: 'status.active', Inactive: 'status.inactive', Suspended: 'status.suspended',
};

export function statusBadge(status) {
  return badge(t(STATUS_KEY[status] ?? status, {}), STATUS_TYPE[status] ?? 'gray');
}

// Confirm dialog (custom async — avoids browser blocking window.confirm)
export function confirm(message) {
  return new Promise((resolve) => {
    const id = 'confirmDialog';
    const existing = document.getElementById(id);
    if (existing) existing.remove();

    const overlay = document.createElement('div');
    overlay.id = id;
    overlay.className = 'modal-overlay';
    overlay.style.cssText = 'z-index:9999';
    overlay.innerHTML = `
      <div class="modal" style="max-width:380px">
        <div class="modal-body" style="padding:1.5rem;text-align:center;font-size:0.95rem">${message}</div>
        <div class="modal-footer" style="justify-content:center;gap:0.75rem">
          <button class="btn btn-secondary" id="confirmNo">${t('btn.cancel')}</button>
          <button class="btn btn-danger" id="confirmYes">${t('btn.confirm')}</button>
        </div>
      </div>
    `;
    document.body.appendChild(overlay);

    const cleanup = (result) => { overlay.remove(); resolve(result); };
    document.getElementById('confirmYes').addEventListener('click', () => cleanup(true));
    document.getElementById('confirmNo').addEventListener('click', () => cleanup(false));
  });
}

// Render empty state
export function emptyState(icon, text) {
  return `<div class="empty-state"><div class="empty-state-icon">${icon}</div><div class="empty-state-text">${text}</div></div>`;
}
