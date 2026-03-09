import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, confirm } from '../ui.js';

export async function renderInstructors(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewInstructor">+ Novo Instrutor</button>
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
      list.innerHTML = emptyState('👤', 'Nenhum instrutor cadastrado');
      return;
    }

    list.innerHTML = `
      <div class="card">
        <div class="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Nome</th>
                <th>E-mail</th>
                <th>Telefone</th>
                <th>Especialidades</th>
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
                      <button class="btn btn-secondary btn-sm" data-edit="${i.id}">Editar</button>
                      <button class="btn btn-danger btn-sm" data-delete="${i.id}">Remover</button>
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
        if (!await confirm('Remover este instrutor?')) return;
        try {
          await api.delete(`/instructors/${btn.dataset.delete}`);
          showToast('Instrutor removido', 'success');
          await loadInstructors();
        } catch (e) {
          showToast('Erro: ' + e.message, 'error');
        }
      });
    });
  } catch (e) {
    list.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro: ${e.message}</div></div>`;
  }
}

function openInstructorModal(instructor = null) {
  createModal({
    id: 'instructorModal',
    title: instructor ? 'Editar Instrutor' : 'Novo Instrutor',
    body: `
      <div class="form-group">
        <label class="form-label">Nome *</label>
        <input class="form-control" id="iName" value="${instructor?.name ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">E-mail *</label>
        <input class="form-control" id="iEmail" type="email" value="${instructor?.email ?? ''}" ${instructor ? 'readonly' : ''}>
      </div>
      <div class="form-group">
        <label class="form-label">Telefone</label>
        <input class="form-control" id="iPhone" value="${instructor?.phone ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">Bio</label>
        <textarea class="form-control" id="iBio" rows="2">${instructor?.bio ?? ''}</textarea>
      </div>
      <div class="form-group">
        <label class="form-label">Especialidades</label>
        <input class="form-control" id="iSpecialties" placeholder="ex: Boxe, Muay Thai" value="${instructor?.specialties ?? ''}">
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('instructorModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveInstructor">Salvar</button>
    `
  });
  openModal('instructorModal');

  document.getElementById('btnSaveInstructor').addEventListener('click', async () => {
    const name = document.getElementById('iName').value.trim();
    const email = document.getElementById('iEmail').value.trim();
    if (!name || !email) { showToast('Nome e e-mail são obrigatórios', 'error'); return; }

    const body = {
      name,
      phone: document.getElementById('iPhone').value.trim() || null,
      bio: document.getElementById('iBio').value.trim() || null,
      specialties: document.getElementById('iSpecialties').value.trim() || null,
    };

    try {
      if (instructor) {
        await api.put(`/instructors/${instructor.id}`, body);
        showToast('Instrutor atualizado', 'success');
      } else {
        await api.post('/instructors', { ...body, email });
        showToast('Instrutor cadastrado', 'success');
      }
      closeModal('instructorModal');
      await loadInstructors();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}
