import { api } from '/js/api.js';

const STATUS_MAP = {
  Pending: { label: 'Pendente', cls: 'badge-warning' },
  Paid: { label: 'Pago', cls: 'badge-success' },
};

export async function renderAffiliateCommissions(container) {
  let data;
  try {
    data = await api.get('/affiliate/commissions?page=1&pageSize=100');
  } catch (e) {
    container.innerHTML = `<div class="alert alert-danger">Erro ao carregar comissões: ${e.message}</div>`;
    return;
  }

  if (!data.items.length) {
    container.innerHTML = `
      <div class="page-header"><h1 class="page-title">Comissões</h1></div>
      <div class="empty-state"><p>Nenhuma comissão gerada ainda.</p></div>`;
    return;
  }

  const cards = data.items.map(c => {
    const s = STATUS_MAP[c.status] ?? { label: c.status, cls: 'badge-secondary' };
    return `
      <div class="affiliate-card">
        <div class="affiliate-card-header">
          <div class="affiliate-card-title">${escHtml(c.tenantName)}</div>
          <span class="badge ${s.cls}">${s.label}</span>
        </div>
        <div class="affiliate-card-body">
          <div class="affiliate-card-row">
            <span class="affiliate-card-label">Data</span>
            <span>${fmtDate(c.createdAt)}</span>
          </div>
          <div class="affiliate-card-row">
            <span class="affiliate-card-label">Valor bruto</span>
            <span>R$${fmtMoney(c.grossAmount)}</span>
          </div>
          <div class="affiliate-card-row">
            <span class="affiliate-card-label">Taxa</span>
            <span>${(c.rate * 100).toFixed(0)}%</span>
          </div>
          <div class="affiliate-card-row">
            <span class="affiliate-card-label">Comissão</span>
            <strong style="color:var(--color-success)">R$${fmtMoney(c.commissionAmount)}</strong>
          </div>
        </div>
      </div>`;
  }).join('');

  container.innerHTML = `
    <div class="page-header">
      <h1 class="page-title">Comissões</h1>
      <p class="page-subtitle">${data.total} registro(s)</p>
    </div>
    <div class="affiliate-cards">${cards}</div>`;
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
