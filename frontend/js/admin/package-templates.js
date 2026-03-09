import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, confirm } from '../ui.js';

export async function renderPackageTemplates(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewTemplate">+ Novo Modelo</button>
    </div>
    <div id="templatesList"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  await loadTemplates();

  document.getElementById('btnNewTemplate').addEventListener('click', () => openTemplateModal());
}

async function loadTemplates() {
  const list = document.getElementById('templatesList');
  try {
    const [templates, classTypes] = await Promise.all([
      api.get('/package-templates'),
      api.get('/class-types'),
    ]);

    if (!templates.length) {
      list.innerHTML = emptyState('📦', 'Nenhum modelo cadastrado');
      return;
    }

    list.innerHTML = templates.map(t => `
      <div class="card" style="margin-bottom:0.75rem">
        <div class="card-body" style="padding:1rem 1.25rem">
          <div style="display:flex;align-items:center;gap:1rem">
            <div style="flex:1">
              <div class="font-medium">${t.name}</div>
              <div class="text-sm text-muted" style="margin-top:0.2rem">
                ${t.durationDays ? `Validade: ${t.durationDays} dias` : 'Sem validade'}
              </div>
              <div style="display:flex;gap:0.5rem;flex-wrap:wrap;margin-top:0.5rem">
                ${t.items.map(i => `
                  <span style="display:inline-flex;align-items:center;gap:0.3rem;font-size:0.75rem;background:var(--gray-100);padding:0.2rem 0.5rem;border-radius:99px">
                    <span style="width:8px;height:8px;border-radius:50%;background:${i.classTypeColor};display:inline-block"></span>
                    ${i.classTypeName} · ${i.totalCredits} créditos · R$ ${Number(i.pricePerCredit).toFixed(2)}/aula
                  </span>
                `).join('')}
              </div>
            </div>
            <div style="display:flex;gap:0.5rem;flex-shrink:0">
              <button class="btn btn-secondary btn-sm" data-edit="${t.id}">Editar</button>
              <button class="btn btn-danger btn-sm" data-delete="${t.id}">Apagar</button>
            </div>
          </div>
        </div>
      </div>
    `).join('');

    list.querySelectorAll('[data-edit]').forEach(btn => {
      btn.addEventListener('click', () => openTemplateModal(templates.find(t => t.id === btn.dataset.edit), classTypes));
    });

    list.querySelectorAll('[data-delete]').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm('Apagar este modelo?')) return;
        try {
          await api.delete(`/package-templates/${btn.dataset.delete}`);
          showToast('Modelo apagado', 'success');
          await loadTemplates();
        } catch (e) {
          showToast('Erro: ' + e.message, 'error');
        }
      });
    });
  } catch (e) {
    list.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro: ${e.message}</div></div>`;
  }
}

async function openTemplateModal(template = null, classTypes = null) {
  if (!classTypes) {
    classTypes = await api.get('/class-types');
  }
  const activeClassTypes = classTypes.filter(ct => ct.isActive);

  const itemsForm = activeClassTypes.map(ct => {
    const existing = template?.items.find(i => i.classTypeId === ct.id);
    return `
      <div class="form-group" style="background:var(--gray-50);padding:0.75rem;border-radius:var(--border-radius);border:1px solid var(--gray-200)">
        <div style="display:flex;align-items:center;gap:0.5rem;margin-bottom:0.5rem">
          <div style="width:10px;height:10px;border-radius:2px;background:${ct.color}"></div>
          <label class="form-label" style="margin:0">${ct.name}</label>
        </div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:0.5rem">
          <div>
            <label class="form-label" style="font-size:0.7rem">Créditos</label>
            <input class="form-control" id="tpl_credits_${ct.id}" type="number" min="0" value="${existing?.totalCredits ?? 0}">
          </div>
          <div>
            <label class="form-label" style="font-size:0.7rem">R$/aula</label>
            <input class="form-control" id="tpl_price_${ct.id}" type="number" min="0" step="0.01" value="${existing?.pricePerCredit ?? 0}">
          </div>
        </div>
      </div>
    `;
  }).join('');

  createModal({
    id: 'templateModal',
    title: template ? 'Editar Modelo' : 'Novo Modelo',
    body: `
      <div class="form-group">
        <label class="form-label">Nome do modelo *</label>
        <input class="form-control" id="tplName" placeholder="ex: Plano Mensal Grupo" value="${template?.name ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">Validade padrão (dias)</label>
        <input class="form-control" id="tplDuration" type="number" min="1" placeholder="ex: 30 — deixe vazio para sem validade" value="${template?.durationDays ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label" style="margin-bottom:0.5rem">Créditos por modalidade</label>
        ${itemsForm}
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('templateModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveTemplate">Salvar</button>
    `
  });
  openModal('templateModal');

  document.getElementById('btnSaveTemplate').addEventListener('click', async () => {
    const name = document.getElementById('tplName').value.trim();
    if (!name) { showToast('Informe o nome do modelo', 'error'); return; }

    const durationDays = parseInt(document.getElementById('tplDuration').value) || null;

    const items = activeClassTypes.map(ct => ({
      classTypeId: ct.id,
      totalCredits: parseInt(document.getElementById(`tpl_credits_${ct.id}`).value) || 0,
      pricePerCredit: parseFloat(document.getElementById(`tpl_price_${ct.id}`).value) || 0,
    })).filter(i => i.totalCredits > 0);

    if (!items.length) { showToast('Adicione créditos para pelo menos uma modalidade', 'error'); return; }

    try {
      if (template) {
        // No PUT endpoint — delete and recreate
        await api.delete(`/package-templates/${template.id}`);
      }
      await api.post('/package-templates', { name, durationDays, items });
      showToast(template ? 'Modelo atualizado' : 'Modelo criado', 'success');
      closeModal('templateModal');
      await loadTemplates();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}
