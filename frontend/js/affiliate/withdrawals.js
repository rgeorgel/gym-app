import { api } from '/js/api.js';

const STATUS_MAP = {
  Pending: { label: 'Pendente', cls: 'badge-warning' },
  Approved: { label: 'Aprovado', cls: 'badge-success' },
  Rejected: { label: 'Rejeitado', cls: 'badge-danger' },
};

export async function renderAffiliateWithdrawals(container) {
  let [withdrawals, balance] = [[], null];
  try {
    [withdrawals, balance] = await Promise.all([
      api.get('/affiliate/withdrawals'),
      api.get('/affiliate/balance'),
    ]);
  } catch (e) {
    container.innerHTML = `<div class="alert alert-danger">Erro ao carregar saques: ${e.message}</div>`;
    return;
  }

  const canWithdraw = balance.availableBalance >= balance.minWithdrawalCents / 100;
  const minValue = (balance.minWithdrawalCents / 100).toFixed(2);

  const cards = withdrawals.length
    ? withdrawals.map(w => {
        const s = STATUS_MAP[w.status] ?? { label: w.status, cls: 'badge-secondary' };
        return `
          <div class="affiliate-card">
            <div class="affiliate-card-header">
              <div class="affiliate-card-title">R$${fmtMoney(w.requestedAmount)}</div>
              <span class="badge ${s.cls}">${s.label}</span>
            </div>
            <div class="affiliate-card-body">
              <div class="affiliate-card-row">
                <span class="affiliate-card-label">Solicitado em</span>
                <span>${fmtDate(w.createdAt)}</span>
              </div>
              <div class="affiliate-card-row">
                <span class="affiliate-card-label">Resolvido em</span>
                <span>${w.resolvedAt ? fmtDate(w.resolvedAt) : '—'}</span>
              </div>
              ${w.adminNotes ? `
              <div class="affiliate-card-row">
                <span class="affiliate-card-label">Observação</span>
                <span>${escHtml(w.adminNotes)}</span>
              </div>
              ` : ''}
            </div>
          </div>`;
      }).join('')
    : '<div class="empty-state"><p>Nenhuma solicitação ainda.</p></div>';

  container.innerHTML = `
    <div class="page-header">
      <h1 class="page-title">Saques</h1>
    </div>

    <!-- New withdrawal -->
    <div class="card" style="margin-bottom:24px">
      <div class="card-body">
        <h3 style="margin:0 0 12px;font-size:var(--font-size-base);font-weight:600">Solicitar saque</h3>
        <p style="margin:0 0 12px;font-size:var(--font-size-sm);color:var(--color-text-muted)">
          Saldo disponível: <strong>R$${fmtMoney(balance.availableBalance)}</strong>
        </p>
        ${canWithdraw ? `
          <div style="display:flex;gap:8px;align-items:flex-end;flex-wrap:wrap">
            <div style="flex:1;min-width:160px">
              <label class="form-label">Valor (mín. R$${minValue})</label>
              <input type="number" id="withdrawAmount" class="form-control"
                min="${minValue}" max="${balance.availableBalance.toFixed(2)}"
                step="0.01" value="${balance.availableBalance.toFixed(2)}">
            </div>
            <button class="btn btn-primary" id="btnWithdraw">Solicitar</button>
          </div>
          <div id="withdrawMsg" style="margin-top:8px"></div>
        ` : `
          <p style="color:var(--color-text-muted);margin:0">
            Saldo mínimo de R$${minValue} necessário para solicitar saque.
          </p>
        `}
      </div>
    </div>

    <!-- History -->
    <div class="affiliate-cards">${cards}</div>`;

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
        msg.innerHTML = `<span class="badge badge-success">Solicitação enviada! Recarregando...</span>`;
        setTimeout(() => renderAffiliateWithdrawals(container), 1500);
      } catch (e) {
        msg.innerHTML = `<span class="badge badge-danger">${escHtml(e.message)}</span>`;
      }
    });
  }
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
