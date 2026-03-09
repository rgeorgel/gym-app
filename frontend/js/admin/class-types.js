import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState } from '../ui.js';

export async function renderClassTypes(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewClassType">+ Nova Modalidade</button>
    </div>
    <div class="card" id="classTypesCard">
      <div class="loading-center"><span class="spinner"></span></div>
    </div>
  `;

  document.getElementById('btnNewClassType').addEventListener('click', () => openClassTypeModal());
  await loadClassTypes();
}

async function loadClassTypes() {
  const card = document.getElementById('classTypesCard');
  try {
    const types = await api.get('/class-types');
    if (!types.length) {
      card.innerHTML = emptyState('🥊', 'Nenhuma modalidade cadastrada');
      return;
    }
    card.innerHTML = `
      <div class="table-wrapper">
        <table>
          <thead><tr><th>Cor</th><th>Nome</th><th>Modalidade</th><th>Status</th><th></th></tr></thead>
          <tbody>
            ${types.map(ct => `
              <tr>
                <td><div style="width:20px;height:20px;background:${ct.color};border-radius:4px"></div></td>
                <td class="font-medium">${ct.name}</td>
                <td>${{ Group: 'Grupo', Individual: 'Individual', Pair: 'Dupla' }[ct.modalityType] ?? ct.modalityType}</td>
                <td><span class="badge ${ct.isActive ? 'badge-success' : 'badge-gray'}">${ct.isActive ? 'Ativo' : 'Inativo'}</span></td>
                <td><button class="btn btn-secondary btn-sm" onclick="window._editClassType('${ct.id}')">Editar</button></td>
              </tr>
            `).join('')}
          </tbody>
        </table>
      </div>
    `;
    window._editClassType = (id) => openClassTypeModal(types.find(ct => ct.id === id));
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

function openClassTypeModal(ct = null) {
  createModal({
    id: 'classTypeModal',
    title: ct ? 'Editar Modalidade' : 'Nova Modalidade',
    body: `
      <div class="form-group">
        <label class="form-label">Nome *</label>
        <input class="form-control" id="ctName" required value="${ct?.name ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">Descrição</label>
        <textarea class="form-control" id="ctDesc">${ct?.description ?? ''}</textarea>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">Cor</label>
          <input class="form-control" id="ctColor" type="color" value="${ct?.color ?? '#3498db'}" style="height:42px;padding:2px 4px;cursor:pointer">
        </div>
        <div class="form-group">
          <label class="form-label">Tipo</label>
          <select class="form-control" id="ctModality">
            <option value="Group" ${ct?.modalityType==='Group'?'selected':''}>Grupo</option>
            <option value="Individual" ${ct?.modalityType==='Individual'?'selected':''}>Individual</option>
            <option value="Pair" ${ct?.modalityType==='Pair'?'selected':''}>Dupla</option>
          </select>
        </div>
      </div>
      ${ct ? `
      <div class="form-group">
        <label class="form-label">Status</label>
        <select class="form-control" id="ctActive">
          <option value="true" ${ct.isActive?'selected':''}>Ativo</option>
          <option value="false" ${!ct.isActive?'selected':''}>Inativo</option>
        </select>
      </div>` : ''}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('classTypeModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveCt">Salvar</button>
    `
  });
  openModal('classTypeModal');

  document.getElementById('btnSaveCt').addEventListener('click', async () => {
    const body = {
      name: document.getElementById('ctName').value.trim(),
      description: document.getElementById('ctDesc').value.trim() || null,
      color: document.getElementById('ctColor').value,
      modalityType: document.getElementById('ctModality').value,
    };
    if (ct) body.isActive = document.getElementById('ctActive').value === 'true';

    try {
      if (ct) await api.put(`/class-types/${ct.id}`, body);
      else await api.post('/class-types', body);
      showToast('Modalidade salva', 'success');
      closeModal('classTypeModal');
      await loadClassTypes();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}
