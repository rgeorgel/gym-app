import { api } from '../api.js';
import { t } from '../i18n.js';

export async function renderHeatmap(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  try {
    const response = await api.get('/dashboard/heatmap/weekday-timeslot');
    container.innerHTML = renderHeatmapUI(response.data, response.period);
    initHeatmapInteractions(response.data);
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-icon">⚠️</div><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
  }
}

function renderHeatmapUI(data, period) {
  const weekdays = [t('weekday.0'), t('weekday.1'), t('weekday.2'), t('weekday.3'), t('weekday.4'), t('weekday.5'), t('weekday.6')];
  const hours = Array.from({ length: 16 }, (_, i) => i + 6);

  const dataMap = new Map();
  data.forEach(d => dataMap.set(`${d.weekday}-${d.hour}`, d));

  const maxBookings = Math.max(...data.map(d => d.totalBookings), 1);
  const maxSessions = Math.max(...data.map(d => d.sessionCount), 1);

  return `
    <div class="card">
      <div class="card-header">
        <span class="card-title">${t('heatmap.title')}</span>
        <span class="text-sm text-muted">${t('heatmap.period', { days: period })}</span>
      </div>
      <div class="card-body" style="overflow-x:auto">
        <div style="display:flex;align-items:flex-start;gap:0.5rem">
          <div style="display:flex;flex-direction:column;padding-top:2rem">
            ${hours.map(h => `
              <div style="height:2rem;display:flex;align-items:center;font-size:0.65rem;color:var(--gray-500);width:3rem;justify-content:flex-end;padding-right:0.5rem">
                ${String(h).padStart(2, '0')}:00
              </div>
            `).join('')}
          </div>
          <div>
            <div style="display:grid;grid-template-columns:repeat(7,2rem);gap:2px;margin-bottom:0.5rem">
              ${weekdays.map((day, i) => `
                <div style="text-align:center;font-size:0.65rem;font-weight:600;color:var(--gray-600);padding:0.25rem 0">${day}</div>
              `).join('')}
            </div>
            <div style="display:grid;grid-template-columns:repeat(7,2rem);grid-template-rows:repeat(${hours.length},2rem);gap:2px" id="heatmapGrid">
              ${hours.map(h => weekdays.map((_, weekday) => {
                const cell = dataMap.get(`${weekday}-${h}`);
                const intensity = cell ? cell.totalBookings / maxBookings : 0;
                const color = getHeatColor(intensity);
                return `
                  <div class="heatmap-cell ${cell ? 'has-data' : ''}"
                       data-weekday="${weekday}"
                       data-hour="${h}"
                       data-sessions="${cell?.sessionCount ?? 0}"
                       data-bookings="${cell?.totalBookings ?? 0}"
                       data-capacity="${cell?.totalCapacity ?? 0}"
                       data-occupancy="${cell?.avgOccupancyPct?.toFixed(0) ?? 0}"
                       style="background:${color};cursor:${cell ? 'pointer' : 'default'}">
                  </div>
                `;
              }).join('')).join('')}
            </div>
          </div>
        </div>
        ${renderLegend()}
      </div>
    </div>
    <div class="card" style="margin-top:1.25rem">
      <div class="card-header">
        <span class="card-title">${t('heatmap.insights')}</span>
      </div>
      <div class="card-body">
        ${renderInsights(data, maxBookings)}
      </div>
    </div>
  `;
}

function getHeatColor(intensity) {
  if (intensity === 0) return 'var(--gray-100)';
  if (intensity < 0.25) return '#d4edda';
  if (intensity < 0.5) return '#91d4a8';
  if (intensity < 0.75) return '#f5c242';
  if (intensity < 0.9) return '#f08c28';
  return '#e74c3c';
}

function renderLegend() {
  const levels = [
    { label: t('heatmap.legend.none'), color: 'var(--gray-100)' },
    { label: '25%', color: '#d4edda' },
    { label: '50%', color: '#91d4a8' },
    { label: '75%', color: '#f5c242' },
    { label: '100%', color: '#e74c3c' },
  ];

  return `
    <div style="display:flex;align-items:center;gap:1rem;margin-top:1rem;font-size:0.7rem;color:var(--gray-500)">
      <span>${t('heatmap.legend.label')}</span>
      ${levels.map(l => `
        <div style="display:flex;align-items:center;gap:0.25rem">
          <div style="width:0.75rem;height:0.75rem;background:${l.color};border-radius:2px"></div>
          <span>${l.label}</span>
        </div>
      `).join('')}
    </div>
  `;
}

function renderInsights(data, maxBookings) {
  if (!data.length) {
    return `<div class="empty-state"><div class="empty-state-text">${t('heatmap.noData')}</div></div>`;
  }

  const topSlots = [...data]
    .sort((a, b) => b.totalBookings - a.totalBookings)
    .slice(0, 3);

  const weekdays = [t('weekday.0'), t('weekday.1'), t('weekday.2'), t('weekday.3'), t('weekday.4'), t('weekday.5'), t('weekday.6')];

  const busiestDay = weekdays[data.reduce((acc, _, i) => {
    const dayBookings = data.filter(d => d.weekday === i).reduce((sum, d) => sum + d.totalBookings, 0);
    return dayBookings > acc.count ? { day: i, count: dayBookings } : acc;
  }, { day: 0, count: 0 }).day];

  const peakHour = data.reduce((acc, d) =>
    d.sessionCount > acc.sessionCount || (d.sessionCount === acc.sessionCount && d.totalBookings > acc.totalBookings) ? d : acc, data[0]);
  const peakTime = `${String(peakHour.hour).padStart(2, '0')}:00`;

  return `
    <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:1rem">
      <div style="text-align:center;padding:1rem;background:var(--gray-50);border-radius:var(--border-radius)">
        <div style="font-size:1.5rem;margin-bottom:0.25rem">🔥</div>
        <div style="font-weight:700;font-size:1.25rem;color:var(--color-primary)">${busiestDay}</div>
        <div style="font-size:var(--font-size-xs);color:var(--gray-500)">${t('heatmap.insight.busiestDay')}</div>
      </div>
      <div style="text-align:center;padding:1rem;background:var(--gray-50);border-radius:var(--border-radius)">
        <div style="font-size:1.5rem;margin-bottom:0.25rem">⏰</div>
        <div style="font-weight:700;font-size:1.25rem;color:var(--color-primary)">${peakTime}</div>
        <div style="font-size:var(--font-size-xs);color:var(--gray-500)">${t('heatmap.insight.peakHour')}</div>
      </div>
      <div style="text-align:center;padding:1rem;background:var(--gray-50);border-radius:var(--border-radius)">
        <div style="font-size:1.5rem;margin-bottom:0.25rem">📊</div>
        <div style="font-weight:700;font-size:1.25rem;color:var(--color-primary)">${data.reduce((s, d) => s + d.sessionCount, 0)}</div>
        <div style="font-size:var(--font-size-xs);color:var(--gray-500)">${t('heatmap.insight.totalSessions')}</div>
      </div>
    </div>
    ${topSlots.length > 0 ? `
      <div style="margin-top:1.25rem">
        <div style="font-size:var(--font-size-sm);font-weight:600;margin-bottom:0.75rem">${t('heatmap.insight.topSlots')}</div>
        <div style="display:flex;flex-direction:column;gap:0.5rem">
          ${topSlots.map((slot, i) => {
            const pct = Math.round((slot.totalBookings / maxBookings) * 100);
            return `
              <div style="display:flex;align-items:center;gap:0.75rem">
                <div style="width:1.5rem;height:1.5rem;border-radius:50%;background:var(--brand-secondary);display:flex;align-items:center;justify-content:center;font-size:0.65rem;font-weight:700;color:white;flex-shrink:0">${i + 1}</div>
                <div style="flex:1">
                  <div style="font-size:var(--font-size-sm);font-weight:500">${weekdays[slot.weekday]} · ${String(slot.hour).padStart(2, '0')}:00</div>
                  <div class="progress" style="height:0.375rem;margin-top:0.25rem">
                    <div class="progress-bar" style="width:${pct}%;background:var(--color-warning)"></div>
                  </div>
                </div>
                <div style="font-size:var(--font-size-sm);font-weight:600;color:var(--gray-600)">${slot.totalBookings} ${t('heatmap.insight.bookings')}</div>
              </div>
            `;
          }).join('')}
        </div>
      </div>
    ` : ''}
  `;
}

function initHeatmapInteractions(data) {
  const cells = document.querySelectorAll('.heatmap-cell.has-data');
  cells.forEach(cell => {
    cell.addEventListener('mouseenter', (e) => {
      const weekday = e.target.dataset.weekday;
      const hour = e.target.dataset.hour;
      const bookings = e.target.dataset.bookings;
      const capacity = e.target.dataset.capacity;
      const occupancy = e.target.dataset.occupancy;
      const weekdays = [t('weekday.0'), t('weekday.1'), t('weekday.2'), t('weekday.3'), t('weekday.4'), t('weekday.5'), t('weekday.6')];

      e.target.title = `${weekdays[weekday]} ${String(hour).padStart(2, '0')}:00\n${bookings} ${t('heatmap.insight.bookings')} / ${capacity} cap\n${occupancy}% ${t('heatmap.insight.occupancy')}`;
    });
  });
}
