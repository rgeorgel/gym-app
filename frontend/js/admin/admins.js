import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, formatDate, statusBadge, emptyState } from '../ui.js';
import { getUser } from '../auth.js';
import { t } from '../i18n.js';

let allAdmins = [];
const currentUser = getUser();

export async function renderAdmins(container) {
  container.innerHTML = `
    <div class="filters-bar">
      <span style="color:var(--gray-500);font-size:var(--font-size-sm);flex:1">${t('admins.none')}</span>
      <button class="btn btn-primary" id="btnNewAdmin">${t('admins.new')}</button>
    </div>
    <div class="card">
      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>${t('field.name')}</th>
              <th>${t('field.email')}</th>
              <th>${t('field.status')}</th>
              <th>${t('admins.col.createdAt')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody id="adminsTbody"><tr><td colspan="5"><div class="loading-center"><span class="spinner"></span></div></td></tr></tbody>
        </table>
      </div>
    </div>
  `;

  await loadAdmins();
  document.getElementById('btnNewAdmin').addEventListener('click', () => openAdminModal());
}

async function loadAdmins() {
  try {
    allAdmins = await api.get('/admins');
    renderTable(allAdmins);
  } catch (e) {
    showToast(t('error.prefix') + e.message, 'error');
  }
}

function renderTable(admins) {
  const tbody = document.getElementById('adminsTbody');
  if (!tbody) return;
  if (!admins.length) {
    tbody.innerHTML = `<tr><td colspan="5">${emptyState('👤', t('admins.none'))}</td></tr>`;
    return;
  }
  tbody.innerHTML = admins.map(a => {
    const isSelf = a.id === currentUser?.id;
    return `
      <tr>
        <td>
          <div class="font-medium">${a.name}</div>
          ${isSelf ? `<span class="badge badge-gray" style="font-size:0.7rem">você</span>` : ''}
        </td>
        <td class="text-sm text-muted">${a.email}</td>
        <td>${statusBadge(a.status)}</td>
        <td class="text-sm text-muted">${formatDate(a.createdAt)}</td>
        <td>
          <button class="btn btn-secondary btn-sm" onclick="window._editAdmin('${a.id}')">${t('btn.edit')}</button>
        </td>
      </tr>
    `;
  }).join('');

  window._editAdmin = (id) => openAdminModal(allAdmins.find(a => a.id === id));
}

function openAdminModal(admin = null) {
  const isSelf = admin?.id === currentUser?.id;

  createModal({
    id: 'adminModal',
    title: admin ? t('admins.title.edit') : t('admins.title.new'),
    body: `
      <form id="adminForm">
        <div class="form-group">
          <label class="form-label">${t('field.name')} *</label>
          <input class="form-control" id="aName" required value="${admin?.name ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.email')} *</label>
          <input class="form-control" id="aEmail" type="email" required value="${admin?.email ?? ''}" ${admin ? 'readonly' : ''}>
        </div>
        ${!admin ? `
        <div class="form-group">
          <label class="form-label">${t('students.field.initialPassword')} *</label>
          <input class="form-control" id="aPassword" type="password" placeholder="${t('students.field.passwordPlaceholder')}" required>
        </div>` : ''}
        ${admin ? `
        <div class="form-group">
          <label class="form-label">${t('field.status')}</label>
          <select class="form-control" id="aStatus" ${isSelf ? 'disabled' : ''}>
            <option value="Active" ${admin.status === 'Active' ? 'selected' : ''}>${t('status.active')}</option>
            <option value="Inactive" ${admin.status === 'Inactive' ? 'selected' : ''}>${t('status.inactive')}</option>
            <option value="Suspended" ${admin.status === 'Suspended' ? 'selected' : ''}>${t('status.suspended')}</option>
          </select>
          ${isSelf ? `<p class="text-sm text-muted" style="margin-top:0.25rem">${t('admins.selfDeactivate')}</p>` : ''}
        </div>
        <div class="form-group">
          <label style="display:flex;align-items:center;gap:0.5rem;cursor:pointer">
            <input type="checkbox" id="aReceivesReminders" ${admin.receivesSubscriptionReminders ? 'checked' : ''}>
            <span>${t('admins.receivesReminders')}</span>
          </label>
          <p class="text-sm text-muted" style="margin-top:0.25rem">${t('admins.receivesReminders.hint')}</p>
        </div>` : ''}
      </form>
    `,
    footer: `
      ${admin ? `<button class="btn btn-secondary" id="btnResetPwd" style="margin-right:auto">${t('admins.resetPwd')}</button>` : ''}
      <button class="btn btn-secondary" onclick="closeModal('adminModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveAdmin">${t('btn.save')}</button>
    `
  });
  openModal('adminModal');

  document.getElementById('btnSaveAdmin').addEventListener('click', async () => {
    const name = document.getElementById('aName').value.trim();
    if (!name) { showToast(t('error.prefix') + 'Nome obrigatório', 'error'); return; }

    const btn = document.getElementById('btnSaveAdmin');
    btn.disabled = true;
    try {
      if (admin) {
        const status = isSelf ? admin.status : document.getElementById('aStatus').value;
        const receivesSubscriptionReminders = document.getElementById('aReceivesReminders')?.checked ?? admin.receivesSubscriptionReminders;
        await api.put(`/admins/${admin.id}`, { name, status, receivesSubscriptionReminders });
        showToast(t('admins.saved'), 'success');
      } else {
        const email = document.getElementById('aEmail').value.trim();
        const password = document.getElementById('aPassword').value;
        if (!email) { showToast(t('error.prefix') + 'E-mail obrigatório', 'error'); btn.disabled = false; return; }
        if (!password || password.length < 6) { showToast(t('students.password.min'), 'error'); btn.disabled = false; return; }
        await api.post('/admins', { name, email, password });
        showToast(t('admins.created'), 'success');
      }
      closeModal('adminModal');
      await loadAdmins();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
      btn.disabled = false;
    }
  });

  document.getElementById('btnResetPwd')?.addEventListener('click', () => openResetPwdModal(admin));
}

function openResetPwdModal(admin) {
  createModal({
    id: 'resetPwdModal',
    title: t('admins.resetPwd'),
    body: `
      <div class="form-group">
        <label class="form-label">${t('students.field.initialPassword')} *</label>
        <input class="form-control" id="newPwd" type="password" placeholder="${t('students.field.passwordPlaceholder')}">
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('resetPwdModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveNewPwd">${t('btn.save')}</button>
    `
  });
  openModal('resetPwdModal');

  document.getElementById('btnSaveNewPwd').addEventListener('click', async () => {
    const pwd = document.getElementById('newPwd').value;
    if (!pwd || pwd.length < 6) { showToast(t('students.password.min'), 'error'); return; }
    const btn = document.getElementById('btnSaveNewPwd');
    btn.disabled = true;
    try {
      await api.post(`/admins/${admin.id}/reset-password`, { newPassword: pwd });
      showToast(t('admins.resetPwd.saved'), 'success');
      closeModal('resetPwdModal');
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
      btn.disabled = false;
    }
  });
}
