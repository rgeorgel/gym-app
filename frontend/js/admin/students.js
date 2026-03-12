import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, formatDate, statusBadge, emptyState, confirm } from '../ui.js';
import { t } from '../i18n.js';

let allStudents = [];

export async function renderStudents(container) {
  container.innerHTML = `
    <div class="filters-bar">
      <input type="text" id="studentSearch" class="search-input" placeholder="${t('students.search')}">
      <select id="studentStatus" class="form-control" style="width:auto">
        <option value="">${t('students.status.all')}</option>
        <option value="Active">${t('status.active')}</option>
        <option value="Inactive">${t('status.inactive')}</option>
        <option value="Suspended">${t('status.suspended')}</option>
      </select>
      <button class="btn btn-primary" id="btnNewStudent">${t('students.new')}</button>
    </div>
    <div class="card">
      <div class="table-wrapper">
        <table id="studentsTable">
          <thead>
            <tr>
              <th>${t('field.name')}</th><th>${t('field.email')}</th><th>${t('field.phone')}</th><th>${t('field.status')}</th><th>${t('students.col.registered')}</th><th></th>
            </tr>
          </thead>
          <tbody id="studentsTbody"><tr><td colspan="6"><div class="loading-center"><span class="spinner"></span></div></td></tr></tbody>
        </table>
      </div>
    </div>
  `;

  await loadStudents();

  document.getElementById('btnNewStudent').addEventListener('click', () => openStudentModal());
  document.getElementById('studentSearch').addEventListener('input', filterStudents);
  document.getElementById('studentStatus').addEventListener('change', filterStudents);
}

async function loadStudents() {
  try {
    allStudents = await api.get('/students');
    renderTable(allStudents);
  } catch (e) {
    showToast(t('error.prefix') + e.message, 'error');
  }
}

function filterStudents() {
  const q = document.getElementById('studentSearch').value.toLowerCase();
  const status = document.getElementById('studentStatus').value;
  const filtered = allStudents.filter(s =>
    (!q || s.name.toLowerCase().includes(q) || s.email.toLowerCase().includes(q)) &&
    (!status || s.status === status)
  );
  renderTable(filtered);
}

function renderTable(students) {
  const tbody = document.getElementById('studentsTbody');
  if (!tbody) return;
  if (!students.length) {
    tbody.innerHTML = `<tr><td colspan="6">${emptyState('👤', t('students.none'))}</td></tr>`;
    return;
  }
  tbody.innerHTML = students.map(s => `
    <tr>
      <td><div class="font-medium">${s.name}</div></td>
      <td class="text-sm text-muted">${s.email}</td>
      <td class="text-sm">${s.phone ?? '—'}</td>
      <td>${statusBadge(s.status)}</td>
      <td class="text-sm text-muted">${formatDate(s.createdAt)}</td>
      <td>
        <div class="flex gap-2">
          <button class="btn btn-secondary btn-sm" onclick="window._editStudent('${s.id}')">${t('btn.edit')}</button>
          <button class="btn btn-secondary btn-sm" onclick="window._viewPackages('${s.id}', '${s.name}')">${t('packages.label')}</button>
        </div>
      </td>
    </tr>
  `).join('');

  window._editStudent = (id) => openStudentModal(allStudents.find(s => s.id === id));
  window._viewPackages = (id, name) => openPackagesModal(id, name);
}

function openStudentModal(student = null) {
  const modal = createModal({
    id: 'studentModal',
    title: student ? t('students.title.edit') : t('students.title.new'),
    body: `
      <form id="studentForm">
        <div class="form-group">
          <label class="form-label">${t('field.name')} *</label>
          <input class="form-control" id="sName" required value="${student?.name ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.email')} *</label>
          <input class="form-control" id="sEmail" type="email" required value="${student?.email ?? ''}" ${student ? 'readonly' : ''}>
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.phone')}</label>
          <input class="form-control" id="sPhone" value="${student?.phone ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.birthDate')}</label>
          <input class="form-control" id="sBirth" type="date" value="${student?.birthDate?.split('T')[0] ?? ''}">
        </div>
        ${student ? `
        <div class="form-group">
          <label class="form-label">${t('field.status')}</label>
          <select class="form-control" id="sStatus">
            <option value="Active" ${student.status==='Active'?'selected':''}>${t('status.active')}</option>
            <option value="Inactive" ${student.status==='Inactive'?'selected':''}>${t('status.inactive')}</option>
            <option value="Suspended" ${student.status==='Suspended'?'selected':''}>${t('status.suspended')}</option>
          </select>
        </div>` : ''}
        <div class="form-group">
          <label class="form-label">${t('students.field.healthNotes')}</label>
          <textarea class="form-control" id="sHealth">${student?.healthNotes ?? ''}</textarea>
        </div>
        ${!student ? `
        <div class="form-group">
          <label class="form-label">${t('students.field.initialPassword')} *</label>
          <input class="form-control" id="sPassword" type="password" placeholder="${t('students.field.passwordPlaceholder')}" required>
        </div>` : ''}
      </form>
    `,
    footer: `
      ${student ? `<button class="btn btn-secondary" id="btnResetLink" style="margin-right:auto">${t('students.resetLink')}</button>` : ''}
      <button class="btn btn-secondary" onclick="closeModal('studentModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveStudent">${t('btn.save')}</button>
    `
  });

  openModal('studentModal');

  document.getElementById('btnSaveStudent').addEventListener('click', async () => {
    const body = {
      name: document.getElementById('sName').value.trim(),
      email: document.getElementById('sEmail').value.trim(),
      phone: document.getElementById('sPhone').value.trim() || null,
      birthDate: document.getElementById('sBirth').value || null,
      healthNotes: document.getElementById('sHealth').value.trim() || null,
    };
    if (student) {
      body.status = document.getElementById('sStatus').value;
    } else {
      const pw = document.getElementById('sPassword').value;
      if (!pw || pw.length < 6) { showToast(t('students.password.min'), 'error'); return; }
      body.password = pw;
    }

    try {
      if (student) await api.put(`/students/${student.id}`, body);
      else await api.post('/students', body);
      showToast(student ? t('students.saved') : t('students.created'), 'success');
      closeModal('studentModal');
      await loadStudents();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });

  document.getElementById('btnResetLink')?.addEventListener('click', async () => {
    try {
      const data = await api.post(`/students/${student.id}/reset-link`, {});
      const url = `${location.origin}/reset-password.html?token=${data.token}`;
      await navigator.clipboard.writeText(url);
      showToast(t('students.resetLink.copied'), 'success');
    } catch (e) {
      showToast(t('students.resetLink.error') + e.message, 'error');
    }
  });
}

async function openPackagesModal(studentId, studentName) {
  const modal = createModal({
    id: 'packagesModal',
    title: t('packages.modal.title', { name: studentName }),
    body: '<div class="loading-center"><span class="spinner"></span></div>',
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('packagesModal')">${t('btn.close')}</button>
      <button class="btn btn-primary" id="btnNewPackage">${t('packages.new')}</button>
    `
  });
  openModal('packagesModal');

  try {
    const [packages, classTypes, templates] = await Promise.all([
      api.get(`/students/${studentId}/packages`),
      api.get('/class-types'),
      api.get('/package-templates'),
    ]);

    const body = modal.querySelector('.modal-body');
    if (!packages.length) {
      body.innerHTML = emptyState('📦', t('packages.none.admin'));
    } else {
      body.innerHTML = packages.map(p => `
        <div class="package-card" style="margin-bottom:0.75rem">
          <div class="package-header">
            <span class="package-name">${p.name}</span>
            <span class="package-expiry">${p.expiresAt ? t('packages.expires') + new Date(p.expiresAt).toLocaleDateString() : t('packages.noExpiry')}</span>
            <button class="btn btn-danger btn-sm" data-delete-pkg="${p.id}" style="margin-left:auto">${t('btn.delete')}</button>
          </div>
          <div class="package-items">
            ${p.items.map(i => `
              <div class="credit-item">
                <div class="credit-color" style="background:${i.classTypeColor}"></div>
                <div class="credit-info">
                  <div class="credit-type">${i.classTypeName}</div>
                  <div class="credit-used">${t('packages.usedOf', { used: i.usedCredits, total: i.totalCredits })} · R$ ${i.pricePerCredit}${t('packages.pricePerClass')}</div>
                </div>
                <div class="credit-remaining">${i.remainingCredits}</div>
              </div>
            `).join('')}
          </div>
        </div>
      `).join('');

      body.querySelectorAll('[data-delete-pkg]').forEach(btn => {
        btn.addEventListener('click', async () => {
          if (!await confirm(t('packages.delete.confirm'))) return;
          try {
            await api.delete(`/packages/${btn.dataset.deletePkg}`);
            showToast(t('packages.deleted'), 'success');
            closeModal('packagesModal');
            openPackagesModal(studentId, studentName);
          } catch (e) {
            showToast(t('error.prefix') + e.message, 'error');
          }
        });
      });
    }

    document.getElementById('btnNewPackage').addEventListener('click', () => openNewPackageModal(studentId, classTypes, templates, async () => {
      closeModal('packagesModal');
      openPackagesModal(studentId, studentName);
    }));
  } catch (e) {
    showToast('Erro ao carregar pacotes: ' + e.message, 'error');
  }
}

function openNewPackageModal(studentId, classTypes, templates, onSuccess) {
  const activeClassTypes = classTypes.filter(ct => ct.isActive);

  const templateOptions = templates.length
    ? templates.map(tpl => `<option value="${tpl.id}">${tpl.name}${tpl.durationDays ? ` · ${tpl.durationDays}d` : ''}</option>`).join('')
    : `<option disabled>${t('packages.noTemplates')}</option>`;

  const itemsForm = activeClassTypes.map(ct => `
    <div class="form-group" style="background:var(--gray-50);padding:0.75rem;border-radius:var(--border-radius);border:1px solid var(--gray-200)">
      <div style="display:flex;align-items:center;gap:0.5rem;margin-bottom:0.5rem">
        <div style="width:10px;height:10px;border-radius:2px;background:${ct.color}"></div>
        <label class="form-label" style="margin:0">${ct.name}</label>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:0.5rem">
        <div>
          <label class="form-label" style="font-size:0.7rem">${t('packages.field.credits')}</label>
          <input class="form-control" id="credits_${ct.id}" type="number" min="0" value="0">
        </div>
        <div>
          <label class="form-label" style="font-size:0.7rem">${t('packages.field.pricePerClass')}</label>
          <input class="form-control" id="price_${ct.id}" type="number" min="0" step="0.01" value="0">
        </div>
      </div>
    </div>
  `).join('');

  createModal({
    id: 'newPackageModal',
    title: t('packages.modal.new'),
    body: `
      <div style="display:flex;gap:0.5rem;margin-bottom:1.25rem">
        <button class="btn btn-primary" id="tabTemplate" style="flex:1">${t('packages.tab.template')}</button>
        <button class="btn btn-secondary" id="tabCustom" style="flex:1">${t('packages.tab.custom')}</button>
      </div>

      <div id="sectionTemplate">
        <div class="form-group">
          <label class="form-label">${t('packages.field.template')}</label>
          <select class="form-control" id="templateSelect">${templateOptions}</select>
        </div>
        <div class="form-group">
          <label class="form-label">${t('packages.field.expiryOverride')}</label>
          <input class="form-control" id="templateExpiry" type="date">
        </div>
      </div>

      <div id="sectionCustom" class="hidden">
        <div class="form-group">
          <label class="form-label">${t('packages.field.name')}</label>
          <input class="form-control" id="pkgName" placeholder="${t('packages.namePlaceholder')}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('packages.field.expiry')}</label>
          <input class="form-control" id="pkgExpiry" type="date">
        </div>
        ${itemsForm}
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('newPackageModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSavePkg">${t('packages.modal.new')}</button>
    `
  });
  openModal('newPackageModal');

  // Tab switching
  const tabTemplate = document.getElementById('tabTemplate');
  const tabCustom = document.getElementById('tabCustom');
  const sectionTemplate = document.getElementById('sectionTemplate');
  const sectionCustom = document.getElementById('sectionCustom');

  tabTemplate.addEventListener('click', () => {
    tabTemplate.className = 'btn btn-primary';
    tabCustom.className = 'btn btn-secondary';
    sectionTemplate.classList.remove('hidden');
    sectionCustom.classList.add('hidden');
  });
  tabCustom.addEventListener('click', () => {
    tabCustom.className = 'btn btn-primary';
    tabTemplate.className = 'btn btn-secondary';
    sectionCustom.classList.remove('hidden');
    sectionTemplate.classList.add('hidden');
  });

  document.getElementById('btnSavePkg').addEventListener('click', async () => {
    const isTemplate = !sectionTemplate.classList.contains('hidden');
    try {
      if (isTemplate) {
        const templateId = document.getElementById('templateSelect').value;
        if (!templateId) { showToast(t('packages.select.required'), 'error'); return; }
        const expiry = document.getElementById('templateExpiry').value || null;
        await api.post(`/package-templates/${templateId}/assign`, { studentId, expiresAt: expiry });
      } else {
        const items = activeClassTypes.map(ct => ({
          classTypeId: ct.id,
          totalCredits: parseInt(document.getElementById(`credits_${ct.id}`).value) || 0,
          pricePerCredit: parseFloat(document.getElementById(`price_${ct.id}`).value) || 0,
        })).filter(i => i.totalCredits > 0);
        if (!items.length) { showToast(t('packages.addCredits'), 'error'); return; }
        await api.post('/packages', {
          studentId,
          name: document.getElementById('pkgName').value.trim() || 'Pacote',
          expiresAt: document.getElementById('pkgExpiry').value || null,
          items
        });
      }
      showToast(t('packages.created'), 'success');
      closeModal('newPackageModal');
      await onSuccess();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
