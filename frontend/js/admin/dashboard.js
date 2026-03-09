import { api } from '../api.js';
import { formatDate, WEEKDAYS } from '../ui.js';

export async function renderDashboard(container) {
  container.innerHTML = `<div class="loading-center"><span class="spinner"></span></div>`;

  try {
    const [stats, occupancy, expiring] = await Promise.all([
      api.get('/dashboard/stats'),
      api.get('/dashboard/occupancy'),
      api.get('/dashboard/expiring-packages'),
    ]);

    container.innerHTML = `
      <div class="stats-grid" style="margin-bottom:1.5rem">
        <div class="stat-card">
          <div class="stat-label">Alunos ativos</div>
          <div class="stat-value">${stats.totalStudents}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Aulas hoje</div>
          <div class="stat-value">${stats.sessionsToday}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Agendamentos no mês</div>
          <div class="stat-value">${stats.bookingsThisMonth}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Pacotes vencendo</div>
          <div class="stat-value" style="color:var(--color-warning)">${stats.expiringPackages}</div>
          <div class="stat-meta">nos próximos 7 dias</div>
        </div>
      </div>

      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1.25rem">
        <div class="card">
          <div class="card-header">
            <span class="card-title">Ocupação — próximos 7 dias</span>
          </div>
          <div class="card-body" style="padding:0">
            ${occupancy.length === 0
              ? '<div class="empty-state"><div class="empty-state-text">Nenhuma aula programada</div></div>'
              : occupancy.map(s => `
              <div style="padding:0.875rem 1.25rem;border-bottom:1px solid var(--gray-100)">
                <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:0.375rem">
                  <div>
                    <span class="font-medium text-sm">${s.classType}</span>
                    <span class="text-xs text-muted" style="margin-left:0.5rem">${WEEKDAYS[new Date(s.date).getDay()]} ${s.date?.split('T')[0]?.split('-').slice(1).reverse().join('/')} ${s.startTime?.slice(0,5)}</span>
                  </div>
                  <span class="text-xs text-muted">${s.bookings}/${s.capacity}</span>
                </div>
                <div class="progress">
                  <div class="progress-bar ${s.occupancyPct >= 90 ? 'warning' : s.occupancyPct >= 100 ? 'danger' : ''}" style="width:${Math.min(s.occupancyPct,100)}%"></div>
                </div>
              </div>`).join('')}
          </div>
        </div>

        <div class="card">
          <div class="card-header">
            <span class="card-title">Pacotes vencendo em breve</span>
          </div>
          <div class="card-body" style="padding:0">
            ${expiring.length === 0
              ? '<div class="empty-state"><div class="empty-state-text">Nenhum pacote vencendo</div></div>'
              : expiring.map(p => `
              <div style="padding:0.75rem 1.25rem;border-bottom:1px solid var(--gray-100)">
                <div class="font-medium text-sm">${p.studentName}</div>
                <div class="text-xs text-muted">${p.name} · vence ${formatDate(p.expiresAt)} · ${p.remainingCredits} créditos restantes</div>
              </div>`).join('')}
          </div>
        </div>
      </div>
    `;
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-icon">⚠️</div><div class="empty-state-text">Erro ao carregar dashboard: ${e.message}</div></div>`;
  }
}
