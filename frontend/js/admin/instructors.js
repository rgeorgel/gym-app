import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, confirm } from '../ui.js';
import { t } from '../i18n.js';

export async function renderInstructors(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewInstructor">${t('instructors.new')}</button>
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
      list.innerHTML = emptyState('👤', t('instructors.none'));
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
                    <div class="font-medium">${i.name}</div>
                    ${i.bio ? `<div class="text-sm text-muted">${i.bio}</div>` : ''}
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
        if (!await confirm(t('instructors.remove.confirm'))) return;
        try {
          await api.delete(`/instructors/${btn.dataset.delete}`);
          showToast(t('instructors.removed'), 'success');
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
  createModal({
    id: 'instructorModal',
    title: instructor ? t('instructors.title.edit') : t('instructors.title.new'),
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
        <label class="form-label">${t('instructors.field.bio')}</label>
        <textarea class="form-control" id="iBio" rows="2">${instructor?.bio ?? ''}</textarea>
      </div>
      <div class="form-group">
        <label class="form-label">${t('instructors.field.specialties')}</label>
        <input class="form-control" id="iSpecialties" placeholder="${t('instructors.field.specialtiesPlaceholder')}" value="${instructor?.specialties ?? ''}">
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('instructorModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveInstructor">${t('btn.save')}</button>
    `
  });
  openModal('instructorModal');

  document.getElementById('btnSaveInstructor').addEventListener('click', async () => {
    const name = document.getElementById('iName').value.trim();
    const email = document.getElementById('iEmail').value.trim();
    if (!name || !email) { showToast(t('instructors.required'), 'error'); return; }

    const body = {
      name,
      phone: document.getElementById('iPhone').value.trim() || null,
      bio: document.getElementById('iBio').value.trim() || null,
      specialties: document.getElementById('iSpecialties').value.trim() || null,
    };

    try {
      if (instructor) {
        await api.put(`/instructors/${instructor.id}`, body);
        showToast(t('instructors.saved'), 'success');
      } else {
        await api.post('/instructors', { ...body, email });
        showToast(t('instructors.created'), 'success');
      }
      closeModal('instructorModal');
      await loadInstructors();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
