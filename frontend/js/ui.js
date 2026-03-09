// Shared UI utilities

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

  // Close handlers
  overlay.querySelector('.modal-close').addEventListener('click', () => closeModal(id));
  overlay.addEventListener('click', (e) => { if (e.target === overlay) closeModal(id); });
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
  return d.toLocaleDateString('pt-BR');
}

export function formatDateTime(dateStr) {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleString('pt-BR');
}

export function formatTime(timeStr) {
  // "07:00:00" → "07:00"
  return timeStr?.slice(0, 5) ?? '—';
}

export function formatCurrency(value) {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
}

export const WEEKDAYS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
export const WEEKDAYS_FULL = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'];

export function badge(text, type = 'gray') {
  return `<span class="badge badge-${type}">${text}</span>`;
}

export const STATUS_LABELS = {
  Confirmed: { label: 'Confirmado', type: 'success' },
  CheckedIn: { label: 'Check-in', type: 'info' },
  Cancelled: { label: 'Cancelado', type: 'danger' },
  Active: { label: 'Ativo', type: 'success' },
  Inactive: { label: 'Inativo', type: 'gray' },
  Suspended: { label: 'Suspenso', type: 'danger' },
};

export function statusBadge(status) {
  const s = STATUS_LABELS[status] ?? { label: status, type: 'gray' };
  return badge(s.label, s.type);
}

// Confirm dialog
export function confirm(message) {
  return window.confirm(message);
}

// Render empty state
export function emptyState(icon, text) {
  return `<div class="empty-state"><div class="empty-state-icon">${icon}</div><div class="empty-state-text">${text}</div></div>`;
}
