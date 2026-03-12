import { api } from '../api.js';
import { formatDate, formatCurrency, getWeekdays } from '../ui.js';
import { t, getLocale } from '../i18n.js';

export async function renderDashboard(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  try {
    const [stats, todaySessions, occupancy, expiring, topStudents, inactiveStudents, weeklyCheckins] = await Promise.all([
      api.get('/dashboard/stats'),
      api.get('/dashboard/today'),
      api.get('/dashboard/occupancy'),
      api.get('/dashboard/expiring-packages'),
      api.get('/dashboard/frequency?days=30'),
      api.get('/dashboard/inactive-students?days=14'),
      api.get('/dashboard/weekly-checkins'),
    ]);

    container.innerHTML = `
      ${renderTodaySessions(todaySessions)}
      ${renderKpiGrid(stats)}
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1.25rem;margin-bottom:1.25rem">
        ${renderOccupancy(occupancy)}
        ${renderExpiringPackages(expiring)}
      </div>
      <div style="display:grid;grid-template-columns:2fr 1fr;gap:1.25rem;margin-bottom:1.25rem">
        ${renderWeeklyChart(weeklyCheckins)}
        ${renderTopStudents(topStudents.slice(0, 5))}
      </div>
      ${renderInactiveStudents(inactiveStudents)}
    `;
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-icon">⚠️</div><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
  }
}

function renderTodaySessions(sessions) {
  if (!sessions.length) {
    return `
      <div class="card" style="margin-bottom:1.25rem">
        <div class="card-body" style="padding:1rem 1.25rem;color:var(--gray-400);font-size:var(--font-size-sm)">
          ${t('dash.today.none')}
        </div>
      </div>
    `;
  }

  return `
    <div class="card" style="margin-bottom:1.25rem">
      <div class="card-header">
        <span class="card-title">${t('dash.today')}</span>
        <span class="text-sm text-muted">${sessions.length}</span>
      </div>
      <div style="display:flex;gap:0.75rem;padding:0.75rem 1.25rem 1rem;flex-wrap:wrap">
        ${sessions.map(s => {
          const pct = s.occupancyPct;
          const color = s.status === 'Cancelled' ? 'var(--color-danger)' : pct >= 80 ? 'var(--color-warning)' : 'var(--color-success)';
          return `
            <div style="background:var(--gray-50);border:1px solid var(--gray-200);border-left:3px solid ${s.classTypeColor};border-radius:var(--border-radius);padding:0.625rem 0.875rem;min-width:130px">
              <div style="font-weight:700;font-size:var(--font-size-sm)">${s.startTime?.slice(0,5)}</div>
              <div style="font-size:var(--font-size-xs);color:var(--gray-600);margin:0.1rem 0">${s.classType}</div>
              <div style="font-size:var(--font-size-xs);color:${color};font-weight:500">
                ${s.status === 'Cancelled' ? t('dash.cancelled') : `${s.bookings}/${s.capacity} · ${s.checkedIn} ✓`}
              </div>
            </div>
          `;
        }).join('')}
      </div>
    </div>
  `;
}

function renderKpiGrid(stats) {
  const kpis = [
    { label: t('dash.kpi.activeStudents'), value: stats.totalStudents, icon: '👥' },
    { label: t('dash.kpi.newThisMonth'), value: stats.newStudentsThisMonth, icon: '🆕' },
    { label: t('dash.kpi.classesToday'), value: stats.sessionsToday, icon: '📅' },
    { label: t('dash.kpi.bookingsThisMonth'), value: stats.bookingsThisMonth, icon: '🎫' },
    { label: t('dash.kpi.avgOccupancy'), value: `${stats.avgOccupancyPct}%`, icon: '📊', meta: t('dash.kpi.last30') },
    { label: t('dash.kpi.cancellationRate'), value: `${stats.cancellationRatePct}%`, icon: '❌', meta: t('dash.kpi.thisMonth'), warn: stats.cancellationRatePct > 20 },
    { label: t('dash.kpi.revenue'), value: formatCurrency(stats.revenueThisMonth), icon: '💰' },
    { label: t('dash.kpi.noCredits'), value: stats.studentsWithNoCredits, icon: '⚠️', meta: t('dash.kpi.toRenew'), warn: stats.studentsWithNoCredits > 0 },
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

function renderOccupancy(sessions) {
  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">${t('dash.occupancy')}</span>
      </div>
      <div class="card-body" style="padding:0;max-height:340px;overflow-y:auto">
        ${sessions.length === 0
          ? `<div class="empty-state"><div class="empty-state-text">${t('dash.occupancy.none')}</div></div>`
          : sessions.map(s => {
              const pct = s.occupancyPct;
              const color = pct >= 80 ? 'var(--color-danger)' : pct >= 50 ? 'var(--color-warning)' : 'var(--color-success)';
              const dot = pct >= 80 ? '🔴' : pct >= 50 ? '🟡' : '🟢';
              const dateObj = new Date(s.date + 'T12:00:00');
              const dateLabel = `${getWeekdays()[dateObj.getDay()]} ${String(dateObj.getDate()).padStart(2,'0')}/${String(dateObj.getMonth()+1).padStart(2,'0')}`;
              return `
                <div style="padding:0.75rem 1.25rem;border-bottom:1px solid var(--gray-100)">
                  <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:0.375rem">
                    <div>
                      <span class="font-medium text-sm">${s.classType}</span>
                      <span class="text-xs text-muted" style="margin-left:0.5rem">${dateLabel} ${s.startTime?.slice(0,5)}</span>
                    </div>
                    <span class="text-xs" style="color:${color};font-weight:600">${dot} ${s.bookings}/${s.capacity}</span>
                  </div>
                  <div class="progress">
                    <div class="progress-bar" style="width:${Math.min(pct,100)}%;background:${color}"></div>
                  </div>
                </div>
              `;
            }).join('')}
      </div>
    </div>
  `;
}

function renderExpiringPackages(packages) {
  const expired = packages.filter(p => p.isExpired);
  const expiring = packages.filter(p => !p.isExpired);

  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">${t('dash.expiring')}</span>
        ${packages.length ? `<span class="badge badge-warning">${packages.length}</span>` : ''}
      </div>
      <div class="card-body" style="padding:0;max-height:340px;overflow-y:auto">
        ${packages.length === 0
          ? `<div class="empty-state"><div class="empty-state-text">${t('dash.expiring.none')}</div></div>`
          : `
            ${expired.length ? `
              <div style="padding:0.375rem 1.25rem;background:rgba(239,68,68,0.06);font-size:0.68rem;font-weight:600;color:var(--color-danger);text-transform:uppercase;letter-spacing:0.05em">
                ${t('dash.expired.label')} — ${expired.length}
              </div>
              ${expired.map(p => renderPkgRow(p, true)).join('')}
            ` : ''}
            ${expiring.length ? `
              <div style="padding:0.375rem 1.25rem;background:rgba(245,158,11,0.06);font-size:0.68rem;font-weight:600;color:var(--color-warning);text-transform:uppercase;letter-spacing:0.05em">
                ${t('dash.expiringSoon.label')} — ${expiring.length}
              </div>
              ${expiring.map(p => renderPkgRow(p, false)).join('')}
            ` : ''}
          `}
      </div>
    </div>
  `;
}

function renderPkgRow(p, isExpired) {
  return `
    <div style="padding:0.625rem 1.25rem;border-bottom:1px solid var(--gray-100)">
      <div class="font-medium text-sm">${p.studentName}</div>
      <div class="text-xs text-muted">
        ${p.name} ·
        ${isExpired
          ? `<span style="color:var(--color-danger)">${t('dash.session.expired')} ${formatDate(p.expiresAt)}</span>`
          : `${t('dash.session.expires')} ${formatDate(p.expiresAt)}`}
        · ${p.remainingCredits} ${p.remainingCredits !== 1 ? t('dash.credits') : t('dash.credit')}
      </div>
    </div>
  `;
}

function renderWeeklyChart(weeks) {
  const max = Math.max(...weeks.map(w => w.count), 1);

  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">${t('dash.weekly')}</span>
        <span class="text-sm text-muted">${t('dash.weekly.subtitle')}</span>
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

function renderTopStudents(students) {
  const medals = ['#f59e0b', '#9ca3af', '#b45309'];

  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">${t('dash.top5')}</span>
        <span class="text-sm text-muted">${t('dash.top5.subtitle')}</span>
      </div>
      <div class="card-body" style="padding:0">
        ${students.length === 0
          ? `<div class="empty-state"><div class="empty-state-text">${t('dash.top5.none')}</div></div>`
          : students.map((s, i) => `
            <div style="padding:0.625rem 1.25rem;border-bottom:1px solid var(--gray-100);display:flex;align-items:center;gap:0.75rem">
              <div style="width:22px;height:22px;border-radius:50%;background:${medals[i] ?? 'var(--gray-200)'};display:flex;align-items:center;justify-content:center;font-size:0.65rem;font-weight:700;color:${i < 3 ? 'white' : 'var(--gray-600)'};flex-shrink:0">
                ${i + 1}
              </div>
              <div style="flex:1;font-size:var(--font-size-sm);font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${s.studentName}</div>
              <div style="font-size:var(--font-size-sm);font-weight:700;color:var(--brand-secondary);flex-shrink:0">${s.checkIns}×</div>
            </div>
          `).join('')}
      </div>
    </div>
  `;
}

function renderInactiveStudents(students) {
  if (!students.length) return '';

  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">${t('dash.inactive')}</span>
        <span class="badge badge-warning">${students.length}</span>
      </div>
      <div class="card-body" style="padding:0;max-height:220px;overflow-y:auto">
        <div class="table-wrapper">
          <table>
            <thead><tr><th>${t('field.name')}</th><th>${t('field.email')}</th><th>${t('field.phone')}</th></tr></thead>
            <tbody>
              ${students.map(s => `
                <tr>
                  <td class="font-medium text-sm">${s.name}</td>
                  <td class="text-sm text-muted">${s.email}</td>
                  <td class="text-sm">${s.phone ?? '—'}</td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `;
}
