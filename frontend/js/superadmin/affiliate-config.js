import { api } from '/js/api.js';

export async function renderAffiliateConfig(container) {
  let config;
  try {
    config = await api.get('/admin/config');
  } catch (e) {
    container.innerHTML = `<div class="alert alert-danger">Erro: ${e.message}</div>`;
    return;
  }

  const minValue = (config.affiliateMinWithdrawalCents / 100).toFixed(2);
  const rateValue = (config.affiliateDefaultCommissionRate * 100).toFixed(0);

  container.innerHTML = `
    <div style="margin-bottom:20px">
      <h2 style="margin:0;font-size:1.25rem;font-weight:700">Configurações de Afiliados</h2>
    </div>

    <div class="card" style="max-width:520px">
      <div class="card-body">
        <div class="form-group">
          <label class="form-label">Saque mínimo (R$)</label>
          <input type="number" id="cfgMinWithdrawal" class="form-control"
            min="0" step="0.01" value="${minValue}">
          <small style="color:var(--color-text-muted)">Valor mínimo para que o afiliado possa solicitar um saque.</small>
        </div>
        <div class="form-group">
          <label class="form-label">Taxa de comissão padrão (%)</label>
          <input type="number" id="cfgDefaultRate" class="form-control"
            min="0" max="100" step="1" value="${rateValue}">
          <small style="color:var(--color-text-muted)">Aplicada a novos afiliados. Não altera taxas já configuradas individualmente.</small>
        </div>
        <div id="cfgMsg" style="margin-bottom:12px"></div>
        <button class="btn btn-primary" id="btnSaveConfig">Salvar configurações</button>
      </div>
    </div>
  `;

  document.getElementById('btnSaveConfig').addEventListener('click', async () => {
    const msg = document.getElementById('cfgMsg');
    const minCents = Math.round(parseFloat(document.getElementById('cfgMinWithdrawal').value) * 100);
    const rate     = parseFloat(document.getElementById('cfgDefaultRate').value) / 100;

    if (isNaN(minCents) || minCents < 0) {
      msg.innerHTML = `<span class="badge badge-danger">Valor mínimo inválido.</span>`;
      return;
    }
    if (isNaN(rate) || rate < 0 || rate > 1) {
      msg.innerHTML = `<span class="badge badge-danger">Taxa inválida (0–100%).</span>`;
      return;
    }

    try {
      await api.patch('/admin/config', {
        affiliateMinWithdrawalCents: minCents,
        affiliateDefaultCommissionRate: rate,
      });
      msg.innerHTML = `<span class="badge badge-success">Configurações salvas!</span>`;
    } catch (e) {
      msg.innerHTML = `<span class="badge badge-danger">${String(e.message).replace(/&/g,'&amp;').replace(/</g,'&lt;')}</span>`;
    }
  });
}
