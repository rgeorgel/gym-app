import { api } from '../api.js?v=202603311200';
import { showToast, createModal, openModal, closeModal, formatDate, statusBadge, emptyState } from '../ui.js?v=202603311200';
import { getUser } from '../auth.js?v=202603311200';
import { t } from '../i18n.js?v=202603311200';

let allAdmins = [];
const currentUser = getUser();

export async function renderAdmins(container) {
  container.innerHTML = `
    <div class="filters-bar">
      <span style="color:var(--gray-500);font-size:var(--font-size-sm);flex:1">${t('admins.none')}</span>
      <button class="btn btn-primary" id="btnNewAdmin">${t('admins.new')}</button>
    </div>
    <div id="adminsListContainer"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  await loadAdmins();
  document.getElementById('btnNewAdmin').addEventListener('click', () => openAdminModal());
}

async function loadAdmins() {
  try {
    allAdmins = await api.get('/admins');
    renderList(allAdmins);
  } catch (e) {
    showToast(t('error.prefix') + e.message, 'error');
  }
}

function renderList(admins) {
  const container = document.getElementById('adminsListContainer');
  if (!container) return;
  if (!admins.length) {
    container.innerHTML = `<div class="card card-body">${emptyState('👤', t('admins.none'))}</div>`;
    return;
  }
  container.innerHTML = `
    <div class="simple-list">
      ${admins.map(a => {
        const isSelf = a.id === currentUser?.id;
        return `
          <div class="simple-list-item">
            <div class="simple-list-info">
              <div class="simple-list-name">
                ${a.name}
                ${isSelf ? `<span class="badge badge-gray" style="font-size:0.7rem;margin-left:0.375rem">você</span>` : ''}
              </div>
              <div class="simple-list-sub">${a.email} · ${formatDate(a.createdAt)}</div>
            </div>
            <div class="simple-list-actions">
              ${statusBadge(a.status)}
              <button class="btn btn-secondary btn-sm btn-edit-admin" data-id="${a.id}">${t('btn.edit')}</button>
            </div>
          </div>
        `;
      }).join('')}
    </div>
  `;
  container.querySelectorAll('.btn-edit-admin').forEach(btn => {
    btn.addEventListener('click', () => openAdminModal(allAdmins.find(a => a.id === btn.dataset.id)));
  });
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
