import { api } from '../api.js';
import { showToast, formatDate } from '../ui.js';
import { getUser } from '../auth.js';

export async function renderSuperAdmins(container) {
  container.innerHTML = `<div class="loading-center"><span class="spinner"></span></div>`;

  let admins;
  try {
    admins = await api.get('/admin/super-admins');
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro: ${e.message}</div></div>`;
    return;
  }

  const currentUserId = getUser()?.id;

  container.innerHTML = `
    <div style="display:flex;flex-direction:column;gap:1.25rem">
      <div style="display:flex;justify-content:flex-end">
        <button class="btn btn-primary btn-sm" id="btnNewSuperAdmin">+ Novo Super Admin</button>
      </div>

      <div class="card">
        <div class="card-body" style="padding:0">
          <div class="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Nome</th>
                  <th>E-mail</th>
                  <th>Telefone</th>
                  <th>Status</th>
                  <th>Criado em</th>
                  <th style="width:140px"></th>
                </tr>
              </thead>
              <tbody id="superAdminsBody">
                ${admins.map(a => rowHtml(a, currentUserId)).join('')}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal -->
    <div id="saModal" style="display:none;position:fixed;inset:0;background:rgba(0,0,0,0.45);z-index:1000;align-items:center;justify-content:center">
      <div class="card" style="width:100%;max-width:440px;margin:1rem">
        <div class="card-body" style="padding:1.5rem">
          <h3 id="saModalTitle" style="margin:0 0 1.25rem">Super Admin</h3>
          <div class="form-group">
            <label class="form-label">Nome</label>
            <input class="form-control" id="saName">
          </div>
          <div class="form-group">
            <label class="form-label">E-mail</label>
            <input class="form-control" id="saEmail" type="email">
          </div>
          <div class="form-group">
            <label class="form-label">Telefone</label>
            <input class="form-control" id="saPhone" type="tel">
          </div>
          <div class="form-group">
            <label class="form-label" id="saPasswordLabel">Senha</label>
            <input class="form-control" id="saPassword" type="password" autocomplete="new-password">
          </div>
          <div style="display:flex;gap:0.75rem;justify-content:flex-end;margin-top:1.25rem">
            <button class="btn btn-secondary" id="saCancel">Cancelar</button>
            <button class="btn btn-primary" id="saConfirm">Salvar</button>
          </div>
        </div>
      </div>
    </div>
  `;

  // --- create ---
  container.querySelector('#btnNewSuperAdmin').addEventListener('click', () => {
    openModal(null);
  });

  // --- edit / toggle via delegation ---
  container.querySelector('#superAdminsBody').addEventListener('click', async (e) => {
    const editBtn = e.target.closest('[data-action="edit"]');
    const toggleBtn = e.target.closest('[data-action="toggle"]');

    if (editBtn) {
      const id = editBtn.dataset.id;
      const admin = admins.find(a => a.id === id);
      if (admin) openModal(admin);
    }

    if (toggleBtn) {
      const id = toggleBtn.dataset.id;
      const isActive = toggleBtn.dataset.active === 'true';
      toggleBtn.disabled = true;
      try {
        const updated = await api.put(`/admin/super-admins/${id}/status`, { isActive: !isActive });
        const idx = admins.findIndex(a => a.id === id);
        if (idx !== -1) admins[idx] = updated;
        container.querySelector('#superAdminsBody').innerHTML = admins.map(a => rowHtml(a, currentUserId)).join('');
        showToast(updated.isActive ? 'Super admin habilitado.' : 'Super admin desabilitado.', 'success');
      } catch (err) {
        showToast('Erro: ' + err.message, 'error');
        toggleBtn.disabled = false;
      }
    }
  });

  // --- modal logic ---
  let editingId = null;

  function openModal(admin) {
    editingId = admin?.id ?? null;
    container.querySelector('#saModalTitle').textContent = admin ? 'Editar Super Admin' : 'Novo Super Admin';
    container.querySelector('#saName').value = admin?.name ?? '';
    container.querySelector('#saEmail').value = admin?.email ?? '';
    container.querySelector('#saPhone').value = admin?.phone ?? '';
    container.querySelector('#saPassword').value = '';
    container.querySelector('#saPasswordLabel').textContent = admin ? 'Nova senha (deixe em branco para manter)' : 'Senha';
    container.querySelector('#saModal').style.display = 'flex';
  }

  container.querySelector('#saCancel').addEventListener('click', () => {
    container.querySelector('#saModal').style.display = 'none';
  });

  container.querySelector('#saConfirm').addEventListener('click', async () => {
    const name = container.querySelector('#saName').value.trim();
    const email = container.querySelector('#saEmail').value.trim();
    const phone = container.querySelector('#saPhone').value.trim() || null;
    const password = container.querySelector('#saPassword').value;

    if (!name || !email) {
      showToast('Nome e e-mail são obrigatórios.', 'error');
      return;
    }
    if (!editingId && !password) {
      showToast('Senha é obrigatória para novos super admins.', 'error');
      return;
    }

    const btn = container.querySelector('#saConfirm');
    btn.disabled = true;
    try {
      let result;
      if (editingId) {
        result = await api.put(`/admin/super-admins/${editingId}`, { name, email, phone, password: password || null });
        const idx = admins.findIndex(a => a.id === editingId);
        if (idx !== -1) admins[idx] = result;
        showToast('Super admin atualizado.', 'success');
      } else {
        result = await api.post('/admin/super-admins', { name, email, phone, password });
        admins.push(result);
        showToast('Super admin criado.', 'success');
      }
      container.querySelector('#superAdminsBody').innerHTML = admins.map(a => rowHtml(a, currentUserId)).join('');
      container.querySelector('#saModal').style.display = 'none';
    } catch (err) {
      showToast('Erro: ' + err.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });
}

function rowHtml(a, currentUserId) {
  const isSelf = a.id === currentUserId;
  const statusBadge = a.isActive
    ? `<span class="badge badge-success">Ativo</span>`
    : `<span class="badge badge-gray">Inativo</span>`;

  const toggleLabel = a.isActive ? 'Desabilitar' : 'Habilitar';
  const toggleClass = a.isActive ? 'btn-secondary' : 'btn-primary';

  return `
    <tr>
      <td><strong>${a.name}</strong>${isSelf ? ' <span class="badge badge-gray" style="font-size:0.65rem">você</span>' : ''}</td>
      <td>${a.email}</td>
      <td>${a.phone ?? '<span class="text-muted">—</span>'}</td>
      <td>${statusBadge}</td>
      <td>${formatDate(a.createdAt)}</td>
      <td style="display:flex;gap:0.5rem;justify-content:flex-end">
        <button class="btn btn-secondary btn-sm" data-action="edit" data-id="${a.id}">Editar</button>
        <button class="btn ${toggleClass} btn-sm" data-action="toggle" data-id="${a.id}" data-active="${a.isActive}"
          ${isSelf ? 'disabled title="Você não pode alterar seu próprio status"' : ''}>${toggleLabel}</button>
      </td>
    </tr>
  `;
}
