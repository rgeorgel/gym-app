import { api } from '../api.js?v=202603311200';
import { showToast, createModal, openModal, closeModal, formatDate, statusBadge, emptyState, confirm, applyPhoneMask } from '../ui.js?v=202603311200';
import { t } from '../i18n.js?v=202603311200';
import { renderStudentDetail } from './student-detail.js?v=202603311200';
import { tenantType } from '../tenant.js?v=202603311200';

let allStudents = [];
let sortField = 'name';
let sortDir = 'asc';
let activeQuickFilter = 'all';

let studentsContainer = null;

export async function renderStudents(container, openStudentId = null) {
  studentsContainer = container;
  const isGym = tenantType !== 'BeautySalon';

  container.innerHTML = `
    <div class="filters-bar">
      <input type="text" id="studentSearch" class="search-input" placeholder="${t('students.search')}">
      <select id="studentStatus" class="form-control" style="width:auto">
        <option value="">${t('students.status.all')}</option>
        <option value="Active">${t('status.active')}</option>
        <option value="Inactive">${t('status.inactive')}</option>
        <option value="Suspended">${t('status.suspended')}</option>
      </select>
      <select id="studentQuickFilter" class="form-control" style="width:auto">
        <option value="all">${t('students.filter.all')}</option>
        ${isGym ? `<option value="noCredits">${t('students.filter.noCredits')}</option>` : ''}
        <option value="noBooking7">${t('students.filter.noBooking7')}</option>
        <option value="noBooking15">${t('students.filter.noBooking15')}</option>
        <option value="noBooking30">${t('students.filter.noBooking30')}</option>
        <option value="noBooking60">${t('students.filter.noBooking60')}</option>
        <option value="neverBooked">${t('students.filter.neverBooked')}</option>
        <option value="suspended">${t('students.filter.suspended')}</option>
      </select>
      <select id="studentSort" class="form-control" style="width:auto">
        <option value="name-asc">${t('field.name')} A→Z</option>
        <option value="name-desc">${t('field.name')} Z→A</option>
        ${isGym ? `
        <option value="credits-desc">${t('students.col.credits')} ↓</option>
        <option value="credits-asc">${t('students.col.credits')} ↑</option>
        ` : ''}
        <option value="lastBooking-desc">${t('students.col.lastBooking')} ↓</option>
        <option value="createdAt-desc">${t('students.col.registered')} ↓</option>
      </select>
      <button class="btn btn-primary" id="btnNewStudent">${t('students.new')}</button>
    </div>
    <div id="studentsListContainer"></div>
  `;

  await loadStudents();

  if (openStudentId) {
    renderStudentDetail(studentsContainer, openStudentId, () => {
      history.replaceState(null, '', '#students');
      renderStudents(studentsContainer);
    });
    return;
  }

  document.getElementById('btnNewStudent').addEventListener('click', () => openStudentModal());
  document.getElementById('studentSearch').addEventListener('input', applyFilters);
  document.getElementById('studentStatus').addEventListener('change', applyFilters);
  document.getElementById('studentQuickFilter').addEventListener('change', (e) => {
    activeQuickFilter = e.target.value;
    applyFilters();
  });
  document.getElementById('studentSort').addEventListener('change', (e) => {
    const [field, dir] = e.target.value.split('-');
    sortField = field;
    sortDir = dir;
    applyFilters();
  });
}

async function loadStudents() {
  try {
    allStudents = await api.get('/students');
    applyFilters();
  } catch (e) {
    showToast(t('error.prefix') + e.message, 'error');
  }
}

function applyFilters() {
  const q = document.getElementById('studentSearch')?.value.toLowerCase() ?? '';
  const status = document.getElementById('studentStatus')?.value ?? '';
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  let filtered = allStudents.filter(s => {
    if (q && !s.name.toLowerCase().includes(q) && !s.email.toLowerCase().includes(q)) return false;
    if (status && s.status !== status) return false;

    switch (activeQuickFilter) {
      case 'noCredits':   return s.totalRemainingCredits === 0;
      case 'noBooking7':  return daysSinceLastBooking(s) >= 7;
      case 'noBooking15': return daysSinceLastBooking(s) >= 15;
      case 'noBooking30': return daysSinceLastBooking(s) >= 30;
      case 'noBooking60': return daysSinceLastBooking(s) >= 60;
      case 'neverBooked': return !s.lastBookingDate;
      case 'suspended':   return s.status === 'Suspended';
    }
    return true;
  });

  filtered = sortStudents(filtered);
  renderList(filtered);
}

function daysSinceLastBooking(s) {
  if (!s.lastBookingDate) return Infinity;
  const last = new Date(s.lastBookingDate);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.floor((today - last) / 86400000);
}

function sortStudents(list) {
  return [...list].sort((a, b) => {
    let va, vb;
    switch (sortField) {
      case 'name':    va = a.name.toLowerCase(); vb = b.name.toLowerCase(); break;
      case 'email':   va = a.email.toLowerCase(); vb = b.email.toLowerCase(); break;
      case 'status':  va = a.status; vb = b.status; break;
      case 'credits': va = a.totalRemainingCredits; vb = b.totalRemainingCredits; break;
      case 'lastBooking':
        va = a.lastBookingDate ? new Date(a.lastBookingDate).getTime() : 0;
        vb = b.lastBookingDate ? new Date(b.lastBookingDate).getTime() : 0;
        break;
      case 'createdAt':
        va = new Date(a.createdAt).getTime();
        vb = new Date(b.createdAt).getTime();
        break;
      default: return 0;
    }
    if (va < vb) return sortDir === 'asc' ? -1 : 1;
    if (va > vb) return sortDir === 'asc' ? 1 : -1;
    return 0;
  });
}

function renderList(students) {
  const container = document.getElementById('studentsListContainer');
  if (!container) return;

  const isGym = tenantType !== 'BeautySalon';

  if (!students.length) {
    container.innerHTML = `<div class="card card-body">${emptyState('👤', t('students.none'))}</div>`;
    return;
  }

  container.innerHTML = `<div class="student-list">${students.map(s => {
    const creditsHtml = isGym
      ? (s.totalRemainingCredits > 0
          ? `<span class="badge badge-success">${s.totalRemainingCredits} cred.</span>`
          : `<span class="badge badge-gray">0 cred.</span>`)
      : '';
    const lastBooking = s.lastBookingDate
      ? formatDate(s.lastBookingDate)
      : t('students.lastBooking.never');

    return `
      <div class="student-card">
        <div class="student-card-main">
          <div class="student-card-info">
            <div class="student-card-name">${s.name}</div>
            <div class="student-card-contact">${s.email}${s.phone ? ` · ${s.phone}` : ''}</div>
          </div>
          <div class="student-card-badges">
            ${statusBadge(s.status)}
            ${creditsHtml}
          </div>
        </div>
        <div class="student-card-footer">
          <span class="student-card-lastbooking">📅 ${lastBooking}</span>
          <div class="student-card-actions">
            <button class="btn btn-secondary btn-sm btn-edit-student" data-id="${s.id}">${t('btn.edit')}</button>
            ${isGym ? `<button class="btn btn-secondary btn-sm btn-view-packages" data-id="${s.id}" data-name="${s.name}">${t('packages.label')}</button>` : ''}
            <button class="btn btn-primary btn-sm btn-view-detail" data-id="${s.id}">Ver detalhes →</button>
          </div>
        </div>
      </div>
    `;
  }).join('')}</div>`;

  container.querySelectorAll('.btn-edit-student').forEach(btn => {
    btn.addEventListener('click', () => openStudentModal(allStudents.find(s => s.id === btn.dataset.id)));
  });
  container.querySelectorAll('.btn-view-packages').forEach(btn => {
    btn.addEventListener('click', () => openPackagesModal(btn.dataset.id, btn.dataset.name));
  });
  container.querySelectorAll('.btn-view-detail').forEach(btn => {
    btn.addEventListener('click', () => {
      history.replaceState(null, '', '#students/' + btn.dataset.id);
      renderStudentDetail(studentsContainer, btn.dataset.id, () => {
        history.replaceState(null, '', '#students');
        renderStudents(studentsContainer);
      });
    });
  });
}

function openStudentModal(student = null) {
  createModal({
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
  applyPhoneMask(document.getElementById('sPhone'));

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
            <span class="package-expiry">${p.expiresAt ? t('packages.expires') + formatDate(p.expiresAt) : t('packages.noExpiry')}</span>
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
