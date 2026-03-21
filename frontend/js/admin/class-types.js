import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState } from '../ui.js';
import { t } from '../i18n.js';
import { tenantType } from '../tenant.js';

export async function renderClassTypes(container) {
  const isSalon = tenantType === 'BeautySalon';
  const newLabel = isSalon ? '+ Novo Serviço' : t('classTypes.new');

  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;gap:0.5rem;margin-bottom:1rem">
      ${isSalon ? '<button class="btn btn-secondary" id="btnManageCategories">Categorias</button>' : ''}
      <button class="btn btn-primary" id="btnNewClassType">${newLabel}</button>
    </div>
    <div class="card" id="classTypesCard">
      <div class="loading-center"><span class="spinner"></span></div>
    </div>
  `;

  document.getElementById('btnNewClassType').addEventListener('click', () => openClassTypeModal());
  if (isSalon) {
    document.getElementById('btnManageCategories').addEventListener('click', () => openCategoriesModal());
  }
  await loadClassTypes();
}

async function loadClassTypes() {
  const isSalon = tenantType === 'BeautySalon';
  const card = document.getElementById('classTypesCard');
  try {
    const types = await api.get('/class-types');
    if (!types.length) {
      card.innerHTML = emptyState(isSalon ? '💅' : '🥊', isSalon ? 'Nenhum serviço cadastrado' : t('classTypes.none'));
      return;
    }
    card.innerHTML = `
      <div class="table-wrapper">
        <table>
          <thead><tr>
            <th>${t('field.color')}</th>
            <th>${isSalon ? 'Serviço' : t('field.name')}</th>
            ${isSalon ? '<th>Categoria</th>' : `<th>${t('classTypes.col.modality')}</th>`}
            <th>${t('classTypes.col.price')}</th>
            <th>${t('field.status')}</th>
            <th></th>
          </tr></thead>
          <tbody>
            ${types.map(ct => `
              <tr>
                <td><div style="width:20px;height:20px;background:${ct.color};border-radius:4px"></div></td>
                <td class="font-medium">${ct.name}</td>
                ${isSalon
                  ? `<td style="color:var(--text-muted);font-size:0.85rem">${ct.categoryName ?? '—'}</td>`
                  : `<td>${{ Group: t('classTypes.type.group'), Individual: t('classTypes.type.individual'), Pair: t('classTypes.type.pair') }[ct.modalityType] ?? ct.modalityType}</td>`}
                <td>${ct.price != null ? `R$ ${Number(ct.price).toFixed(2).replace('.', ',')}` : '—'}</td>
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

async function openClassTypeModal(ct = null) {
  const isSalon = tenantType === 'BeautySalon';
  const titleNew = isSalon ? 'Novo Serviço' : t('classTypes.title.new');
  const titleEdit = isSalon ? 'Editar Serviço' : t('classTypes.title.edit');

  let categories = [];
  if (isSalon) {
    try { categories = await api.get('/service-categories'); } catch { /* ignore */ }
  }

  const categoryOptions = categories
    .filter(c => c.isActive)
    .map(c => `<option value="${c.id}" ${ct?.categoryId === c.id ? 'selected' : ''}>${c.name}</option>`)
    .join('');

  createModal({
    id: 'classTypeModal',
    title: ct ? titleEdit : titleNew,
    body: `
      <div class="form-group">
        <label class="form-label">${isSalon ? 'Nome do serviço' : t('field.name')} *</label>
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
        ${isSalon ? `
        <div class="form-group">
          <label class="form-label">Categoria</label>
          <select class="form-control" id="ctCategory">
            <option value="">Sem categoria</option>
            ${categoryOptions}
          </select>
        </div>` : `
        <div class="form-group">
          <label class="form-label">${t('field.type')}</label>
          <select class="form-control" id="ctModality">
            <option value="Group" ${ct?.modalityType==='Group'?'selected':''}>${t('classTypes.type.group')}</option>
            <option value="Individual" ${ct?.modalityType==='Individual'?'selected':''}>${t('classTypes.type.individual')}</option>
            <option value="Pair" ${ct?.modalityType==='Pair'?'selected':''}>${t('classTypes.type.pair')}</option>
          </select>
        </div>`}
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">${t('field.price')}</label>
          <input class="form-control" id="ctPrice" type="number" min="0" step="0.01" placeholder="ex: 35.00" value="${ct?.price ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.durationMinutes')}</label>
          <input class="form-control" id="ctDuration" type="number" min="5" step="5" placeholder="ex: 30" value="${ct?.durationMinutes ?? ''}">
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
    const priceVal = document.getElementById('ctPrice').value.trim();
    const durationVal = document.getElementById('ctDuration').value.trim();
    const catVal = isSalon ? document.getElementById('ctCategory').value : null;
    const body = {
      name: document.getElementById('ctName').value.trim(),
      description: document.getElementById('ctDesc').value.trim() || null,
      color: document.getElementById('ctColor').value,
      modalityType: isSalon ? 'Individual' : document.getElementById('ctModality').value,
      price: priceVal !== '' ? parseFloat(priceVal) : null,
      durationMinutes: durationVal !== '' ? parseInt(durationVal) : null,
      categoryId: catVal || null,
    };
    if (ct) body.isActive = document.getElementById('ctActive').value === 'true';

    try {
      if (ct) await api.put(`/class-types/${ct.id}`, body);
      else await api.post('/class-types', body);
      showToast(isSalon ? 'Serviço salvo' : t('classTypes.saved'), 'success');
      closeModal('classTypeModal');
      await loadClassTypes();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}

async function openCategoriesModal() {
  createModal({
    id: 'categoriesModal',
    title: 'Categorias de Serviços',
    body: '<div id="categoriesModalBody"><div class="loading-center"><span class="spinner"></span></div></div>',
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('categoriesModal')">Fechar</button>
      <button class="btn btn-primary" id="btnAddCategory">+ Nova Categoria</button>
    `
  });
  openModal('categoriesModal');
  await loadCategoriesInModal();
  document.getElementById('btnAddCategory').addEventListener('click', () => openCategoryForm());
}

async function loadCategoriesInModal() {
  const body = document.getElementById('categoriesModalBody');
  if (!body) return;
  try {
    const cats = await api.get('/service-categories');
    if (!cats.length) {
      body.innerHTML = '<p style="color:var(--text-muted);font-size:0.9rem;padding:0.5rem 0">Nenhuma categoria cadastrada.</p>';
      return;
    }
    body.innerHTML = `
      <div class="table-wrapper">
        <table>
          <thead><tr><th>Nome</th><th>Ordem</th><th>Status</th><th></th></tr></thead>
          <tbody>
            ${cats.map(c => `
              <tr>
                <td class="font-medium">${c.name}</td>
                <td style="color:var(--text-muted)">${c.sortOrder}</td>
                <td><span class="badge ${c.isActive ? 'badge-success' : 'badge-gray'}">${c.isActive ? 'Ativa' : 'Inativa'}</span></td>
                <td>
                  <button class="btn btn-secondary btn-sm" onclick="window._editCategory('${c.id}')">Editar</button>
                  <button class="btn btn-danger btn-sm" style="margin-left:0.25rem" onclick="window._deleteCategory('${c.id}')">Excluir</button>
                </td>
              </tr>
            `).join('')}
          </tbody>
        </table>
      </div>
    `;
    window._editCategory = (id) => openCategoryForm(cats.find(c => c.id === id));
    window._deleteCategory = async (id) => {
      if (!confirm('Excluir esta categoria? Os serviços vinculados ficarão sem categoria.')) return;
      try {
        await api.delete(`/service-categories/${id}`);
        showToast('Categoria excluída', 'success');
        await loadCategoriesInModal();
      } catch (e) {
        showToast('Erro: ' + e.message, 'error');
      }
    };
  } catch (e) {
    body.innerHTML = `<p style="color:var(--text-danger)">${e.message}</p>`;
  }
}

function openCategoryForm(cat = null) {
  createModal({
    id: 'categoryFormModal',
    title: cat ? 'Editar Categoria' : 'Nova Categoria',
    body: `
      <div class="form-group">
        <label class="form-label">Nome *</label>
        <input class="form-control" id="catName" value="${cat?.name ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">Ordem de exibição</label>
        <input class="form-control" id="catOrder" type="number" min="0" value="${cat?.sortOrder ?? 0}">
      </div>
      ${cat ? `
      <div class="form-group">
        <label class="form-label">Status</label>
        <select class="form-control" id="catActive">
          <option value="true" ${cat.isActive?'selected':''}>Ativa</option>
          <option value="false" ${!cat.isActive?'selected':''}>Inativa</option>
        </select>
      </div>` : ''}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('categoryFormModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveCat">Salvar</button>
    `
  });
  openModal('categoryFormModal');

  document.getElementById('btnSaveCat').addEventListener('click', async () => {
    const body = {
      name: document.getElementById('catName').value.trim(),
      sortOrder: parseInt(document.getElementById('catOrder').value) || 0,
    };
    if (cat) body.isActive = document.getElementById('catActive').value === 'true';

    try {
      if (cat) await api.put(`/service-categories/${cat.id}`, body);
      else await api.post('/service-categories', body);
      showToast('Categoria salva', 'success');
      closeModal('categoryFormModal');
      await loadCategoriesInModal();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}
