import { api } from '/js/api.js';

export async function renderAffiliateDashboard(container) {
  let profile, balance;
  try {
    [profile, balance] = await Promise.all([
      api.get('/affiliate/me'),
      api.get('/affiliate/balance'),
    ]);
  } catch (e) {
    container.innerHTML = `<div class="alert alert-danger">Erro ao carregar dados: ${e.message}</div>`;
    return;
  }

  const canWithdraw = balance.availableBalance >= balance.minWithdrawalCents / 100;
  const minValue = (balance.minWithdrawalCents / 100).toFixed(2);

  container.innerHTML = `
    <div class="page-header">
      <div>
        <h1 class="page-title">Dashboard</h1>
        <p class="page-subtitle">Olá, ${escHtml(profile.name)}!</p>
      </div>
    </div>

    <!-- Balance cards -->
    <div class="stats-grid" style="margin-bottom:24px">
      <div class="stat-card">
        <div class="stat-label">Saldo disponível</div>
        <div class="stat-value" style="color:var(--color-success)">R$${fmtMoney(balance.availableBalance)}</div>
      </div>
      <div class="stat-card">
        <div class="stat-label">Total ganho</div>
        <div class="stat-value">R$${fmtMoney(balance.totalEarned)}</div>
      </div>
      <div class="stat-card">
        <div class="stat-label">Em processamento</div>
        <div class="stat-value" style="color:var(--color-warning)">R$${fmtMoney(balance.pendingWithdrawal)}</div>
      </div>
      <div class="stat-card">
        <div class="stat-label">Minha comissão</div>
        <div class="stat-value">${(profile.commissionRate * 100).toFixed(0)}%</div>
      </div>
    </div>

    <!-- Referral link -->
    <div class="card" style="margin-bottom:24px">
      <div class="card-body">
        <h3 style="margin:0 0 12px;font-size:var(--font-size-base);font-weight:600">Seu link de indicação</h3>
        <div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap">
          <input id="refLinkInput" class="form-control" readonly value="${escHtml(profile.referralLink)}"
            style="flex:1;min-width:200px;background:var(--color-bg-secondary);cursor:text">
          <button class="btn btn-primary" id="btnCopyLink">Copiar link</button>
        </div>
        <p style="margin:8px 0 0;font-size:var(--font-size-sm);color:var(--color-text-muted)">
          Código: <strong>${escHtml(profile.referralCode)}</strong>
        </p>
      </div>
    </div>

    <!-- Withdraw -->
    <div class="card" style="margin-bottom:24px">
      <div class="card-body">
        <h3 style="margin:0 0 12px;font-size:var(--font-size-base);font-weight:600">Solicitar saque</h3>
        ${canWithdraw ? `
          <div style="display:flex;gap:8px;align-items:flex-end;flex-wrap:wrap">
            <div style="flex:1;min-width:160px">
              <label class="form-label">Valor (mín. R$${minValue})</label>
              <input type="number" id="withdrawAmount" class="form-control"
                min="${minValue}" max="${balance.availableBalance.toFixed(2)}"
                step="0.01" value="${balance.availableBalance.toFixed(2)}">
            </div>
            <button class="btn btn-primary" id="btnWithdraw">Solicitar saque</button>
          </div>
          <div id="withdrawMsg" style="margin-top:8px"></div>
        ` : `
          <p style="color:var(--color-text-muted);margin:0">
            Saldo mínimo de R$${minValue} necessário para solicitar saque.
            Seu saldo atual é <strong>R$${fmtMoney(balance.availableBalance)}</strong>.
          </p>
        `}
      </div>
    </div>
  `;

  // Copy link
  document.getElementById('btnCopyLink').addEventListener('click', async () => {
    await navigator.clipboard.writeText(profile.referralLink);
    const btn = document.getElementById('btnCopyLink');
    btn.textContent = 'Copiado!';
    setTimeout(() => { btn.textContent = 'Copiar link'; }, 2000);
  });

  // Withdraw
  if (canWithdraw) {
    document.getElementById('btnWithdraw').addEventListener('click', async () => {
      const amount = parseFloat(document.getElementById('withdrawAmount').value);
      const msg = document.getElementById('withdrawMsg');
      if (isNaN(amount) || amount < balance.minWithdrawalCents / 100) {
        msg.innerHTML = `<span class="badge badge-danger">Valor inválido ou abaixo do mínimo.</span>`;
        return;
      }
      try {
        await api.post('/affiliate/withdrawal', { amount });
        msg.innerHTML = `<span class="badge badge-success">Solicitação enviada com sucesso!</span>`;
        document.getElementById('btnWithdraw').disabled = true;
      } catch (e) {
        msg.innerHTML = `<span class="badge badge-danger">${escHtml(e.message)}</span>`;
      }
    });
  }
}

function fmtMoney(v) {
  return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function escHtml(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
