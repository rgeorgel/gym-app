import { api } from '../api.js?v=202603311200';
import { emptyState, formatDate } from '../ui.js?v=202603311200';
import { getUser } from '../auth.js?v=202603311200';
import { t } from '../i18n.js?v=202603311200';

export async function renderMyPackages(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
  const user = getUser();

  try {
    const packages = await api.get(`/students/${user.id}/packages`);

    if (!packages.length) {
      container.innerHTML = emptyState('📦', t('myPackages.none'));
      return;
    }

    const renderCard = (p) => {
      const totalCredits = p.items.reduce((s, i) => s + i.totalCredits, 0);
      const usedCredits = p.items.reduce((s, i) => s + i.usedCredits, 0);
      const pct = totalCredits > 0 ? (usedCredits / totalCredits) * 100 : 0;
      const isExpired = p.expiresAt && new Date(p.expiresAt) < new Date();
      const expiring = p.expiresAt && !isExpired && new Date(p.expiresAt) < new Date(Date.now() + 7 * 86400000);

      return `
        <div class="package-card">
          <div class="package-header">
            <div>
              <div class="package-name">${p.name}</div>
              ${p.expiresAt ? `<div class="package-expiry ${isExpired ? 'text-danger' : expiring ? 'text-warning' : ''}">
                ${isExpired ? t('myPackages.expired') : expiring ? t('myPackages.expiringSoon') : t('myPackages.expires')}: ${formatDate(p.expiresAt)}
              </div>` : ''}
            </div>
            <div style="text-align:right">
              <div style="font-size:var(--font-size-xs);opacity:0.7">${t('myPackages.overallUsage')}</div>
              <div style="font-weight:700">${usedCredits}/${totalCredits}</div>
            </div>
          </div>
          <div style="padding:0.75rem 1.25rem">
            <div class="progress" style="margin-bottom:0.75rem">
              <div class="progress-bar ${pct >= 90 ? 'warning' : ''}" style="width:${pct}%"></div>
            </div>
          </div>
          <div class="package-items">
            ${p.items.map(i => `
              <div class="credit-item">
                <div class="credit-color" style="background:${i.classTypeColor}"></div>
                <div class="credit-info">
                  <div class="credit-type">${i.classTypeName}</div>
                  <div class="credit-used">${t('myPackages.usedOf', { used: i.usedCredits, total: i.totalCredits })}</div>
                </div>
                <div class="credit-remaining" style="color:${i.remainingCredits === 0 ? 'var(--color-danger)' : i.remainingCredits <= 2 ? 'var(--color-warning)' : 'var(--gray-900)'}">
                  ${i.remainingCredits}
                </div>
              </div>
            `).join('')}
          </div>
        </div>
      `;
    };

    const isInactive = (p) => {
      const isExpired = p.expiresAt && new Date(p.expiresAt) < new Date();
      const noCredits = p.items.every(i => i.remainingCredits === 0);
      return isExpired || noCredits;
    };

    const active = packages.filter(p => !isInactive(p));
    const history = packages.filter(p => isInactive(p));

    let html = active.map(renderCard).join('');
    if (history.length) {
      html += `<div style="margin:1.5rem 0 0.75rem;font-weight:600;font-size:var(--font-size-sm);color:var(--gray-500);text-transform:uppercase;letter-spacing:0.05em">${t('myPackages.history')}</div>`;
      html += `<div style="opacity:0.6">${history.map(renderCard).join('')}</div>`;
    }

    container.innerHTML = html;
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
  }
}
