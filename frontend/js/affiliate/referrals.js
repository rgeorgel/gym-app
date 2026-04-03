import { api } from '/js/api.js';

const STATUS_LABELS = {
  Trial: { label: 'Trial', cls: 'badge-warning' },
  Active: { label: 'Ativo', cls: 'badge-success' },
  PastDue: { label: 'Em atraso', cls: 'badge-danger' },
  Canceled: { label: 'Cancelado', cls: 'badge-secondary' },
  Suspended: { label: 'Suspenso', cls: 'badge-danger' },
};

export async function renderAffiliateReferrals(container) {
  let referrals;
  try {
    referrals = await api.get('/affiliate/referrals');
  } catch (e) {
    container.innerHTML = `<div class="alert alert-danger">Erro ao carregar indicações: ${e.message}</div>`;
    return;
  }

  if (!referrals.length) {
    container.innerHTML = `
      <div class="page-header"><h1 class="page-title">Indicações</h1></div>
      <div class="empty-state">
        <p>Nenhuma indicação ainda. Compartilhe seu link para começar!</p>
      </div>`;
    return;
  }

  const cards = referrals.map(r => {
    const s = STATUS_LABELS[r.subscriptionStatus] ?? { label: r.subscriptionStatus, cls: 'badge-secondary' };
    const active = r.isActive ? '' : ' <span class="badge badge-danger">Inativo</span>';
    return `
      <div class="affiliate-card">
        <div class="affiliate-card-header">
          <div class="affiliate-card-title">${escHtml(r.tenantName)}</div>
          <span class="badge ${s.cls}">${s.label}</span>
        </div>
        <div class="affiliate-card-body">
          <div class="affiliate-card-row">
            <span class="affiliate-card-label">Slug</span>
            <code>${escHtml(r.tenantSlug)}</code>
          </div>
          <div class="affiliate-card-row">
            <span class="affiliate-card-label">Cadastro</span>
            <span>${fmtDate(r.registeredAt)}</span>
          </div>
          <div class="affiliate-card-row">
            <span class="affiliate-card-label">Comissão total</span>
            <strong>R$${fmtMoney(r.totalCommission)}</strong>
          </div>
        </div>
        ${!r.isActive ? '<div class="affiliate-card-inactive">Inativo</div>' : ''}
      </div>`;
  }).join('');

  container.innerHTML = `
    <div class="page-header">
      <h1 class="page-title">Indicações</h1>
      <p class="page-subtitle">${referrals.length} salão(ões) indicado(s)</p>
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
