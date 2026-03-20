import { api } from '../api.js';
import { showToast } from '../ui.js';
import { t } from '../i18n.js';
import { getUser } from '../auth.js';

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

  // Build catalog URL from current hostname
  const slug = localStorage.getItem('tenant_slug') ?? '';
  const host = location.hostname;
  const parts = host.split('.');
  const catalogUrl = parts.length >= 3
    ? `${location.protocol}//${host}/catalog/`
    : `${location.protocol}//${host}/catalog/?slug=${slug}`;

  container.innerHTML = `
    <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,480px),1fr));gap:1.25rem;align-items:start">
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
          <h3 style="margin:0 0 0.25rem">${t('settings.logo.title')}</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">${t('settings.logo.desc')}</p>
          <div class="form-group">
            <label class="form-label">${t('settings.logo.field')}</label>
            <input class="form-control" id="inputLogoUrl" type="url"
              placeholder="${t('settings.logo.placeholder')}"
              value="${settings.logoUrl ?? ''}">
          </div>
          ${settings.logoUrl ? `<img src="${settings.logoUrl}" alt="Logo preview" style="margin-top:0.75rem;max-height:64px;max-width:200px;object-fit:contain;border-radius:var(--border-radius);border:1px solid var(--gray-200)" id="logoPreview">` : ''}
          <div style="margin-top:1rem">
            <button class="btn btn-primary" id="btnSaveLogo">${t('btn.save')}</button>
          </div>
        </div>
      </div>

      ${settings.tenantType === 'BeautySalon' ? `
      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">Link de Agendamento</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">
            Compartilhe este link com suas clientes. Elas poderão ver seus serviços e agendar diretamente.
          </p>
          <div style="display:flex;align-items:center;gap:0.5rem">
            <input class="form-control" id="inputCatalogUrl" value="${catalogUrl}" readonly
              style="font-size:0.8rem;font-family:monospace;background:var(--gray-50);cursor:text">
            <button class="btn btn-primary btn-sm" id="btnCopyCatalog" style="white-space:nowrap">Copiar link</button>
            <a href="${catalogUrl}" target="_blank" class="btn btn-secondary btn-sm" style="white-space:nowrap">Abrir</a>
          </div>
        </div>
      </div>
      ` : ''}

      ${getUser()?.role === 'SuperAdmin' ? `
      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">${t('settings.tenantType.title')}</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">${t('settings.tenantType.desc')}</p>
          <div class="form-group">
            <label class="form-label">${t('settings.tenantType.field')}</label>
            <select class="form-control" id="selectTenantType">
              <option value="Gym" ${settings.tenantType === 'Gym' || !settings.tenantType ? 'selected' : ''}>${t('settings.tenantType.gym')}</option>
              <option value="BeautySalon" ${settings.tenantType === 'BeautySalon' ? 'selected' : ''}>${t('settings.tenantType.beautySalon')}</option>
            </select>
          </div>
          <div style="margin-top:1rem">
            <button class="btn btn-primary" id="btnSaveTenantType">${t('btn.save')}</button>
          </div>
        </div>
      </div>` : ''}

      <div class="card" style="grid-column:1/-1">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">${t('settings.abacatepay.title')}</h3>
          <p class="text-muted text-sm" style="margin:0 0 1rem">${t('settings.abacatepay.desc')}</p>

          <div class="form-group">
            <label class="form-label">${t('settings.abacatepay.field')}</label>
            <div style="display:flex;align-items:center;gap:0.5rem">
              <input class="form-control" id="inputAbacatePayKey" type="password"
                placeholder="${settings.hasAbacatePayStudentApiKey ? t('settings.abacatepay.alreadySet') : t('settings.abacatepay.placeholder')}">
              <button class="btn btn-primary btn-sm" id="btnSaveAbacatePay">${t('btn.save')}</button>
            </div>
          </div>

          <div class="form-group" style="margin-top:1rem">
            <label class="form-label">${t('settings.abacatepay.webhookSecret.field')}</label>
            <div style="display:flex;align-items:center;gap:0.5rem">
              <input class="form-control" id="inputAbacatePayWebhookSecret" type="password"
                placeholder="${settings.hasAbacatePayStudentWebhookSecret ? t('settings.abacatepay.alreadySet') : t('settings.abacatepay.webhookSecret.placeholder')}">
              <button class="btn btn-primary btn-sm" id="btnSaveAbacatePayWebhook">${t('btn.save')}</button>
            </div>
            <p class="text-sm text-muted" style="margin-top:0.25rem">${t('settings.abacatepay.webhookSecret.hint')}</p>
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

  document.getElementById('btnSaveAbacatePay').addEventListener('click', async () => {
    const val = document.getElementById('inputAbacatePayKey').value.trim();
    if (!val) { showToast(t('error.prefix') + t('settings.abacatepay.required'), 'error'); return; }
    const btn = document.getElementById('btnSaveAbacatePay');
    btn.disabled = true;
    try {
      await api.put('/settings/abacatepay-student-api-key', { apiKey: val });
      showToast(t('settings.abacatepay.saved'), 'success');
      await renderSettings(container);
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('btnSaveAbacatePayWebhook').addEventListener('click', async () => {
    const val = document.getElementById('inputAbacatePayWebhookSecret').value.trim();
    if (!val) { showToast(t('error.prefix') + t('settings.abacatepay.required'), 'error'); return; }
    const btn = document.getElementById('btnSaveAbacatePayWebhook');
    btn.disabled = true;
    try {
      await api.put('/settings/abacatepay-student-webhook-secret', { secret: val });
      showToast(t('settings.abacatepay.webhookSecret.saved'), 'success');
      await renderSettings(container);
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('inputLogoUrl').addEventListener('input', (e) => {
    const preview = document.getElementById('logoPreview');
    const url = e.target.value.trim();
    if (url && preview) {
      preview.src = url;
    } else if (url) {
      const img = document.createElement('img');
      img.id = 'logoPreview';
      img.alt = 'Logo preview';
      img.src = url;
      img.style.cssText = 'margin-top:0.75rem;max-height:64px;max-width:200px;object-fit:contain;border-radius:var(--border-radius);border:1px solid var(--gray-200)';
      e.target.closest('.card-body').insertBefore(img, document.getElementById('btnSaveLogo').parentElement);
    } else if (preview) {
      preview.remove();
    }
  });

  document.getElementById('btnSaveLogo').addEventListener('click', async () => {
    const url = document.getElementById('inputLogoUrl').value.trim();
    const btn = document.getElementById('btnSaveLogo');
    btn.disabled = true;
    try {
      await api.put('/settings/logo', { logoUrl: url || null });
      showToast(t('settings.logo.saved'), 'success');
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('btnSaveTenantType')?.addEventListener('click', async () => {
    const val = document.getElementById('selectTenantType').value;
    const btn = document.getElementById('btnSaveTenantType');
    btn.disabled = true;
    try {
      await api.put('/settings/tenant-type', { tenantType: val });
      showToast(t('settings.tenantType.saved'), 'success');
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('btnCopyCatalog')?.addEventListener('click', () => {
    navigator.clipboard.writeText(catalogUrl).then(() => {
      const btn = document.getElementById('btnCopyCatalog');
      btn.textContent = 'Copiado!';
      setTimeout(() => { btn.textContent = 'Copiar link'; }, 2000);
    });
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
