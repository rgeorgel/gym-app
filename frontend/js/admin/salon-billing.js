import { api } from '../api.js';
import { formatCurrency } from '../ui.js';

const MONTHS = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez'];

function shortCurrency(value) {
  const n = Number(value);
  if (n >= 1000) return `R$${(n / 1000).toFixed(1)}k`;
  return `R$${Math.round(n)}`;
}

export async function renderSalonBilling(container) {
  const today = new Date();
  let year  = today.getFullYear();
  let month = today.getMonth() + 1;

  container.innerHTML = `
    <div style="max-width:860px">
      <div style="display:flex;align-items:center;gap:0.75rem;margin-bottom:1.5rem;flex-wrap:wrap">
        <button class="btn btn-secondary btn-sm" id="btnPrevMonth">← Anterior</button>
        <span id="monthLabel" style="font-size:1.1rem;font-weight:600;min-width:120px;text-align:center"></span>
        <button class="btn btn-secondary btn-sm" id="btnNextMonth">Próximo →</button>
      </div>
      <div id="billingContent"><div class="loading-center"><span class="spinner"></span></div></div>
    </div>
  `;

  async function load() {
    document.getElementById('monthLabel').textContent = `${MONTHS[month - 1]} ${year}`;
    document.getElementById('btnNextMonth').disabled = (year === today.getFullYear() && month === today.getMonth() + 1);
    await loadBilling(year, month);
  }

  document.getElementById('btnPrevMonth').addEventListener('click', () => {
    month--;
    if (month < 1) { month = 12; year--; }
    load();
  });

  document.getElementById('btnNextMonth').addEventListener('click', () => {
    if (year === today.getFullYear() && month === today.getMonth() + 1) return;
    month++;
    if (month > 12) { month = 1; year++; }
    load();
  });

  await load();
}

async function loadBilling(year, month) {
  const content = document.getElementById('billingContent');
  content.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
  try {
    const d = await api.get(`/dashboard/salon-billing?year=${year}&month=${month}`);
    content.innerHTML = `
      ${renderKpis(d)}
      ${renderHistory(d.monthlyHistory, year, month)}
      ${renderByService(d.byService, d.totalRevenue)}
    `;
  } catch (e) {
    content.innerHTML = `<div class="empty-state"><div class="empty-state-icon">⚠️</div><div class="empty-state-text">${e.message}</div></div>`;
  }
}

function renderKpis(d) {
  const kpis = [
    { icon: '💰', label: 'Receita estimada',    value: formatCurrency(d.totalRevenue),      sub: 'no período' },
    { icon: '📅', label: 'Atendimentos',         value: d.totalAppointments,                 sub: 'realizados' },
    { icon: '🎫', label: 'Ticket médio',          value: formatCurrency(d.averageTicket),    sub: 'por atendimento' },
    { icon: '❌', label: 'Cancelamentos',         value: d.cancelledAppointments,             sub: 'no período', warn: d.cancelledAppointments > 0 },
  ];
  return `
    <div class="stats-grid" style="margin-bottom:1.5rem">
      ${kpis.map(k => `
        <div class="stat-card">
          <div class="stat-label">${k.icon} ${k.label}</div>
          <div class="stat-value" style="${k.warn ? 'color:var(--color-warning)' : ''}">${k.value}</div>
          <div class="stat-meta">${k.sub}</div>
        </div>
      `).join('')}
    </div>
  `;
}

function renderHistory(history, currentYear, currentMonth) {
  if (!history?.length) return '';
  const maxRevenue = Math.max(...history.map(h => Number(h.revenue)), 1);

  return `
    <div class="card" style="margin-bottom:1.25rem">
      <div class="card-header">
        <span class="card-title">📈 Histórico — últimos 12 meses</span>
      </div>
      <div class="card-body">
        <div style="display:flex;align-items:flex-end;gap:0.375rem;height:140px;margin-bottom:0.5rem">
          ${history.map(h => {
            const pct = Math.round((Number(h.revenue) / maxRevenue) * 100);
            const isCurrent = h.year === currentYear && h.month === currentMonth;
            return `
              <div style="flex:1;display:flex;flex-direction:column;align-items:center;gap:0.2rem;height:100%;justify-content:flex-end">
                ${h.appointments > 0 ? `<div style="font-size:0.6rem;color:var(--gray-500);line-height:1">${shortCurrency(h.revenue)}</div>` : ''}
                <div title="${MONTHS[h.month-1]} ${h.year}: ${formatCurrency(h.revenue)} — ${h.appointments} atend."
                  style="width:100%;border-radius:3px 3px 0 0;height:${Math.max(pct, h.appointments > 0 ? 3 : 0)}%;
                    background:${isCurrent ? 'var(--brand-secondary)' : 'var(--gray-300)'};opacity:${isCurrent ? 1 : 0.7}">
                </div>
              </div>
            `;
          }).join('')}
        </div>
        <div style="display:flex;gap:0.375rem;border-top:1px solid var(--gray-200);padding-top:0.375rem">
          ${history.map(h => `
            <div style="flex:1;text-align:center;font-size:0.6rem;color:var(--gray-400);font-weight:${h.year === currentYear && h.month === currentMonth ? '700' : '400'}">${MONTHS[h.month-1]}</div>
          `).join('')}
        </div>
      </div>
    </div>
  `;
}

function renderByService(services, totalRevenue) {
  if (!services?.length) return `
    <div class="card">
      <div class="card-body" style="padding:2rem;text-align:center;color:var(--gray-400);font-size:var(--font-size-sm)">
        Nenhum atendimento registrado neste período.
      </div>
    </div>
  `;

  const max = services[0]?.count ?? 1;
  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">✂️ Por serviço</span>
        <span class="text-sm text-muted">${services.reduce((s, x) => s + x.count, 0)} atendimentos no total</span>
      </div>
      <div class="card-body" style="padding:0">
        ${services.map(s => {
          const pct = totalRevenue > 0 ? Math.round(Number(s.revenue) / Number(totalRevenue) * 100) : 0;
          return `
            <div style="padding:0.875rem 1.25rem;border-bottom:1px solid var(--gray-100)">
              <div style="display:flex;align-items:center;gap:0.625rem;margin-bottom:0.375rem">
                <span style="width:10px;height:10px;border-radius:50%;background:${s.color};flex-shrink:0"></span>
                <span style="font-size:var(--font-size-sm);font-weight:500;flex:1">${s.name}</span>
                <span style="font-size:var(--font-size-sm);color:var(--gray-500);margin-right:0.75rem">${s.count}×</span>
                <span style="font-size:var(--font-size-sm);font-weight:700;color:var(--brand-secondary);min-width:80px;text-align:right">${formatCurrency(s.revenue)}</span>
                <span style="font-size:var(--font-size-xs);color:var(--gray-400);min-width:36px;text-align:right">${pct}%</span>
              </div>
              <div class="progress">
                <div class="progress-bar" style="width:${Math.round(s.count / max * 100)}%;background:${s.color};opacity:0.7"></div>
              </div>
            </div>
          `;
        }).join('')}
      </div>
    </div>
  `;
}
