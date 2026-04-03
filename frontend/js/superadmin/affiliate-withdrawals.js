import { api } from '/js/api.js';

const STATUS_MAP = {
  Pending: { label: 'Pendente', cls: 'badge-warning' },
  Approved: { label: 'Aprovado', cls: 'badge-success' },
  Rejected: { label: 'Rejeitado', cls: 'badge-danger' },
};

export async function renderSuperAdminWithdrawals(container) {
  await loadWithdrawals(container, 'Pending');
}

async function loadWithdrawals(container, statusFilter) {
  let withdrawals;
  try {
    const qs = statusFilter && statusFilter !== 'all' ? `?status=${statusFilter}` : '';
    withdrawals = await api.get(`/admin/affiliates/withdrawals${qs}`);
  } catch (e) {
    container.innerHTML = `<div class="alert alert-danger">Erro: ${e.message}</div>`;
    return;
  }

  const rows = withdrawals.map(w => {
    const s = STATUS_MAP[w.status] ?? { label: w.status, cls: 'badge-secondary' };
    const isPending = w.status === 'Pending';
    return `
      <tr>
        <td><strong>${escHtml(w.affiliateName)}</strong><br><small style="color:var(--color-text-muted)">${escHtml(w.affiliateEmail)}</small></td>
        <td><strong>R$${fmtMoney(w.requestedAmount)}</strong></td>
        <td><span class="badge ${s.cls}">${s.label}</span></td>
        <td>${fmtDate(w.createdAt)}</td>
        <td>${w.resolvedAt ? fmtDate(w.resolvedAt) : '—'}</td>
        <td>${escHtml(w.adminNotes ?? '—')}</td>
        <td>
          ${isPending ? `
            <div style="display:flex;gap:4px;flex-wrap:wrap">
              <button class="btn btn-sm" style="background:var(--color-success);color:white"
                data-action="approve" data-id="${w.id}">Aprovar</button>
              <button class="btn btn-sm btn-danger"
                data-action="reject" data-id="${w.id}">Rejeitar</button>
            </div>
          ` : '—'}
        </td>
      </tr>`;
  }).join('') || `<tr><td colspan="7" style="text-align:center;padding:32px;color:var(--color-text-muted)">Nenhuma solicitação encontrada.</td></tr>`;

  container.innerHTML = `
    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:20px;flex-wrap:wrap;gap:12px">
      <h2 style="margin:0;font-size:1.25rem;font-weight:700">Solicitações de Saque</h2>
      <div style="display:flex;gap:8px">
        <button class="btn btn-sm ${statusFilter === 'Pending' ? 'btn-primary' : 'btn-secondary'}" data-filter="Pending">Pendentes</button>
        <button class="btn btn-sm ${statusFilter === 'all' ? 'btn-primary' : 'btn-secondary'}" data-filter="all">Todos</button>
      </div>
    </div>

    <div class="card">
      <div class="table-responsive">
        <table class="table">
          <thead>
            <tr>
              <th>Afiliado</th>
              <th>Valor</th>
              <th>Status</th>
              <th>Solicitado</th>
              <th>Resolvido</th>
              <th>Observação</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>
    </div>

    <!-- Resolve modal -->
    <div id="resolveModal" style="display:none;position:fixed;inset:0;background:rgba(0,0,0,0.5);z-index:1000;align-items:center;justify-content:center">
      <div class="card" style="width:100%;max-width:420px;margin:16px">
        <div class="card-body">
          <h3 id="resolveTitle" style="margin:0 0 16px;font-weight:700"></h3>
          <div class="form-group">
            <label class="form-label">Observação (opcional)</label>
            <textarea id="resolveNotes" class="form-control" rows="3" placeholder="Motivo ou instrução de pagamento..."></textarea>
          </div>
          <div id="resolveMsg" style="margin-bottom:12px"></div>
          <div style="display:flex;gap:8px;justify-content:flex-end">
            <button class="btn btn-secondary" id="btnCancelResolve">Cancelar</button>
            <button class="btn btn-primary" id="btnConfirmResolve">Confirmar</button>
          </div>
        </div>
      </div>
    </div>
  `;

  // Filter buttons
  container.querySelectorAll('[data-filter]').forEach(btn => {
    btn.addEventListener('click', () => loadWithdrawals(container, btn.dataset.filter));
  });

  // Approve / reject
  let pendingId = null, pendingStatus = null;
  const modal = document.getElementById('resolveModal');

  container.querySelectorAll('[data-action]').forEach(btn => {
    btn.addEventListener('click', () => {
      pendingId     = btn.dataset.id;
      pendingStatus = btn.dataset.action === 'approve' ? 'Approved' : 'Rejected';
      document.getElementById('resolveTitle').textContent =
        pendingStatus === 'Approved' ? 'Aprovar saque' : 'Rejeitar saque';
      document.getElementById('resolveNotes').value = '';
      document.getElementById('resolveMsg').innerHTML = '';
      modal.style.display = 'flex';
    });
  });

  document.getElementById('btnCancelResolve').addEventListener('click', () => {
    modal.style.display = 'none';
  });

  document.getElementById('btnConfirmResolve').addEventListener('click', async () => {
    const msg = document.getElementById('resolveMsg');
    try {
      await api.patch(`/admin/affiliates/withdrawals/${pendingId}`, {
        status: pendingStatus,
        adminNotes: document.getElementById('resolveNotes').value.trim() || null,
      });
      modal.style.display = 'none';
      await loadWithdrawals(container, statusFilter);
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
