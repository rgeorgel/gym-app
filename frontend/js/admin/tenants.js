import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, formatDate, confirm } from '../ui.js';

const PLANS = { Basic: 'Basic', Pro: 'Pro', Enterprise: 'Enterprise' };
const PLAN_LABELS = { Basic: 'Basic', Pro: 'Pro', Enterprise: 'Enterprise' };

export async function renderTenants(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewTenant">+ Nova Academia</button>
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
      list.innerHTML = emptyState('🏢', 'Nenhuma academia cadastrada');
      return;
    }

    list.innerHTML = `
      <div class="card">
        <div class="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Nome</th>
                <th>Slug</th>
                <th>Plano</th>
                <th>Status</th>
                <th>Criado em</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              ${tenants.map(t => `
                <tr>
                  <td class="font-medium">${t.name}</td>
                  <td class="text-sm text-muted">${t.slug}</td>
                  <td class="text-sm">${PLAN_LABELS[t.plan] ?? t.plan}</td>
                  <td><span class="badge ${t.isActive ? 'badge-success' : 'badge-gray'}">${t.isActive ? 'Ativa' : 'Inativa'}</span></td>
                  <td class="text-sm text-muted">${formatDate(t.createdAt)}</td>
                  <td>
                    <button class="btn btn-secondary btn-sm" data-edit="${t.id}">Editar</button>
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      </div>
    `;

    list.querySelectorAll('[data-edit]').forEach(btn => {
      btn.addEventListener('click', () => openTenantModal(tenants.find(t => t.id === btn.dataset.edit)));
    });
  } catch (e) {
    list.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro: ${e.message}</div></div>`;
  }
}

function openTenantModal(tenant = null) {
  createModal({
    id: 'tenantModal',
    title: tenant ? 'Editar Academia' : 'Nova Academia',
    body: `
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group" style="grid-column:1/-1">
          <label class="form-label">Nome *</label>
          <input class="form-control" id="tName" value="${tenant?.name ?? ''}">
        </div>
        ${!tenant ? `
        <div class="form-group" style="grid-column:1/-1">
          <label class="form-label">Slug * <span class="text-muted" style="font-weight:400;font-size:0.75rem">(usado na URL e login)</span></label>
          <input class="form-control" id="tSlug" placeholder="ex: boxe-elite" value="">
        </div>
        ` : `
        <div class="form-group" style="grid-column:1/-1">
          <label class="form-label">Slug</label>
          <input class="form-control" id="tSlug" value="${tenant.slug}" readonly style="background:var(--gray-50)">
        </div>
        `}
        <div class="form-group">
          <label class="form-label">Cor primária</label>
          <input class="form-control" id="tPrimary" type="color" value="${tenant?.primaryColor ?? '#1a1a2e'}" style="height:42px;padding:2px 4px;cursor:pointer">
        </div>
        <div class="form-group">
          <label class="form-label">Cor secundária</label>
          <input class="form-control" id="tSecondary" type="color" value="${tenant?.secondaryColor ?? '#e94560'}" style="height:42px;padding:2px 4px;cursor:pointer">
        </div>
        <div class="form-group">
          <label class="form-label">Plano</label>
          <select class="form-control" id="tPlan" ${tenant ? 'disabled' : ''}>
            ${Object.entries(PLANS).map(([v, l]) => `<option value="${v}" ${tenant?.plan === v ? 'selected' : ''}>${l}</option>`).join('')}
          </select>
        </div>
        ${tenant ? `
        <div class="form-group" style="grid-column:1/-1">
          <label class="form-label">Domínio personalizado <span class="text-muted" style="font-weight:400;font-size:0.75rem">(deixe vazio para usar subdomínio padrão)</span></label>
          <input class="form-control" id="tCustomDomain" placeholder="app.boxeelite.com.br" value="${tenant.customDomain ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">Status</label>
          <select class="form-control" id="tActive">
            <option value="true" ${tenant.isActive ? 'selected' : ''}>Ativa</option>
            <option value="false" ${!tenant.isActive ? 'selected' : ''}>Inativa</option>
          </select>
        </div>
        ` : ''}
      </div>
      ${!tenant ? `
      <div style="border-top:1px solid var(--gray-200);margin-top:0.5rem;padding-top:1rem">
        <div class="text-sm font-medium" style="margin-bottom:0.75rem;color:var(--gray-600)">Administrador da academia</div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
          <div class="form-group">
            <label class="form-label">Nome *</label>
            <input class="form-control" id="tAdminName" placeholder="Nome completo">
          </div>
          <div class="form-group">
            <label class="form-label">E-mail *</label>
            <input class="form-control" id="tAdminEmail" type="email" placeholder="admin@academia.com">
          </div>
          <div class="form-group" style="grid-column:1/-1">
            <label class="form-label">Senha *</label>
            <input class="form-control" id="tAdminPassword" type="password" placeholder="Mínimo 6 caracteres">
          </div>
        </div>
      </div>
      ` : ''}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('tenantModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveTenant">Salvar</button>
    `
  });
  openModal('tenantModal');

  document.getElementById('btnSaveTenant').addEventListener('click', async () => {
    const name = document.getElementById('tName').value.trim();
    const slug = document.getElementById('tSlug').value.trim();
    if (!name || !slug) { showToast('Nome e slug são obrigatórios', 'error'); return; }

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
        showToast('Academia atualizada', 'success');
      } else {
        const adminName = document.getElementById('tAdminName').value.trim();
        const adminEmail = document.getElementById('tAdminEmail').value.trim();
        const adminPassword = document.getElementById('tAdminPassword').value;
        if (!adminName || !adminEmail || !adminPassword) {
          showToast('Preencha os dados do administrador', 'error'); return;
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
        showToast('Academia criada', 'success');
      }
      closeModal('tenantModal');
      await loadTenants();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}
