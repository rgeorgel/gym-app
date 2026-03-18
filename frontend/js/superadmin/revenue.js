import { api } from '../api.js';
import { formatDate } from '../ui.js';

export async function renderRevenue(container) {
  container.innerHTML = `<div class="loading-center"><span class="spinner"></span></div>`;

  let data;
  try {
    data = await api.get('/admin/billing/overview');
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro ao carregar dados: ${e.message}</div></div>`;
    return;
  }

  const price = (data.subscriptionPriceCents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  const mrr   = (data.estimatedMrrCents   / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

  container.innerHTML = `
    <div style="display:flex;flex-direction:column;gap:1.5rem">

      <!-- KPI cards -->
      <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:1rem">
        ${kpiCard('MRR Estimado', mrr, '#10b981', '💰')}
        ${kpiCard('Ativas', data.activeTenants, '#3b82f6', '✅')}
        ${kpiCard('Trial', data.trialTenants, '#f59e0b', '⏳')}
        ${kpiCard('Inadimplentes', data.pastDueTenants, '#ef4444', '⚠️')}
        ${kpiCard('Canceladas', data.canceledTenants, '#6b7280', '❌')}
        ${kpiCard('Total', data.totalTenants, '#8b5cf6', '🏢')}
      </div>

      <p class="text-muted text-sm" style="margin:0">
        MRR calculado com base em ${data.activeTenants} academia(s) ativa(s) × ${price}/mês.
      </p>

      <!-- Tenants table -->
      <div class="card">
        <div class="card-body" style="padding:0">
          <div class="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Academia</th>
                  <th>Slug</th>
                  <th>Status</th>
                  <th>Vencimento / Trial</th>
                  <th>Desde</th>
                </tr>
              </thead>
              <tbody>
                ${data.tenants.map(row => `
                  <tr>
                    <td><strong>${row.name}</strong></td>
                    <td><span style="font-family:monospace;font-size:0.8rem;color:var(--gray-500)">${row.slug}</span></td>
                    <td>${statusBadge(row.status, row.isInTrial, row.trialDaysRemaining)}</td>
                    <td>${periodCell(row)}</td>
                    <td>${formatDate(row.createdAt)}</td>
                  </tr>
                `).join('')}
              </tbody>
            </table>
          </div>
        </div>
      </div>

    </div>
  `;
}

function kpiCard(label, value, color, icon) {
  return `
    <div class="card">
      <div class="card-body" style="padding:1.25rem">
        <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:0.5rem">
          <span style="font-size:0.75rem;font-weight:600;color:var(--gray-500);text-transform:uppercase;letter-spacing:0.05em">${label}</span>
          <span style="font-size:1.25rem">${icon}</span>
        </div>
        <div style="font-size:1.75rem;font-weight:800;color:${color};letter-spacing:-0.05em">${value}</div>
      </div>
    </div>
  `;
}

function statusBadge(status, isInTrial, trialDaysRemaining) {
  if (isInTrial) return `<span class="badge badge-warning">Trial (${trialDaysRemaining}d)</span>`;
  const map = {
    Trial:     ['badge-warning', 'Trial expirado'],
    Active:    ['badge-success', 'Ativa'],
    PastDue:   ['badge-danger',  'Inadimplente'],
    Canceled:  ['badge-gray',    'Cancelada'],
    Suspended: ['badge-gray',    'Suspensa'],
  };
  const [cls, label] = map[status] ?? ['badge-gray', status];
  return `<span class="badge ${cls}">${label}</span>`;
}

function periodCell(row) {
  if (row.isInTrial) {
    const end = row.currentPeriodEnd
      ? formatDate(new Date(row.createdAt).setDate(new Date(row.createdAt).getDate() + 14))
      : '—';
    return `<span class="text-muted text-sm">Trial até ${end}</span>`;
  }
  if (!row.currentPeriodEnd) return '<span class="text-muted">—</span>';
  const d = new Date(row.currentPeriodEnd);
  const isExpired = d < new Date();
  return `<span style="color:${isExpired ? 'var(--color-danger)' : 'inherit'}">${formatDate(row.currentPeriodEnd)}</span>`;
}
