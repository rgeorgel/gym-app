/**
 * Gerador de Panfletos — editor.js
 * 100% client-side. Para usuários Agendofy: importa via JWT existente.
 */

const API_BASE = '/api';

// ── State ────────────────────────────────────────────────────────────────────
const state = {
  templateId: 'rosa-delicado',
  logoDataUrl: null,
  name: '',
  tagline: '',
  phone: '',
  services: [
    { name: 'Esmaltação simples', price: 'R$ 25' },
    { name: 'Gel Francês',        price: 'R$ 70' },
    { name: 'Manicure completa',  price: 'R$ 45' },
  ],
  instagram: '',
  tiktok: '',
  website: '',
};

// ── DOM refs ─────────────────────────────────────────────────────────────────
const canvas          = document.getElementById('panfletoCanvas');
const inputLogo       = document.getElementById('inputLogo');
const logoUploadArea  = document.getElementById('logoUploadArea');
const logoPreviewImg  = document.getElementById('logoPreviewImg');
const logoPlaceholder = document.getElementById('logoPlaceholder');
const btnRemoveLogo   = document.getElementById('btnRemoveLogo');
const inputName       = document.getElementById('inputName');
const inputTagline    = document.getElementById('inputTagline');
const inputPhone      = document.getElementById('inputPhone');
const servicesList    = document.getElementById('servicesList');
const btnAddService   = document.getElementById('btnAddService');
const inputInstagram  = document.getElementById('inputInstagram');
const inputTikTok     = document.getElementById('inputTikTok');
const inputWebsite    = document.getElementById('inputWebsite');
const btnExport       = document.getElementById('btnExport');
const btnExportLabel  = document.getElementById('btnExportLabel');
const btnExportSpinner= document.getElementById('btnExportSpinner');
const templateStrip   = document.getElementById('templateStrip');
const importModal     = document.getElementById('importModal');
const btnImport       = document.getElementById('btnImportAgendofy');
const btnDoImport     = document.getElementById('btnDoImport');
const btnCancelImport = document.getElementById('btnCancelImport');
const importEmail     = document.getElementById('importEmail');
const importPassword  = document.getElementById('importPassword');
const importError     = document.getElementById('importError');

// ── Phone mask ────────────────────────────────────────────────────────────────
function maskPhone(value) {
  const digits = value.replace(/\D/g, '').slice(0, 11);
  if (digits.length === 0) return '';
  if (digits.length <= 2)  return `(${digits}`;
  if (digits.length <= 6)  return `(${digits.slice(0,2)}) ${digits.slice(2)}`;
  if (digits.length === 10) {
    // (XX) XXXX-XXXX — 8-digit number (landline)
    return `(${digits.slice(0,2)}) ${digits.slice(2,6)}-${digits.slice(6)}`;
  }
  // (XX) X XXXX-XXXX — 9-digit number (mobile)
  return `(${digits.slice(0,2)}) ${digits.slice(2,3)} ${digits.slice(3,7)}-${digits.slice(7)}`;
}

inputPhone.addEventListener('input', e => {
  const masked = maskPhone(e.target.value);
  e.target.value = masked;
  state.phone = masked;
  render();
});

// ── Render canvas ────────────────────────────────────────────────────────────
function render() {
  const { templateId, logoDataUrl, name, tagline, phone, services, instagram, tiktok, website } = state;
  const cls = `tpl--${templateId}`;

  const socialItems = [
    instagram && `<span class="pf-social-item">📷 @${instagram}</span>`,
    tiktok    && `<span class="pf-social-item">🎵 @${tiktok}</span>`,
    website   && `<span class="pf-social-item">🌐 ${website}</span>`,
  ].filter(Boolean).join('');

  const logoHtml = logoDataUrl
    ? `<img class="pf-logo" src="${logoDataUrl}" alt="Logo">`
    : `<div class="pf-logo-placeholder">💅</div>`;

  // Show up to 8 services so the list doesn't overflow the canvas
  const visibleServices = services.slice(0, 8);
  const servicesHtml = visibleServices.map(s => `
    <div class="pf-service-row">
      <span class="pf-service-name">${escHtml(s.name || 'Serviço')}</span>
      <span class="pf-service-dots">········</span>
      <span class="pf-service-price">${escHtml(s.price || '')}</span>
    </div>
  `).join('');

  canvas.className = `panfleto-canvas ${cls}`;
  canvas.innerHTML = `
    <div class="pf-bg"></div>
    <div class="pf-deco pf-deco-1"></div>
    <div class="pf-deco pf-deco-2"></div>
    <div class="pf-content">
      <div class="pf-header">
        ${logoHtml}
        <div class="pf-title-block">
          <div class="pf-name">${escHtml(name || 'Seu negócio aqui')}</div>
          ${tagline ? `<div class="pf-tagline">${escHtml(tagline)}</div>` : ''}
        </div>
      </div>

      <div class="pf-divider"></div>

      <div class="pf-services">
        ${servicesHtml || '<div style="opacity:0.4;font-size:0.85em">Adicione seus serviços →</div>'}
      </div>

      <div class="pf-footer">
        <div class="pf-social">${socialItems}</div>
        ${phone ? `<div class="pf-phone">📞 ${escHtml(phone)}</div>` : ''}
        <div class="pf-badge">Agendofy.com</div>
      </div>
    </div>
  `;
}

function escHtml(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// ── Services list (form) ─────────────────────────────────────────────────────
function renderServicesList() {
  servicesList.innerHTML = '';
  state.services.forEach((svc, i) => {
    const row = document.createElement('div');
    row.className = 'service-row';
    row.innerHTML = `
      <input class="form-control" type="text" placeholder="Nome do serviço" value="${escHtml(svc.name)}" data-idx="${i}" data-field="name">
      <input class="form-control" type="text" placeholder="R$ 00" value="${escHtml(svc.price)}" data-idx="${i}" data-field="price">
      <button class="btn-remove-service" data-idx="${i}" title="Remover">×</button>
    `;
    servicesList.appendChild(row);
  });

  servicesList.querySelectorAll('input').forEach(inp => {
    inp.addEventListener('input', e => {
      const idx   = parseInt(e.target.dataset.idx);
      const field = e.target.dataset.field;
      state.services[idx][field] = e.target.value;
      render();
    });
  });

  servicesList.querySelectorAll('.btn-remove-service').forEach(btn => {
    btn.addEventListener('click', e => {
      const idx = parseInt(e.target.dataset.idx);
      state.services.splice(idx, 1);
      renderServicesList();
      render();
    });
  });
}

btnAddService.addEventListener('click', () => {
  state.services.push({ name: '', price: '' });
  renderServicesList();
  render();
  const inputs = servicesList.querySelectorAll('input[data-field="name"]');
  inputs[inputs.length - 1]?.focus();
});

// ── Logo upload ──────────────────────────────────────────────────────────────
logoUploadArea.addEventListener('click', () => inputLogo.click());

inputLogo.addEventListener('change', e => {
  const file = e.target.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = ev => {
    state.logoDataUrl = ev.target.result;
    logoPreviewImg.src = ev.target.result;
    logoPreviewImg.style.display = 'block';
    logoPlaceholder.style.display = 'none';
    btnRemoveLogo.style.display = 'block';
    render();
  };
  reader.readAsDataURL(file);
});

btnRemoveLogo.addEventListener('click', () => {
  state.logoDataUrl = null;
  logoPreviewImg.style.display = 'none';
  logoPlaceholder.style.display = 'flex';
  btnRemoveLogo.style.display = 'none';
  inputLogo.value = '';
  render();
});

// ── Text fields ───────────────────────────────────────────────────────────────
function bindField(el, key) {
  el.addEventListener('input', () => { state[key] = el.value.trim(); render(); });
}
bindField(inputName,      'name');
bindField(inputTagline,   'tagline');
bindField(inputInstagram, 'instagram');
bindField(inputTikTok,    'tiktok');
bindField(inputWebsite,   'website');

// ── Template selector ─────────────────────────────────────────────────────────
templateStrip.querySelectorAll('.tpl-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    state.templateId = btn.dataset.tpl;
    templateStrip.querySelectorAll('.tpl-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    render();
  });
});

// ── Export PNG ────────────────────────────────────────────────────────────────
btnExport.addEventListener('click', async () => {
  if (typeof html2canvas === 'undefined') {
    alert('A biblioteca de exportação ainda está carregando. Aguarde um momento e tente novamente.');
    return;
  }

  btnExport.disabled = true;
  btnExportLabel.style.display  = 'none';
  btnExportSpinner.style.display = 'inline';

  try {
    const exportCanvas = await html2canvas(canvas, {
      scale: 1080 / canvas.offsetWidth,
      useCORS: true,
      allowTaint: true,
      backgroundColor: null,
      logging: false,
    });

    const link = document.createElement('a');
    link.download = `panfleto-${(state.name || 'agendofy').replace(/\s+/g, '-').toLowerCase()}.png`;
    link.href = exportCanvas.toDataURL('image/png');
    link.click();
  } catch (err) {
    alert('Erro ao gerar imagem. Tente novamente.');
    console.error(err);
  } finally {
    btnExport.disabled = false;
    btnExportLabel.style.display  = 'inline';
    btnExportSpinner.style.display = 'none';
  }
});

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Extrai somente o handle de uma URL de rede social ou retorna o valor limpo */
function extractHandle(value) {
  if (!value) return '';
  // Remove URLs do tipo https://instagram.com/handle ou @handle
  const match = value.match(/(?:instagram\.com\/|tiktok\.com\/@?|@)([^/?&\s]+)/i);
  if (match) return match[1];
  return value.replace(/^@/, '').trim();
}

function getStoredToken() {
  return localStorage.getItem('access_token');
}

function getTenantSlug() {
  const host  = location.hostname;
  const parts = host.split('.');
  return parts.length >= 3 ? parts[0] : localStorage.getItem('tenant_slug');
}

async function apiGet(path, token, slug) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (slug)  headers['X-Tenant-Slug'] = slug;
  const res = await fetch(`${API_BASE}${path}`, { headers });
  if (!res.ok) throw new Error(`${res.status}`);
  return res.json();
}

// ── Import from Agendofy ──────────────────────────────────────────────────────
async function importFromAgendofy(token, slug) {
  const [settings, classTypes] = await Promise.all([
    apiGet('/settings', token, slug),
    apiGet('/class-types', token, slug),
  ]);

  // Nome da loja
  const tenantName = settings.name || '';
  if (tenantName) {
    state.name = tenantName;
    inputName.value = tenantName;
  }

  // Redes sociais — somente o handle, sem URL
  const ig = extractHandle(settings.socialInstagram || '');
  const tt = extractHandle(settings.socialTikTok || '');
  if (ig) { state.instagram = ig; inputInstagram.value = ig; }
  if (tt) { state.tiktok    = tt; inputTikTok.value    = tt; }

  // Telefone com máscara
  const phone = settings.socialWhatsApp || '';
  if (phone) {
    const masked = maskPhone(phone);
    state.phone = masked;
    inputPhone.value = masked;
  }

  // Catálogo da loja: [slug].agendofy.com/catalogo
  const tenantSlug = settings.slug || slug || '';
  if (tenantSlug) {
    const catalogUrl = `${tenantSlug}.agendofy.com/catalogo`;
    state.website = catalogUrl;
    inputWebsite.value = catalogUrl;
  } else if (settings.socialWebsite) {
    state.website = settings.socialWebsite;
    inputWebsite.value = settings.socialWebsite;
  }

  // Logo
  if (settings.logoUrl) {
    state.logoDataUrl = settings.logoUrl;
    logoPreviewImg.src = settings.logoUrl;
    logoPreviewImg.style.display = 'block';
    logoPlaceholder.style.display = 'none';
    btnRemoveLogo.style.display = 'block';
  }

  // Serviços com preço
  const activeServices = (classTypes || [])
    .filter(ct => ct.isActive && ct.price != null)
    .slice(0, 8)
    .map(ct => ({
      name:  ct.name,
      price: `R$ ${Number(ct.price).toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`,
    }));

  if (activeServices.length > 0) {
    state.services = activeServices;
    renderServicesList();
  }
}

function showImportSuccess() {
  const banner = document.querySelector('.panfleto-cta-banner');
  if (banner) {
    banner.innerHTML = `<span>✅</span> <span>Dados importados do <strong>Agendofy</strong> — edite à vontade!</span>`;
    banner.style.background   = 'linear-gradient(135deg,#f0fdf4,#dcfce7)';
    banner.style.borderColor  = '#86efac';
    banner.style.color        = '#14532d';
  }
}

// Auto-import silencioso se já está logado
(async () => {
  const token = getStoredToken();
  const slug  = getTenantSlug();
  if (token && slug) {
    try {
      await importFromAgendofy(token, slug);
      render();
      showImportSuccess();
    } catch {
      // não logado ou erro — ignora, usuário preenche manualmente
    }
  }
})();

// Botão de import manual
btnImport.addEventListener('click', () => {
  const token = getStoredToken();
  const slug  = getTenantSlug();
  if (token && slug) {
    importFromAgendofy(token, slug).then(() => { render(); showImportSuccess(); }).catch(() => {
      importModal.style.display = 'flex';
    });
  } else {
    importModal.style.display = 'flex';
    importEmail.focus();
  }
});

btnCancelImport.addEventListener('click', () => {
  importModal.style.display = 'none';
  importError.style.display = 'none';
});

importModal.addEventListener('click', e => {
  if (e.target === importModal) {
    importModal.style.display = 'none';
    importError.style.display = 'none';
  }
});

btnDoImport.addEventListener('click', async () => {
  const email    = importEmail.value.trim();
  const password = importPassword.value;

  if (!email || !password) {
    showImportError('Preencha e-mail e senha.');
    return;
  }

  btnDoImport.textContent = 'Importando…';
  btnDoImport.disabled = true;
  importError.style.display = 'none';

  try {
    const loginRes = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (!loginRes.ok) {
      showImportError('E-mail ou senha incorretos.');
      return;
    }

    const { accessToken, tenantSlug } = await loginRes.json();
    if (accessToken) localStorage.setItem('access_token', accessToken);
    if (tenantSlug)  localStorage.setItem('tenant_slug',  tenantSlug);

    await importFromAgendofy(accessToken, tenantSlug);
    render();

    importModal.style.display = 'none';
    importPassword.value = '';
    showImportSuccess();
  } catch {
    showImportError('Erro ao conectar. Tente novamente.');
  } finally {
    btnDoImport.textContent = 'Importar';
    btnDoImport.disabled = false;
  }
});

function showImportError(msg) {
  importError.textContent = msg;
  importError.style.display = 'block';
}

// ── Init ──────────────────────────────────────────────────────────────────────
renderServicesList();
render();
