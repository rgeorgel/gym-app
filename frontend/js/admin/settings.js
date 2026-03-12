import { api } from '../api.js';
import { showToast } from '../ui.js';
import { t } from '../i18n.js';

export async function renderSettings(container) {
  container.innerHTML = `<div class="loading-center"><span class="spinner"></span></div>`;

  let settings, templates;
  try {
    [settings, templates] = await Promise.all([
      api.get('/settings'),
      api.get('/package-templates'),
    ]);
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
    return;
  }

  const options = templates.map(tpl => {
    const selected = tpl.id === settings.defaultPackageTemplateId ? 'selected' : '';
    const duration = tpl.durationDays ? ` · ${tpl.durationDays} ${t('templates.days')}` : ` · ${t('templates.noValidity')}`;
    const credits = tpl.items.map(i => `${i.totalCredits}× ${i.classTypeName}`).join(', ');
    return `<option value="${tpl.id}" ${selected}>${tpl.name}${duration} — ${credits}</option>`;
  }).join('');

  container.innerHTML = `
    <div style="display:flex;flex-direction:column;gap:1.25rem;max-width:600px">
      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">${t('settings.defaultPkg.title')}</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">${t('settings.defaultPkg.desc')}</p>

          <div class="form-group">
            <label class="form-label">${t('settings.defaultPkg.field')}</label>
            <select class="form-control" id="selectDefaultTemplate">
              <option value="">${t('settings.defaultPkg.none')}</option>
              ${options}
            </select>
          </div>

          <div style="margin-top:1rem">
            <button class="btn btn-primary" id="btnSaveDefaultPkg">${t('btn.save')}</button>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">${t('settings.language.title')}</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">${t('settings.language.desc')}</p>

          <div class="form-group">
            <label class="form-label">${t('settings.language.field')}</label>
            <select class="form-control" id="selectLanguage">
              <option value="pt-BR" ${settings.language === 'pt-BR' ? 'selected' : ''}>${t('settings.language.ptBR')}</option>
              <option value="en-US" ${settings.language === 'en-US' ? 'selected' : ''}>${t('settings.language.enUS')}</option>
            </select>
          </div>

          <div style="margin-top:1rem">
            <button class="btn btn-primary" id="btnSaveLanguage">${t('btn.save')}</button>
          </div>
        </div>
      </div>
    </div>
  `;

  document.getElementById('btnSaveDefaultPkg').addEventListener('click', async () => {
    const val = document.getElementById('selectDefaultTemplate').value;
    const btn = document.getElementById('btnSaveDefaultPkg');
    btn.disabled = true;
    try {
      await api.put('/settings/default-package-template', { templateId: val || null });
      showToast(t('settings.saved'), 'success');
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('btnSaveLanguage').addEventListener('click', async () => {
    const lang = document.getElementById('selectLanguage').value;
    const btn = document.getElementById('btnSaveLanguage');
    btn.disabled = true;
    try {
      await api.put('/settings/language', { language: lang });
      showToast(t('settings.language.saved'), 'success');
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });
}
