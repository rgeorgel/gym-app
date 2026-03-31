import { api } from '../api.js';
import { formatCurrency, formatTime, getWeekdays } from '../ui.js';
import { t } from '../i18n.js';
import { renderStudentDetail } from './student-detail.js';
import { renderOnboardingWizard } from './onboarding.js';

export async function renderSalonDashboard(container, openStudentId = null) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  try {
    const [stats, today, topServices, topClients, weekly] = await Promise.all([
      api.get('/dashboard/salon-stats'),
      api.get('/dashboard/salon-today'),
      api.get('/dashboard/salon-top-services'),
      api.get('/dashboard/salon-top-clients'),
      api.get('/dashboard/salon-weekly'),
    ]);

    container.innerHTML = `
      ${renderTodayCard(today)}
      ${renderKpis(stats)}
      <div class="dashboard-2col">
        ${renderWeeklyChart(weekly)}
        ${renderTopServices(topServices)}
      </div>
      ${renderTopClients(topClients)}
    `;

    await renderOnboardingWizard(container);

    if (openStudentId) {
      renderStudentDetail(container, openStudentId, () => {
        history.replaceState(null, '', '#dashboard');
        renderSalonDashboard(container);
      });
      return;
    }

    container._dashClick && container.removeEventListener('click', container._dashClick);
    container._dashClick = e => {
      const el = e.target.closest('[data-student-id]');
      if (el) {
        history.replaceState(null, '', '#dashboard/' + el.dataset.studentId);
        renderStudentDetail(container, el.dataset.studentId, () => {
          history.replaceState(null, '', '#dashboard');
          renderSalonDashboard(container);
        });
      }
    };
    container.addEventListener('click', container._dashClick);
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-icon">⚠️</div><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
  }
}

function renderTodayCard(appointments) {
  const now = new Date();
  const currentMinutes = now.getHours() * 60 + now.getMinutes();

  return `
    <div class="card" style="margin-bottom:1.25rem">
      <div class="card-header">
        <span class="card-title">💅 Agenda de hoje</span>
        <span class="text-sm text-muted">${appointments.length} atendimento${appointments.length !== 1 ? 's' : ''}</span>
      </div>
      ${appointments.length === 0
        ? `<div class="card-body" style="color:var(--gray-400);font-size:var(--font-size-sm);padding:1rem 1.25rem">
             Nenhum atendimento agendado para hoje
           </div>`
        : `<div style="display:flex;gap:0.75rem;padding:0.75rem 1.25rem 1rem;flex-wrap:wrap">
             ${appointments.map(a => {
               const slotMinutes = parseInt(a.startTime?.slice(0,2)) * 60 + parseInt(a.startTime?.slice(3,5));
               const slotEnd = slotMinutes + (a.durationMinutes ?? 0);
               const isNow = currentMinutes >= slotMinutes && currentMinutes < slotEnd;
               const isDone = a.status === 'CheckedIn' || currentMinutes >= slotEnd;
               const accent = a.status === 'CheckedIn' ? 'var(--color-success)'
                            : isNow ? 'var(--brand-secondary)'
                            : isDone ? 'var(--gray-300)'
                            : 'var(--brand-primary)';
               return `
                 <div data-student-id="${a.clientId}" style="background:var(--gray-50);border:1px solid var(--gray-200);border-left:3px solid ${a.serviceColor};border-radius:var(--border-radius);padding:0.625rem 0.875rem;min-width:150px;opacity:${isDone && a.status !== 'CheckedIn' ? 0.55 : 1};cursor:pointer" onmouseenter="this.style.background='var(--gray-100)'" onmouseleave="this.style.background='var(--gray-50)'">
                   <div style="font-weight:700;font-size:var(--font-size-sm)">${a.startTime?.slice(0,5)}</div>
                   <div style="font-size:var(--font-size-xs);color:var(--gray-600);margin:0.1rem 0">${a.serviceName}</div>
                   <div style="font-size:var(--font-size-xs);font-weight:500;color:${accent}">
                     👤 ${a.clientName}
                   </div>
                   ${a.status === 'CheckedIn' ? `<div style="font-size:0.65rem;color:var(--color-success);margin-top:0.1rem">✓ Check-in</div>` : ''}
                 </div>
               `;
             }).join('')}
           </div>`}
    </div>
  `;
}

function renderKpis(stats) {
  const kpis = [
    { label: 'Atendimentos',         value: `${stats.appointmentsToday} / ${stats.appointmentsThisMonth}`, icon: '📅', meta: 'hoje / mês' },
    { label: 'Clientes',             value: `${stats.totalClients} / ${stats.newClientsThisMonth}`,        icon: '👥', meta: 'ativos / novos' },
    { label: 'Receita estimada',     value: formatCurrency(stats.revenueThisMonth), icon: '💰', meta: 'este mês' },
    { label: 'Taxa de cancelamento', value: `${stats.cancellationRatePct}%`, icon: '❌', meta: 'este mês', warn: stats.cancellationRatePct > 20 },
  ];

  return `
    <div class="stats-grid" style="margin-bottom:1.5rem">
      ${kpis.map(k => `
        <div class="stat-card">
          <div class="stat-label">${k.icon} ${k.label}</div>
          <div class="stat-value" style="${k.warn ? 'color:var(--color-warning)' : ''}">${k.value}</div>
          ${k.meta ? `<div class="stat-meta">${k.meta}</div>` : ''}
        </div>
      `).join('')}
    </div>
  `;
}

function renderWeeklyChart(weeks) {
  const max = Math.max(...weeks.map(w => w.count), 1);
  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">📈 Agendamentos por semana</span>
        <span class="text-sm text-muted">últimas 8 semanas</span>
      </div>
      <div class="card-body">
        <div style="display:flex;flex-direction:column;gap:0.5rem">
          <div style="display:flex;align-items:flex-end;gap:0.375rem;height:150px">
            ${weeks.map(w => {
              const pct = Math.round((w.count / max) * 100);
              return `
                <div style="flex:1;display:flex;flex-direction:column;align-items:center;gap:0.2rem;height:100%;justify-content:flex-end">
                  ${w.count > 0 ? `<div style="font-size:0.65rem;color:var(--gray-500);line-height:1">${w.count}</div>` : ''}
                  <div style="width:100%;background:var(--brand-secondary);border-radius:3px 3px 0 0;height:${Math.max(pct, w.count > 0 ? 3 : 0)}%;opacity:0.8"></div>
                </div>
              `;
            }).join('')}
          </div>
          <div style="display:flex;gap:0.375rem;border-top:1px solid var(--gray-200);padding-top:0.375rem">
            ${weeks.map(w => {
              const d = new Date(w.weekStart + 'T12:00:00');
              return `<div style="flex:1;text-align:center;font-size:0.6rem;color:var(--gray-400)">${d.getDate()}/${d.getMonth()+1}</div>`;
            }).join('')}
          </div>
        </div>
      </div>
    </div>
  `;
}

function renderTopServices(services) {
  const max = services[0]?.count ?? 1;
  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">💅 Serviços mais agendados</span>
        <span class="text-sm text-muted">últimos 30 dias</span>
      </div>
      <div class="card-body" style="padding:0">
        ${services.length === 0
          ? `<div class="empty-state"><div class="empty-state-text">Sem dados no período</div></div>`
          : services.map((s, i) => `
              <div style="padding:0.75rem 1.25rem;border-bottom:1px solid var(--gray-100)">
                <div style="display:flex;align-items:center;gap:0.625rem;margin-bottom:0.375rem">
                  <span style="width:10px;height:10px;border-radius:50%;background:${s.color};display:inline-block;flex-shrink:0"></span>
                  <span style="font-size:var(--font-size-sm);font-weight:500;flex:1">${s.name}</span>
                  <span style="font-size:var(--font-size-sm);font-weight:700;color:var(--brand-secondary)">${s.count}×</span>
                </div>
                <div class="progress">
                  <div class="progress-bar" style="width:${Math.round(s.count / max * 100)}%;background:${s.color};opacity:0.7"></div>
                </div>
              </div>
            `).join('')}
      </div>
    </div>
  `;
}

function renderTopClients(clients) {
  if (!clients.length) return '';
  const medals = ['#f59e0b', '#9ca3af', '#b45309'];
  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">⭐ Clientes mais assíduos</span>
        <span class="text-sm text-muted">últimos 30 dias</span>
      </div>
      <div class="card-body" style="padding:0">
        ${clients.map((c, i) => `
          <div data-student-id="${c.clientId}" style="padding:0.625rem 1.25rem;border-bottom:1px solid var(--gray-100);display:flex;align-items:center;gap:0.75rem;cursor:pointer" onmouseenter="this.style.background='var(--gray-50)'" onmouseleave="this.style.background=''">
            <div style="width:22px;height:22px;border-radius:50%;background:${medals[i] ?? 'var(--gray-200)'};display:flex;align-items:center;justify-content:center;font-size:0.65rem;font-weight:700;color:${i < 3 ? 'white' : 'var(--gray-600)'};flex-shrink:0">
              ${i + 1}
            </div>
            <div style="flex:1;overflow:hidden">
              <div style="font-size:var(--font-size-sm);font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${c.clientName}</div>
              ${c.clientPhone ? `<div style="font-size:var(--font-size-xs);color:var(--gray-400)">${c.clientPhone}</div>` : ''}
            </div>
            <div style="font-size:var(--font-size-sm);font-weight:700;color:var(--brand-secondary);flex-shrink:0">${c.count} atend.</div>
          </div>
        `).join('')}
      </div>
    </div>
  `;
}
