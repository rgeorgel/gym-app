import { api } from '/js/api.js';

export async function renderSuperAdminAffiliates(container) {
  await renderList(container);
}

async function renderList(container) {
  let affiliates;
  try {
    affiliates = await api.get('/admin/affiliates');
  } catch (e) {
    container.innerHTML = `<div class="alert alert-danger">Erro: ${e.message}</div>`;
    return;
  }

  const rows = affiliates.map(a => `
    <tr style="cursor:pointer" data-id="${a.id}" class="affiliate-row">
      <td><strong>${escHtml(a.name)}</strong><br><small style="color:var(--color-text-muted)">${escHtml(a.email)}</small></td>
      <td><code>${escHtml(a.referralCode)}</code></td>
      <td>${(a.commissionRate * 100).toFixed(0)}%</td>
      <td>${a.referralCount}</td>
      <td>R$${fmtMoney(a.totalEarned)}</td>
      <td style="color:var(--color-success);font-weight:700">R$${fmtMoney(a.availableBalance)}</td>
      <td>${fmtDate(a.createdAt)}</td>
    </tr>`).join('') || `<tr><td colspan="7" style="text-align:center;padding:32px;color:var(--color-text-muted)">Nenhum afiliado cadastrado.</td></tr>`;

  container.innerHTML = `
    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:20px;flex-wrap:wrap;gap:12px">
      <h2 style="margin:0;font-size:1.25rem;font-weight:700">Afiliados (${affiliates.length})</h2>
      <button class="btn btn-primary" id="btnNewAffiliate">+ Novo Afiliado</button>
    </div>

    <div class="card">
      <div class="table-responsive">
        <table class="table">
          <thead>
            <tr>
              <th>Nome / Email</th>
              <th>Código</th>
              <th>Taxa</th>
              <th>Indicações</th>
              <th>Total ganho</th>
              <th>Saldo disponível</th>
              <th>Cadastro</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>
    </div>

    <!-- New affiliate modal -->
    <div id="affiliateModal" style="display:none;position:fixed;inset:0;background:rgba(0,0,0,0.5);z-index:1000;align-items:center;justify-content:center">
      <div class="card" style="width:100%;max-width:480px;max-height:90vh;overflow-y:auto;margin:16px">
        <div class="card-body">
          <h3 style="margin:0 0 20px;font-weight:700">Novo Afiliado</h3>
          <div class="form-group"><label class="form-label">Nome</label>
            <input id="newName" class="form-control" placeholder="Nome completo"></div>
          <div class="form-group"><label class="form-label">Email</label>
            <input id="newEmail" class="form-control" type="email" placeholder="email@exemplo.com"></div>
          <div class="form-group"><label class="form-label">Senha</label>
            <input id="newPassword" class="form-control" type="password" placeholder="Mínimo 6 caracteres"></div>
          <div class="form-group"><label class="form-label">Código de indicação</label>
            <input id="newCode" class="form-control" placeholder="ex: joao2024"></div>
          <div class="form-group"><label class="form-label">Taxa de comissão (%)</label>
            <input id="newRate" class="form-control" type="number" min="0" max="100" step="1" value="20"></div>
          <div id="newAffiliateMsg" style="margin-bottom:12px"></div>
          <div style="display:flex;gap:8px;justify-content:flex-end">
            <button class="btn btn-secondary" id="btnCancelAffiliate">Cancelar</button>
            <button class="btn btn-primary" id="btnSaveAffiliate">Criar afiliado</button>
          </div>
        </div>
      </div>
    </div>
  `;

  // Row click → detail
  container.querySelectorAll('.affiliate-row').forEach(row => {
    row.addEventListener('click', () => renderDetail(container, row.dataset.id));
  });

  // New affiliate
  const modal = document.getElementById('affiliateModal');
  document.getElementById('btnNewAffiliate').addEventListener('click', () => {
    modal.style.display = 'flex';
  });
  document.getElementById('btnCancelAffiliate').addEventListener('click', () => {
    modal.style.display = 'none';
  });
  document.getElementById('btnSaveAffiliate').addEventListener('click', async () => {
    const msg = document.getElementById('newAffiliateMsg');
    const rate = parseFloat(document.getElementById('newRate').value) / 100;
    try {
      await api.post('/admin/affiliates', {
        name: document.getElementById('newName').value.trim(),
        email: document.getElementById('newEmail').value.trim(),
        password: document.getElementById('newPassword').value,
        referralCode: document.getElementById('newCode').value.trim(),
        commissionRate: rate,
      });
      modal.style.display = 'none';
      await renderList(container);
    } catch (e) {
      msg.innerHTML = `<span class="badge badge-danger">${escHtml(e.message)}</span>`;
    }
  });
}

async function renderDetail(container, id) {
  let affiliate;
  try {
    affiliate = await api.get(`/admin/affiliates/${id}`);
  } catch (e) {
    container.innerHTML = `<div class="alert alert-danger">Erro: ${e.message}</div>`;
    return;
  }

  const referralRows = affiliate.referrals.map(r => `
    <tr>
      <td>${escHtml(r.tenantName)}</td>
      <td><span class="badge ${r.subscriptionStatus === 'Active' ? 'badge-success' : r.subscriptionStatus === 'Trial' ? 'badge-warning' : 'badge-secondary'}">${r.subscriptionStatus}</span></td>
      <td>${fmtDate(r.registeredAt)}</td>
      <td>R$${fmtMoney(r.totalCommission)}</td>
    </tr>`).join('') || `<tr><td colspan="4" style="text-align:center;color:var(--color-text-muted);padding:16px">Sem indicações.</td></tr>`;

  const commissionRows = affiliate.commissions.slice(0, 20).map(c => `
    <tr>
      <td>${fmtDate(c.createdAt)}</td>
      <td>${escHtml(c.tenantName)}</td>
      <td>R$${fmtMoney(c.grossAmount)}</td>
      <td>${(c.rate * 100).toFixed(0)}%</td>
      <td><strong>R$${fmtMoney(c.commissionAmount)}</strong></td>
      <td><span class="badge ${c.status === 'Paid' ? 'badge-success' : 'badge-warning'}">${c.status === 'Paid' ? 'Pago' : 'Pendente'}</span></td>
    </tr>`).join('') || `<tr><td colspan="6" style="text-align:center;color:var(--color-text-muted);padding:16px">Sem comissões.</td></tr>`;

  container.innerHTML = `
    <div style="margin-bottom:20px">
      <button class="btn btn-secondary btn-sm" id="btnBack">← Voltar</button>
    </div>

    <div class="card" style="margin-bottom:20px">
      <div class="card-body">
        <div style="display:flex;justify-content:space-between;align-items:flex-start;flex-wrap:wrap;gap:12px">
          <div>
            <h2 style="margin:0 0 4px;font-weight:800">${escHtml(affiliate.name)}</h2>
            <p style="margin:0;color:var(--color-text-muted)">${escHtml(affiliate.email)} · Código: <code>${escHtml(affiliate.referralCode)}</code></p>
            <p style="margin:4px 0 0;font-size:var(--font-size-sm)">Link: <a href="${escHtml(affiliate.referralLink)}" target="_blank">${escHtml(affiliate.referralLink)}</a></p>
          </div>
          <div style="display:flex;gap:8px;align-items:center">
            <input type="number" id="rateInput" class="form-control" style="width:90px"
              min="0" max="100" step="1" value="${(affiliate.commissionRate * 100).toFixed(0)}">
            <span style="font-size:var(--font-size-sm)">%</span>
            <button class="btn btn-primary btn-sm" id="btnUpdateRate">Salvar taxa</button>
          </div>
        </div>
        <div style="display:flex;gap:24px;margin-top:16px;flex-wrap:wrap">
          <div><div style="font-size:var(--font-size-sm);color:var(--color-text-muted)">Total ganho</div><div style="font-weight:700">R$${fmtMoney(affiliate.totalEarned)}</div></div>
          <div><div style="font-size:var(--font-size-sm);color:var(--color-text-muted)">Saldo disponível</div><div style="font-weight:700;color:var(--color-success)">R$${fmtMoney(affiliate.availableBalance)}</div></div>
          <div><div style="font-size:var(--font-size-sm);color:var(--color-text-muted)">Indicações</div><div style="font-weight:700">${affiliate.referrals.length}</div></div>
        </div>
        <div id="rateMsg" style="margin-top:8px"></div>
      </div>
    </div>

    <h3 style="font-weight:700;margin-bottom:12px">Indicações</h3>
    <div class="card" style="margin-bottom:20px">
      <div class="table-responsive">
        <table class="table">
          <thead><tr><th>Salão</th><th>Status</th><th>Cadastro</th><th>Comissão total</th></tr></thead>
          <tbody>${referralRows}</tbody>
        </table>
      </div>
    </div>

    <h3 style="font-weight:700;margin-bottom:12px">Últimas comissões</h3>
    <div class="card">
      <div class="table-responsive">
        <table class="table">
          <thead><tr><th>Data</th><th>Salão</th><th>Bruto</th><th>Taxa</th><th>Comissão</th><th>Status</th></tr></thead>
          <tbody>${commissionRows}</tbody>
        </table>
      </div>
    </div>
  `;

  document.getElementById('btnBack').addEventListener('click', () => renderList(container));

  document.getElementById('btnUpdateRate').addEventListener('click', async () => {
    const msg = document.getElementById('rateMsg');
    const rate = parseFloat(document.getElementById('rateInput').value) / 100;
    try {
      await api.patch(`/admin/affiliates/${id}/rate`, { commissionRate: rate });
      msg.innerHTML = `<span class="badge badge-success">Taxa atualizada!</span>`;
    } catch (e) {
      msg.innerHTML = `<span class="badge badge-danger">${escHtml(e.message)}</span>`;
    }
  });
}

function fmtDate(iso) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('pt-BR');
}

function fmtMoney(v) {
  return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function escHtml(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
