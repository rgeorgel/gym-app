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

  const efiStatus = settings.efiPayeeCode
    ? `<span class="badge badge-success" style="font-size:0.75rem">${settings.efiPayeeCode}</span>`
    : `<span class="text-muted text-sm">${t('settings.efi.notConfigured')}</span>`;

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
              <option value="es-ES" ${settings.language === 'es-ES' ? 'selected' : ''}>${t('settings.language.esES')}</option>
            </select>
          </div>

          <div style="margin-top:1rem">
            <button class="btn btn-primary" id="btnSaveLanguage">${t('btn.save')}</button>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">${t('settings.payments.title')}</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">${t('settings.payments.desc')}</p>
          ${settings.paymentsAllowedBySuperAdmin === false
            ? `<span class="badge badge-gray">${t('settings.payments.blockedBySuperAdmin')}</span>`
            : settings.paymentsEnabled
              ? `<div style="display:flex;align-items:center;gap:1rem">
                   <span class="badge badge-success">${t('settings.payments.enabled')}</span>
                   <button class="btn btn-secondary btn-sm" id="btnTogglePayments">${t('settings.payments.disable')}</button>
                 </div>`
              : `<div style="display:flex;align-items:center;gap:1rem">
                   <span class="badge badge-gray">${t('settings.payments.disabled')}</span>
                   <button class="btn btn-primary btn-sm" id="btnTogglePayments">${t('settings.payments.enable')}</button>
                 </div>`
          }
        </div>
      </div>

      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">${t('settings.colors.title')}</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">${t('settings.colors.desc')}</p>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
            <div class="form-group" style="margin:0">
              <label class="form-label">${t('settings.colors.primary')}</label>
              <div style="display:flex;align-items:center;gap:0.5rem">
                <input type="color" id="inputPrimaryColor" value="${settings.primaryColor}" style="width:48px;height:36px;padding:2px;border:1px solid var(--gray-300);border-radius:var(--border-radius);cursor:pointer">
                <input class="form-control" id="inputPrimaryHex" value="${settings.primaryColor}" maxlength="7" style="font-family:monospace">
              </div>
            </div>
            <div class="form-group" style="margin:0">
              <label class="form-label">${t('settings.colors.secondary')}</label>
              <div style="display:flex;align-items:center;gap:0.5rem">
                <input type="color" id="inputSecondaryColor" value="${settings.secondaryColor}" style="width:48px;height:36px;padding:2px;border:1px solid var(--gray-300);border-radius:var(--border-radius);cursor:pointer">
                <input class="form-control" id="inputSecondaryHex" value="${settings.secondaryColor}" maxlength="7" style="font-family:monospace">
              </div>
            </div>
          </div>
          <div style="margin-top:1rem">
            <button class="btn btn-primary" id="btnSaveColors">${t('btn.save')}</button>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">${t('settings.efi.title')}</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">${t('settings.efi.desc')}</p>
          <div style="margin-bottom:0.75rem">${efiStatus}</div>
          <div class="form-group">
            <label class="form-label">${t('settings.efi.field')}</label>
            <input class="form-control" id="inputEfiPayeeCode" placeholder="${t('settings.efi.placeholder')}"
              value="${settings.efiPayeeCode ?? ''}">
          </div>
          <div style="margin-top:1rem">
            <button class="btn btn-primary" id="btnSaveEfi">${t('btn.save')}</button>
          </div>
        </div>
      </div>
    </div>
  `;

  // Sync color picker ↔ hex input
  document.getElementById('inputPrimaryColor').addEventListener('input', (e) => {
    document.getElementById('inputPrimaryHex').value = e.target.value;
  });
  document.getElementById('inputPrimaryHex').addEventListener('input', (e) => {
    if (/^#[0-9a-fA-F]{6}$/.test(e.target.value))
      document.getElementById('inputPrimaryColor').value = e.target.value;
  });
  document.getElementById('inputSecondaryColor').addEventListener('input', (e) => {
    document.getElementById('inputSecondaryHex').value = e.target.value;
  });
  document.getElementById('inputSecondaryHex').addEventListener('input', (e) => {
    if (/^#[0-9a-fA-F]{6}$/.test(e.target.value))
      document.getElementById('inputSecondaryColor').value = e.target.value;
  });

  document.getElementById('btnSaveColors').addEventListener('click', async () => {
    const primary = document.getElementById('inputPrimaryHex').value.trim();
    const secondary = document.getElementById('inputSecondaryHex').value.trim();
    if (!/^#[0-9a-fA-F]{6}$/.test(primary) || !/^#[0-9a-fA-F]{6}$/.test(secondary)) {
      showToast(t('error.prefix') + 'Cor inválida (use formato #RRGGBB)', 'error');
      return;
    }
    const btn = document.getElementById('btnSaveColors');
    btn.disabled = true;
    try {
      await api.put('/settings/colors', { primaryColor: primary, secondaryColor: secondary });
      showToast(t('settings.colors.saved'), 'success');
      // Apply immediately to current page
      document.documentElement.style.setProperty('--brand-primary', primary);
      document.documentElement.style.setProperty('--brand-secondary', secondary);
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('btnTogglePayments')?.addEventListener('click', async () => {
    const btn = document.getElementById('btnTogglePayments');
    btn.disabled = true;
    try {
      await api.put('/settings/payments', { enabled: !settings.paymentsEnabled });
      showToast(t('settings.payments.saved'), 'success');
      await renderSettings(container);
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
      btn.disabled = false;
    }
  });

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

  document.getElementById('btnSaveEfi').addEventListener('click', async () => {
    const val = document.getElementById('inputEfiPayeeCode').value.trim();
    const btn = document.getElementById('btnSaveEfi');
    btn.disabled = true;
    try {
      await api.put('/settings/efi-payee-code', { payeeCode: val || null });
      showToast(t('settings.efi.saved'), 'success');
      await renderSettings(container);
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
