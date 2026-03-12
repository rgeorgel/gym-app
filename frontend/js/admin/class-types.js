import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState } from '../ui.js';
import { t } from '../i18n.js';

export async function renderClassTypes(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewClassType">${t('classTypes.new')}</button>
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
      card.innerHTML = emptyState('🥊', t('classTypes.none'));
      return;
    }
    card.innerHTML = `
      <div class="table-wrapper">
        <table>
          <thead><tr><th>${t('field.color')}</th><th>${t('field.name')}</th><th>${t('classTypes.col.modality')}</th><th>${t('field.status')}</th><th></th></tr></thead>
          <tbody>
            ${types.map(ct => `
              <tr>
                <td><div style="width:20px;height:20px;background:${ct.color};border-radius:4px"></div></td>
                <td class="font-medium">${ct.name}</td>
                <td>${{ Group: t('classTypes.type.group'), Individual: t('classTypes.type.individual'), Pair: t('classTypes.type.pair') }[ct.modalityType] ?? ct.modalityType}</td>
                <td><span class="badge ${ct.isActive ? 'badge-success' : 'badge-gray'}">${ct.isActive ? t('status.active') : t('status.inactive')}</span></td>
                <td><button class="btn btn-secondary btn-sm" onclick="window._editClassType('${ct.id}')">${t('btn.edit')}</button></td>
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
    title: ct ? t('classTypes.title.edit') : t('classTypes.title.new'),
    body: `
      <div class="form-group">
        <label class="form-label">${t('field.name')} *</label>
        <input class="form-control" id="ctName" required value="${ct?.name ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">${t('field.description')}</label>
        <textarea class="form-control" id="ctDesc">${ct?.description ?? ''}</textarea>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">${t('field.color')}</label>
          <input class="form-control" id="ctColor" type="color" value="${ct?.color ?? '#3498db'}" style="height:42px;padding:2px 4px;cursor:pointer">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.type')}</label>
          <select class="form-control" id="ctModality">
            <option value="Group" ${ct?.modalityType==='Group'?'selected':''}>${t('classTypes.type.group')}</option>
            <option value="Individual" ${ct?.modalityType==='Individual'?'selected':''}>${t('classTypes.type.individual')}</option>
            <option value="Pair" ${ct?.modalityType==='Pair'?'selected':''}>${t('classTypes.type.pair')}</option>
          </select>
        </div>
      </div>
      ${ct ? `
      <div class="form-group">
        <label class="form-label">${t('field.status')}</label>
        <select class="form-control" id="ctActive">
          <option value="true" ${ct.isActive?'selected':''}>${t('status.active')}</option>
          <option value="false" ${!ct.isActive?'selected':''}>${t('status.inactive')}</option>
        </select>
      </div>` : ''}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('classTypeModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveCt">${t('btn.save')}</button>
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
      showToast(t('classTypes.saved'), 'success');
      closeModal('classTypeModal');
      await loadClassTypes();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
