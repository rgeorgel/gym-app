import { api } from '../api.js?v=202603311200';
import { showToast, emptyState } from '../ui.js?v=202603311200';
import { t } from '../i18n.js?v=202603311200';

export async function renderServices(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  try {
    const services = await api.get('/class-types');
    const active = services.filter(s => s.isActive);

    if (!active.length) {
      container.innerHTML = emptyState('💅', t('services.none'));
      return;
    }

    container.innerHTML = `
      <div style="padding:1rem 0">
        <h2 style="margin:0 0 1.25rem;font-size:1.25rem">${t('services.title')}</h2>
        <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(min(100%,280px),1fr));gap:1rem">
          ${active.map(s => `
            <div class="card" style="cursor:pointer" onclick="window._bookService('${s.id}')">
              <div class="card-body" style="padding:1.25rem">
                <div style="display:flex;align-items:center;gap:0.75rem;margin-bottom:0.5rem">
                  <div style="width:12px;height:12px;border-radius:50%;background:${s.color};flex-shrink:0"></div>
                  <h3 style="margin:0;font-size:1rem;font-weight:600">${s.name}</h3>
                </div>
                ${s.description ? `<p class="text-sm text-muted" style="margin:0 0 0.75rem;line-height:1.4">${s.description}</p>` : ''}
                ${s.price
                  ? `<div style="font-size:1.4rem;font-weight:700;color:var(--brand-secondary);margin-bottom:0.75rem">
                       R$ ${Number(s.price).toFixed(2).replace('.', ',')}
                     </div>`
                  : ''}
                <button class="btn btn-primary" style="width:100%" onclick="event.stopPropagation();window._bookService('${s.id}')">
                  ${t('services.book')}
                </button>
              </div>
            </div>
          `).join('')}
        </div>
        <p class="text-sm text-muted" style="margin-top:1.5rem;text-align:center">
          💳 ${t('services.payAtLocation')}
        </p>
      </div>
    `;

    window._bookService = () => {
      window.dispatchEvent(new CustomEvent('switch-tab', { detail: { tab: 'schedule' } }));
    };
  } catch (e) {
    showToast(t('error.prefix') + e.message, 'error');
  }
}
