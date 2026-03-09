import { api } from '../api.js';
import { showToast, emptyState } from '../ui.js';

export async function renderReports(container) {
  container.innerHTML = `
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:1.25rem">
      <div class="card">
        <div class="card-header">
          <span class="card-title">Frequência por aluno</span>
          <select id="freqDays" class="form-control" style="width:auto;font-size:var(--font-size-sm)">
            <option value="7">Últimos 7 dias</option>
            <option value="30" selected>Últimos 30 dias</option>
            <option value="90">Últimos 90 dias</option>
          </select>
        </div>
        <div class="card-body" style="padding:0" id="freqContent">
          <div class="loading-center"><span class="spinner"></span></div>
        </div>
      </div>

      <div class="card">
        <div class="card-header">
          <span class="card-title">Pacotes vencendo</span>
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
    if (!data.length) { content.innerHTML = emptyState('📊', 'Sem dados no período'); return; }
    const max = data[0]?.checkIns || 1;
    content.innerHTML = data.map(d => `
      <div style="padding:0.75rem 1.25rem;border-bottom:1px solid var(--gray-100)">
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:0.375rem">
          <span class="font-medium text-sm">${d.studentName}</span>
          <span class="text-xs text-muted">${d.checkIns} aulas</span>
        </div>
        <div class="progress">
          <div class="progress-bar" style="width:${(d.checkIns/max)*100}%"></div>
        </div>
      </div>
    `).join('');
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

async function loadExpiring() {
  const content = document.getElementById('expiringContent');
  try {
    const data = await api.get('/dashboard/expiring-packages');
    if (!data.length) { content.innerHTML = emptyState('📦', 'Nenhum pacote vencendo'); return; }
    content.innerHTML = data.map(p => `
      <div style="padding:0.75rem 1.25rem;border-bottom:1px solid var(--gray-100)">
        <div class="font-medium text-sm">${p.studentName}</div>
        <div class="text-xs text-muted">${p.name}</div>
        <div style="display:flex;justify-content:space-between;margin-top:0.25rem">
          <span class="text-xs ${new Date(p.expiresAt) < new Date() ? 'badge badge-danger' : 'badge badge-warning'}">
            Vence: ${new Date(p.expiresAt).toLocaleDateString('pt-BR')}
          </span>
          <span class="text-xs text-muted">${p.remainingCredits} créditos</span>
        </div>
      </div>
    `).join('');
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}
