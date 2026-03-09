import { api } from '../api.js';
import { emptyState, formatDate } from '../ui.js';
import { getUser } from '../auth.js';

export async function renderMyPackages(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
  const user = getUser();

  try {
    const packages = await api.get(`/students/${user.id}/packages`);

    if (!packages.length) {
      container.innerHTML = emptyState('📦', 'Você não tem pacotes ativos');
      return;
    }

    container.innerHTML = packages.map(p => {
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
                ${isExpired ? '❌ Vencido' : expiring ? '⚠️ Vence em breve'  : '📅 Vence'}: ${formatDate(p.expiresAt)}
              </div>` : ''}
            </div>
            <div style="text-align:right">
              <div style="font-size:var(--font-size-xs);opacity:0.7">Uso geral</div>
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
                  <div class="credit-used">${i.usedCredits} de ${i.totalCredits} usados</div>
                </div>
                <div class="credit-remaining" style="color:${i.remainingCredits === 0 ? 'var(--color-danger)' : i.remainingCredits <= 2 ? 'var(--color-warning)' : 'var(--gray-900)'}">
                  ${i.remainingCredits}
                </div>
              </div>
            `).join('')}
          </div>
        </div>
      `;
    }).join('');
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro: ${e.message}</div></div>`;
  }
}
