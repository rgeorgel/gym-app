import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, formatDate, confirm } from '../ui.js';
import { t } from '../i18n.js';

const PLANS = { Basic: 'Basic', Pro: 'Pro', Enterprise: 'Enterprise' };
const TENANT_TYPES = { Gym: '🏋️ Academia', BeautySalon: '💅 Salão' };

export async function renderTenants(container) {
  container.innerHTML = `
    <div class="filters-bar" style="margin-bottom:1rem">
      <select class="form-control" id="filterTenantType" style="width:180px">
        <option value="">Todos os tipos</option>
        <option value="Gym">🏋️ Academia</option>
        <option value="BeautySalon">💅 Salão</option>
      </select>
      <button class="btn btn-primary" id="btnNewTenant" style="margin-left:auto">${t('tenants.new')}</button>
    </div>
    <div id="tenantsList"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  await loadTenants();

  document.getElementById('filterTenantType').addEventListener('change', loadTenants);
  document.getElementById('btnNewTenant').addEventListener('click', () => openTenantModal());
}

async function loadTenants() {
  const list = document.getElementById('tenantsList');
  const typeFilter = document.getElementById('filterTenantType')?.value ?? '';
  try {
    let tenants = await api.get('/admin/tenants');

    if (typeFilter) tenants = tenants.filter(ten => ten.tenantType === typeFilter);

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
                <th>Cliente</th>
                <th>Slug</th>
                <th>Tipo</th>
                <th>${t('tenants.col.plan')}</th>
                <th>${t('field.status')}</th>
                <th>${t('tenants.col.payments')}</th>
                <th>${t('tenants.col.createdAt')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              ${tenants.map(ten => `
                <tr>
                  <td class="font-medium">${ten.name}</td>
                  <td class="text-sm text-muted">${ten.slug}</td>
                  <td class="text-sm">${TENANT_TYPES[ten.tenantType] ?? ten.tenantType}</td>
                  <td class="text-sm">${PLANS[ten.plan] ?? ten.plan}</td>
                  <td><span class="badge ${ten.isActive ? 'badge-success' : 'badge-gray'}">${ten.isActive ? t('tenants.status.active') : t('tenants.status.inactive')}</span></td>
                  <td>
                    <span class="badge ${ten.paymentsAllowedBySuperAdmin ? 'badge-success' : 'badge-gray'}">${ten.paymentsAllowedBySuperAdmin ? t('tenants.payments.allowed') : t('tenants.payments.blocked')}</span>
                    ${ten.paymentsEnabled ? `<span class="badge badge-success" style="margin-left:0.3rem;font-size:0.65rem">${t('settings.payments.enabled')}</span>` : ''}
                  </td>
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

function buildPaymentsSection(tenant) {
  const efiHtml = tenant.efiPayeeCode
    ? `<span class="badge badge-success" style="font-size:0.75rem;font-family:monospace">${tenant.efiPayeeCode}</span>`
    : `<span class="text-sm text-muted">${t('settings.efi.notConfigured')}</span>`;

  return `
    <div class="form-group" style="grid-column:1/-1">
      <div style="border-top:1px solid var(--gray-200);padding-top:1rem;margin-top:0.25rem">
        <div class="text-sm font-medium" style="margin-bottom:0.75rem;color:var(--gray-600)">${t('tenants.payments.section')}</div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:0.75rem">
          <div>
            <div class="text-sm text-muted" style="margin-bottom:0.25rem">${t('tenants.payments.allowedBySuperAdmin')}</div>
            <span class="badge ${tenant.paymentsAllowedBySuperAdmin ? 'badge-success' : 'badge-gray'}">
              ${tenant.paymentsAllowedBySuperAdmin ? t('tenants.payments.allowed') : t('tenants.payments.blocked')}
            </span>
          </div>
          <div>
            <div class="text-sm text-muted" style="margin-bottom:0.25rem">${t('tenants.payments.enabledByTenant')}</div>
            <span class="badge ${tenant.paymentsEnabled ? 'badge-success' : 'badge-gray'}">
              ${tenant.paymentsEnabled ? t('settings.payments.enabled') : t('settings.payments.disabled')}
            </span>
          </div>
          <div style="grid-column:1/-1">
            <div class="text-sm text-muted" style="margin-bottom:0.25rem">${t('tenants.payments.efiCode')}</div>
            ${efiHtml}
          </div>
          <div style="grid-column:1/-1;margin-top:0.25rem">
            <label class="form-label">${t('tenants.payments.allowToggleLabel')}</label>
            <select class="form-control" id="tPaymentsAllowed">
              <option value="true" ${tenant.paymentsAllowedBySuperAdmin ? 'selected' : ''}>${t('tenants.payments.allow')}</option>
              <option value="false" ${!tenant.paymentsAllowedBySuperAdmin ? 'selected' : ''}>${t('tenants.payments.block')}</option>
            </select>
          </div>
        </div>
      </div>
    </div>
  `;
}

function openTenantModal(tenant = null) {
  const paymentsSection = tenant ? buildPaymentsSection(tenant) : '';

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
        <div class="form-group">
          <label class="form-label">Tipo de negócio</label>
          <select class="form-control" id="tTenantType">
            <option value="Gym" ${(tenant?.tenantType ?? 'Gym') === 'Gym' ? 'selected' : ''}>🏋️ Academia</option>
            <option value="BeautySalon" ${tenant?.tenantType === 'BeautySalon' ? 'selected' : ''}>💅 Salão de Beleza</option>
          </select>
        </div>
        ${tenant ? `
        <div class="form-group">
          <label class="form-label">Mensalidade (R$)</label>
          <input class="form-control" id="tPriceCents" type="number" min="0" step="0.01"
            value="${((tenant.subscriptionPriceCents ?? 4900) / 100).toFixed(2)}">
        </div>
        ` : ''}
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
        ${paymentsSection}
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
        const newAllowed = document.getElementById('tPaymentsAllowed').value === 'true';
        const newPriceCents = Math.round(parseFloat(document.getElementById('tPriceCents').value) * 100);
        const tasks = [
          api.put(`/admin/tenants/${tenant.id}`, {
            name,
            logoUrl: null,
            primaryColor: document.getElementById('tPrimary').value,
            secondaryColor: document.getElementById('tSecondary').value,
            customDomain: document.getElementById('tCustomDomain').value.trim() || null,
            isActive: document.getElementById('tActive').value === 'true',
            tenantType: document.getElementById('tTenantType').value,
          }),
        ];
        if (newAllowed !== tenant.paymentsAllowedBySuperAdmin) {
          tasks.push(api.put(`/admin/tenants/${tenant.id}/payments-allowed`, { allowed: newAllowed }));
        }
        if (!isNaN(newPriceCents) && newPriceCents !== tenant.subscriptionPriceCents) {
          tasks.push(api.put(`/admin/tenants/${tenant.id}/subscription-price`, { priceCents: newPriceCents }));
        }
        await Promise.all(tasks);
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
          tenantType: document.getElementById('tTenantType').value,
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
