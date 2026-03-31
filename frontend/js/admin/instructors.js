import { api } from '../api.js?v=202603311200';
import { showToast, createModal, openModal, closeModal, emptyState, confirm } from '../ui.js?v=202603311200';
import { t } from '../i18n.js?v=202603311200';
import { tenantType } from '../tenant.js?v=202603311200';

const isSalon = () => tenantType === 'BeautySalon';

const lbl = {
  new:            () => isSalon() ? '+ Novo Profissional'    : t('instructors.new'),
  none:           () => isSalon() ? 'Nenhum profissional cadastrado' : t('instructors.none'),
  titleNew:       () => isSalon() ? 'Novo Profissional'      : t('instructors.title.new'),
  titleEdit:      () => isSalon() ? 'Editar Profissional'    : t('instructors.title.edit'),
  removed:        () => isSalon() ? 'Profissional removido'  : t('instructors.removed'),
  removeConfirm:  () => isSalon() ? 'Remover este profissional?' : t('instructors.remove.confirm'),
  saved:          () => isSalon() ? 'Profissional atualizado': t('instructors.saved'),
  created:        () => isSalon() ? 'Profissional cadastrado': t('instructors.created'),
  specialtiesPlaceholder: () => isSalon() ? 'ex: Coloração, Corte' : t('instructors.field.specialtiesPlaceholder'),
};

export async function renderInstructors(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewInstructor">${lbl.new()}</button>
    </div>
    <div id="instructorsList"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  await loadInstructors();

  document.getElementById('btnNewInstructor').addEventListener('click', () => openInstructorModal());
}

async function loadInstructors() {
  const list = document.getElementById('instructorsList');
  try {
    const instructors = await api.get('/instructors');

    if (!instructors.length) {
      list.innerHTML = emptyState('👤', lbl.none());
      return;
    }

    list.innerHTML = `
      <div class="card">
        <div class="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>${t('field.name')}</th>
                <th>${t('field.email')}</th>
                <th>${t('field.phone')}</th>
                <th>${t('instructors.col.specialties')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              ${instructors.map(i => `
                <tr>
                  <td>
                    <div style="display:flex;align-items:center;gap:0.6rem">
                      ${i.photoUrl
                        ? `<img src="${i.photoUrl}" style="width:32px;height:32px;border-radius:50%;object-fit:cover;flex-shrink:0" onerror="this.style.display='none'">`
                        : `<span style="width:32px;height:32px;border-radius:50%;background:var(--gray-200);display:inline-flex;align-items:center;justify-content:center;font-size:0.95rem;flex-shrink:0">${isSalon() ? '✂️' : '👤'}</span>`}
                      <div>
                        <div class="font-medium">${i.name}</div>
                        ${i.bio ? `<div class="text-sm text-muted">${i.bio}</div>` : ''}
                      </div>
                    </div>
                  </td>
                  <td class="text-sm text-muted">${i.email}</td>
                  <td class="text-sm">${i.phone ?? '—'}</td>
                  <td class="text-sm">${i.specialties ?? '—'}</td>
                  <td>
                    <div class="flex gap-2">
                      <button class="btn btn-secondary btn-sm" data-edit="${i.id}">${t('btn.edit')}</button>
                      <button class="btn btn-danger btn-sm" data-delete="${i.id}">${t('btn.remove')}</button>
                    </div>
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      </div>
    `;

    list.querySelectorAll('[data-edit]').forEach(btn => {
      btn.addEventListener('click', () =>
        openInstructorModal(instructors.find(i => i.id === btn.dataset.edit))
      );
    });

    list.querySelectorAll('[data-delete]').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm(lbl.removeConfirm())) return;
        try {
          await api.delete(`/instructors/${btn.dataset.delete}`);
          showToast(lbl.removed(), 'success');
          await loadInstructors();
        } catch (e) {
          showToast(t('error.prefix') + e.message, 'error');
        }
      });
    });
  } catch (e) {
    list.innerHTML = `<div class="empty-state"><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
  }
}

function openInstructorModal(instructor = null) {
  const salon = isSalon();
  createModal({
    id: 'instructorModal',
    title: instructor ? lbl.titleEdit() : lbl.titleNew(),
    body: `
      <div class="form-group">
        <label class="form-label">${t('field.name')} *</label>
        <input class="form-control" id="iName" value="${instructor?.name ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">${t('field.email')} *</label>
        <input class="form-control" id="iEmail" type="email" value="${instructor?.email ?? ''}" ${instructor ? 'readonly' : ''}>
      </div>
      <div class="form-group">
        <label class="form-label">${t('field.phone')}</label>
        <input class="form-control" id="iPhone" value="${instructor?.phone ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">${salon ? 'Foto (URL da imagem)' : t('instructors.field.bio')}</label>
        ${salon
          ? `<input class="form-control" id="iPhotoUrl" type="url" placeholder="https://..." value="${instructor?.photoUrl ?? ''}">
             ${instructor?.photoUrl ? `<img src="${instructor.photoUrl}" id="iPhotoPreview" style="margin-top:0.5rem;width:56px;height:56px;border-radius:50%;object-fit:cover" onerror="this.style.display='none'">` : ''}`
          : `<textarea class="form-control" id="iBio" rows="2">${instructor?.bio ?? ''}</textarea>`}
      </div>
      ${salon ? `
      <div class="form-group">
        <label class="form-label">${t('instructors.field.bio')}</label>
        <textarea class="form-control" id="iBio" rows="2">${instructor?.bio ?? ''}</textarea>
      </div>` : ''}
      <div class="form-group">
        <label class="form-label">${t('instructors.field.specialties')}</label>
        <input class="form-control" id="iSpecialties" placeholder="${lbl.specialtiesPlaceholder()}" value="${instructor?.specialties ?? ''}">
      </div>
      ${salon ? `
      <div class="form-group" id="iServicesContainer">
        <label class="form-label">Serviços que atende</label>
        <div class="loading-center" style="justify-content:flex-start"><span class="spinner" style="width:16px;height:16px"></span></div>
      </div>` : ''}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('instructorModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveInstructor">${t('btn.save')}</button>
    `
  });
  openModal('instructorModal');

  // Live preview of photo URL
  document.getElementById('iPhotoUrl')?.addEventListener('input', e => {
    const url = e.target.value.trim();
    let preview = document.getElementById('iPhotoPreview');
    if (!preview && url) {
      preview = document.createElement('img');
      preview.id = 'iPhotoPreview';
      preview.style.cssText = 'margin-top:0.5rem;width:56px;height:56px;border-radius:50%;object-fit:cover';
      preview.onerror = () => preview.style.display = 'none';
      e.target.insertAdjacentElement('afterend', preview);
    }
    if (preview) { preview.src = url; preview.style.display = url ? '' : 'none'; }
  });

  // Load service checkboxes (salon only)
  if (salon) {
    api.get('/class-types').then(services => {
      const el = document.getElementById('iServicesContainer');
      if (!el) return;
      const active = services.filter(s => s.isActive);
      if (!active.length) { el.style.display = 'none'; return; }

      const currentIds = new Set(instructor?.serviceIds ?? []);
      const allChecked = currentIds.size === 0;

      el.innerHTML = `
        <label class="form-label">Serviços que atende</label>
        <label style="display:flex;align-items:center;gap:0.5rem;margin-bottom:0.75rem;cursor:pointer;font-weight:500">
          <input type="checkbox" id="iSvcAll" ${allChecked ? 'checked' : ''}>
          <span>Todos os serviços (padrão)</span>
        </label>
        <div id="iSvcList" style="${allChecked ? 'display:none' : 'display:grid;gap:0.4rem'}">
          ${active.map(s => `
            <label style="display:flex;align-items:center;gap:0.5rem;cursor:pointer;font-size:0.9rem">
              <input type="checkbox" class="iSvcCheck" value="${s.id}" ${currentIds.has(s.id) ? 'checked' : ''}>
              <span style="width:10px;height:10px;border-radius:50%;background:${s.color ?? '#888'};display:inline-block;flex-shrink:0"></span>
              <span>${s.name}</span>
            </label>
          `).join('')}
        </div>
      `;

      document.getElementById('iSvcAll').addEventListener('change', e => {
        document.getElementById('iSvcList').style.display = e.target.checked ? 'none' : 'grid';
        if (e.target.checked)
          document.querySelectorAll('.iSvcCheck').forEach(cb => cb.checked = false);
      });
    }).catch(() => {
      const el = document.getElementById('iServicesContainer');
      if (el) el.style.display = 'none';
    });
  }

  document.getElementById('btnSaveInstructor').addEventListener('click', async () => {
    const name = document.getElementById('iName').value.trim();
    const email = document.getElementById('iEmail').value.trim();
    if (!name || !email) { showToast(t('instructors.required'), 'error'); return; }

    const body = {
      name,
      phone: document.getElementById('iPhone').value.trim() || null,
      bio: document.getElementById('iBio')?.value.trim() || null,
      specialties: document.getElementById('iSpecialties').value.trim() || null,
      photoUrl: document.getElementById('iPhotoUrl')?.value.trim() || null,
    };

    try {
      let savedId;
      if (instructor) {
        await api.put(`/instructors/${instructor.id}`, body);
        savedId = instructor.id;
        showToast(lbl.saved(), 'success');
      } else {
        const created = await api.post('/instructors', { ...body, email });
        savedId = created.id;
        showToast(lbl.created(), 'success');
      }

      // Save service associations (salon only)
      if (salon && savedId) {
        const allSvc = document.getElementById('iSvcAll')?.checked ?? true;
        const serviceIds = allSvc
          ? []
          : [...document.querySelectorAll('.iSvcCheck:checked')].map(cb => cb.value);
        await api.put(`/instructors/${savedId}/services`, { serviceIds }).catch(() => {});
      }

      closeModal('instructorModal');
      await loadInstructors();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
