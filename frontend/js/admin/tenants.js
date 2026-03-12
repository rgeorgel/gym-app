import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, formatDate, confirm } from '../ui.js';
import { t } from '../i18n.js';

const PLANS = { Basic: 'Basic', Pro: 'Pro', Enterprise: 'Enterprise' };

export async function renderTenants(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewTenant">${t('tenants.new')}</button>
    </div>
    <div id="tenantsList"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  await loadTenants();

  document.getElementById('btnNewTenant').addEventListener('click', () => openTenantModal());
}

async function loadTenants() {
  const list = document.getElementById('tenantsList');
  try {
    const tenants = await api.get('/admin/tenants');

    if (!tenants.length) {
      list.innerHTML = emptyState('🏢', t('tenants.none'));
      return;
    }

    list.innerHTML = `
      <div class="card">
        <div class="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>${t('field.name')}</th>
                <th>Slug</th>
                <th>${t('tenants.col.plan')}</th>
                <th>${t('field.status')}</th>
                <th>${t('tenants.col.createdAt')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              ${tenants.map(ten => `
                <tr>
                  <td class="font-medium">${ten.name}</td>
                  <td class="text-sm text-muted">${ten.slug}</td>
                  <td class="text-sm">${PLANS[ten.plan] ?? ten.plan}</td>
                  <td><span class="badge ${ten.isActive ? 'badge-success' : 'badge-gray'}">${ten.isActive ? t('tenants.status.active') : t('tenants.status.inactive')}</span></td>
                  <td class="text-sm text-muted">${formatDate(ten.createdAt)}</td>
                  <td>
                    <button class="btn btn-secondary btn-sm" data-edit="${ten.id}">${t('btn.edit')}</button>
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      </div>
    `;

    list.querySelectorAll('[data-edit]').forEach(btn => {
      btn.addEventListener('click', () => openTenantModal(tenants.find(ten => ten.id === btn.dataset.edit)));
    });
  } catch (e) {
    list.innerHTML = `<div class="empty-state"><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
  }
}

function openTenantModal(tenant = null) {
  createModal({
    id: 'tenantModal',
    title: tenant ? t('tenants.title.edit') : t('tenants.title.new'),
    body: `
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group" style="grid-column:1/-1">
          <label class="form-label">${t('field.name')} *</label>
          <input class="form-control" id="tName" value="${tenant?.name ?? ''}">
        </div>
        ${!tenant ? `
        <div class="form-group" style="grid-column:1/-1">
          <label class="form-label">Slug * <span class="text-muted" style="font-weight:400;font-size:0.75rem">${t('tenants.field.slugNote')}</span></label>
          <input class="form-control" id="tSlug" placeholder="ex: boxe-elite" value="">
        </div>
        ` : `
        <div class="form-group" style="grid-column:1/-1">
          <label class="form-label">Slug</label>
          <input class="form-control" id="tSlug" value="${tenant.slug}" readonly style="background:var(--gray-50)">
        </div>
        `}
        <div class="form-group">
          <label class="form-label">${t('tenants.field.primaryColor')}</label>
          <input class="form-control" id="tPrimary" type="color" value="${tenant?.primaryColor ?? '#1a1a2e'}" style="height:42px;padding:2px 4px;cursor:pointer">
        </div>
        <div class="form-group">
          <label class="form-label">${t('tenants.field.secondaryColor')}</label>
          <input class="form-control" id="tSecondary" type="color" value="${tenant?.secondaryColor ?? '#e94560'}" style="height:42px;padding:2px 4px;cursor:pointer">
        </div>
        <div class="form-group">
          <label class="form-label">${t('tenants.field.plan')}</label>
          <select class="form-control" id="tPlan" ${tenant ? 'disabled' : ''}>
            ${Object.entries(PLANS).map(([v, l]) => `<option value="${v}" ${tenant?.plan === v ? 'selected' : ''}>${l}</option>`).join('')}
          </select>
        </div>
        ${tenant ? `
        <div class="form-group" style="grid-column:1/-1">
          <label class="form-label">${t('tenants.field.customDomain')} <span class="text-muted" style="font-weight:400;font-size:0.75rem">${t('tenants.field.customDomainNote')}</span></label>
          <input class="form-control" id="tCustomDomain" placeholder="${t('tenants.field.customDomainPlaceholder')}" value="${tenant.customDomain ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.status')}</label>
          <select class="form-control" id="tActive">
            <option value="true" ${tenant.isActive ? 'selected' : ''}>${t('tenants.status.active')}</option>
            <option value="false" ${!tenant.isActive ? 'selected' : ''}>${t('tenants.status.inactive')}</option>
          </select>
        </div>
        ` : ''}
      </div>
      ${!tenant ? `
      <div style="border-top:1px solid var(--gray-200);margin-top:0.5rem;padding-top:1rem">
        <div class="text-sm font-medium" style="margin-bottom:0.75rem;color:var(--gray-600)">${t('tenants.admin.section')}</div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
          <div class="form-group">
            <label class="form-label">${t('field.name')} *</label>
            <input class="form-control" id="tAdminName" placeholder="${t('tenants.admin.namePlaceholder')}">
          </div>
          <div class="form-group">
            <label class="form-label">${t('field.email')} *</label>
            <input class="form-control" id="tAdminEmail" type="email" placeholder="${t('tenants.admin.emailPlaceholder')}">
          </div>
          <div class="form-group" style="grid-column:1/-1">
            <label class="form-label">${t('field.password')} *</label>
            <input class="form-control" id="tAdminPassword" type="password" placeholder="${t('tenants.admin.passwordPlaceholder')}">
          </div>
        </div>
      </div>
      ` : ''}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('tenantModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveTenant">${t('btn.save')}</button>
    `
  });
  openModal('tenantModal');

  document.getElementById('btnSaveTenant').addEventListener('click', async () => {
    const name = document.getElementById('tName').value.trim();
    const slug = document.getElementById('tSlug').value.trim();
    if (!name || !slug) { showToast(t('tenants.required'), 'error'); return; }

    try {
      if (tenant) {
        const body = {
          name,
          logoUrl: null,
          primaryColor: document.getElementById('tPrimary').value,
          secondaryColor: document.getElementById('tSecondary').value,
          customDomain: document.getElementById('tCustomDomain').value.trim() || null,
          isActive: document.getElementById('tActive').value === 'true',
        };
        await api.put(`/admin/tenants/${tenant.id}`, body);
        showToast(t('tenants.saved'), 'success');
      } else {
        const adminName = document.getElementById('tAdminName').value.trim();
        const adminEmail = document.getElementById('tAdminEmail').value.trim();
        const adminPassword = document.getElementById('tAdminPassword').value;
        if (!adminName || !adminEmail || !adminPassword) {
          showToast(t('tenants.admin.required'), 'error'); return;
        }
        const body = {
          name,
          slug,
          logoUrl: null,
          primaryColor: document.getElementById('tPrimary').value,
          secondaryColor: document.getElementById('tSecondary').value,
          plan: document.getElementById('tPlan').value,
          adminName,
          adminEmail,
          adminPassword,
        };
        await api.post('/admin/tenants', body);
        showToast(t('tenants.created'), 'success');
      }
      closeModal('tenantModal');
      await loadTenants();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
