import { api } from '../api.js';
import { showToast, emptyState } from '../ui.js';
import { t, getLocale } from '../i18n.js';

export async function renderReports(container) {
  container.innerHTML = `
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:1.25rem">
      <div class="card">
        <div class="card-header">
          <span class="card-title">${t('reports.frequency')}</span>
          <select id="freqDays" class="form-control" style="width:auto;font-size:var(--font-size-sm)">
            <option value="7">${t('reports.frequency.days7')}</option>
            <option value="30" selected>${t('reports.frequency.days30')}</option>
            <option value="90">${t('reports.frequency.days90')}</option>
          </select>
        </div>
        <div class="card-body" style="padding:0" id="freqContent">
          <div class="loading-center"><span class="spinner"></span></div>
        </div>
      </div>

      <div class="card">
        <div class="card-header">
          <span class="card-title">${t('reports.expiring.title')}</span>
        </div>
        <div class="card-body" style="padding:0" id="expiringContent">
          <div class="loading-center"><span class="spinner"></span></div>
        </div>
      </div>
    </div>
  `;

  await Promise.all([loadFrequency(30), loadExpiring()]);

  document.getElementById('freqDays').addEventListener('change', (e) => loadFrequency(parseInt(e.target.value)));
}

async function loadFrequency(days) {
  const content = document.getElementById('freqContent');
  content.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
  try {
    const data = await api.get(`/dashboard/frequency?days=${days}`);
    if (!data.length) { content.innerHTML = emptyState('📊', t('reports.frequency.noData')); return; }
    const max = data[0]?.checkIns || 1;
    content.innerHTML = data.map(d => `
      <div style="padding:0.75rem 1.25rem;border-bottom:1px solid var(--gray-100)">
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:0.375rem">
          <span class="font-medium text-sm">${d.studentName}</span>
          <span class="text-xs text-muted">${d.checkIns} ${t('reports.frequency.classes')}</span>
        </div>
        <div class="progress">
          <div class="progress-bar" style="width:${(d.checkIns/max)*100}%"></div>
        </div>
      </div>
    `).join('');
  } catch (e) {
    showToast(t('error.prefix') + e.message, 'error');
  }
}

async function loadExpiring() {
  const content = document.getElementById('expiringContent');
  try {
    const data = await api.get('/dashboard/expiring-packages');
    if (!data.length) { content.innerHTML = emptyState('📦', t('reports.expiring.none')); return; }
    content.innerHTML = data.map(p => `
      <div style="padding:0.75rem 1.25rem;border-bottom:1px solid var(--gray-100)">
        <div class="font-medium text-sm">${p.studentName}</div>
        <div class="text-xs text-muted">${p.name}</div>
        <div style="display:flex;justify-content:space-between;margin-top:0.25rem">
          <span class="text-xs ${new Date(p.expiresAt) < new Date() ? 'badge badge-danger' : 'badge badge-warning'}">
            ${new Date(p.expiresAt).toLocaleDateString(getLocale())}
          </span>
          <span class="text-xs text-muted">${p.remainingCredits} ${t('reports.credits')}</span>
        </div>
      </div>
    `).join('');
  } catch (e) {
    showToast(t('error.prefix') + e.message, 'error');
  }
}
