import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, confirm } from '../ui.js';
import { t } from '../i18n.js';

export async function renderPackageTemplates(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewTemplate">${t('templates.new')}</button>
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
      list.innerHTML = emptyState('📦', t('templates.none'));
      return;
    }

    list.innerHTML = templates.map(tpl => `
      <div class="card" style="margin-bottom:0.75rem">
        <div class="card-body" style="padding:1rem 1.25rem">
          <div style="display:flex;align-items:center;gap:1rem">
            <div style="flex:1">
              <div class="font-medium">${tpl.name}</div>
              <div class="text-sm text-muted" style="margin-top:0.2rem">
                ${tpl.durationDays ? `${t('templates.validity')} ${tpl.durationDays} ${t('templates.days')}` : t('templates.noValidity')}
              </div>
              <div style="display:flex;gap:0.5rem;flex-wrap:wrap;margin-top:0.5rem">
                ${tpl.items.map(i => `
                  <span style="display:inline-flex;align-items:center;gap:0.3rem;font-size:0.75rem;background:var(--gray-100);padding:0.2rem 0.5rem;border-radius:99px">
                    <span style="width:8px;height:8px;border-radius:50%;background:${i.classTypeColor};display:inline-block"></span>
                    ${i.classTypeName} · ${i.totalCredits} ${t('templates.credits')} · R$ ${Number(i.pricePerCredit).toFixed(2)}${t('templates.pricePerClass')}
                  </span>
                `).join('')}
              </div>
            </div>
            <div style="display:flex;gap:0.5rem;flex-shrink:0">
              <button class="btn btn-secondary btn-sm" data-edit="${tpl.id}">${t('btn.edit')}</button>
              <button class="btn btn-danger btn-sm" data-delete="${tpl.id}">${t('btn.delete')}</button>
            </div>
          </div>
        </div>
      </div>
    `).join('');

    list.querySelectorAll('[data-edit]').forEach(btn => {
      btn.addEventListener('click', () => openTemplateModal(templates.find(tpl => tpl.id === btn.dataset.edit), classTypes));
    });

    list.querySelectorAll('[data-delete]').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm(t('templates.delete.confirm'))) return;
        try {
          await api.delete(`/package-templates/${btn.dataset.delete}`);
          showToast(t('templates.deleted'), 'success');
          await loadTemplates();
        } catch (e) {
          showToast(t('error.prefix') + e.message, 'error');
        }
      });
    });
  } catch (e) {
    list.innerHTML = `<div class="empty-state"><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
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
            <label class="form-label" style="font-size:0.7rem">${t('templates.credits')}</label>
            <input class="form-control" id="tpl_credits_${ct.id}" type="number" min="0" value="${existing?.totalCredits ?? 0}">
          </div>
          <div>
            <label class="form-label" style="font-size:0.7rem">${t('packages.field.pricePerClass')}</label>
            <input class="form-control" id="tpl_price_${ct.id}" type="number" min="0" step="0.01" value="${existing?.pricePerCredit ?? 0}">
          </div>
        </div>
      </div>
    `;
  }).join('');

  createModal({
    id: 'templateModal',
    title: template ? t('templates.title.edit') : t('templates.title.new'),
    body: `
      <div class="form-group">
        <label class="form-label">${t('templates.field.name')}</label>
        <input class="form-control" id="tplName" value="${template?.name ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label">${t('templates.field.duration')}</label>
        <input class="form-control" id="tplDuration" type="number" min="1" placeholder="${t('templates.field.durationPlaceholder')}" value="${template?.durationDays ?? ''}">
      </div>
      <div class="form-group">
        <label class="form-label" style="margin-bottom:0.5rem">${t('templates.field.credits')}</label>
        ${itemsForm}
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('templateModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveTemplate">${t('btn.save')}</button>
    `
  });
  openModal('templateModal');

  document.getElementById('btnSaveTemplate').addEventListener('click', async () => {
    const name = document.getElementById('tplName').value.trim();
    if (!name) { showToast(t('templates.name.required'), 'error'); return; }

    const durationDays = parseInt(document.getElementById('tplDuration').value) || null;

    const items = activeClassTypes.map(ct => ({
      classTypeId: ct.id,
      totalCredits: parseInt(document.getElementById(`tpl_credits_${ct.id}`).value) || 0,
      pricePerCredit: parseFloat(document.getElementById(`tpl_price_${ct.id}`).value) || 0,
    })).filter(i => i.totalCredits > 0);

    if (!items.length) { showToast(t('templates.credits.required'), 'error'); return; }

    try {
      if (template) {
        await api.delete(`/package-templates/${template.id}`);
      }
      await api.post('/package-templates', { name, durationDays, items });
      showToast(template ? t('templates.updated') : t('templates.created'), 'success');
      closeModal('templateModal');
      await loadTemplates();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
