import { api } from '../api.js';

const DISMISSED_KEY = 'onboarding_dismissed';

function getCatalogUrl() {
  const slug = localStorage.getItem('tenant_slug') ?? '';
  const host = location.hostname;
  const parts = host.split('.');
  return parts.length >= 3
    ? `${location.protocol}//${host}/catalog/`
    : `${location.protocol}//${host}/catalog/?slug=${slug}`;
}

/**
 * Renders the onboarding wizard above the dashboard content.
 * @param {HTMLElement} container - element to prepend the wizard into
 */
export async function renderOnboardingWizard(container) {
  if (localStorage.getItem(DISMISSED_KEY) === '1') return;

  let services = [], availability = [];
  try {
    [services, availability] = await Promise.all([
      api.get('/class-types'),
      api.get('/availability'),
    ]);
  } catch {
    return; // silently skip if API fails
  }

  const hasServices     = services.length > 0;
  const hasAvailability = availability.length > 0;
  const allDone         = hasServices && hasAvailability;

  const catalogUrl = getCatalogUrl();

  const step = (done, icon, title, desc, action) => `
    <div style="display:flex;align-items:flex-start;gap:0.875rem;padding:0.75rem 0;border-bottom:1px solid var(--gray-100)">
      <div style="width:26px;height:26px;border-radius:50%;flex-shrink:0;display:flex;align-items:center;justify-content:center;font-size:0.8rem;font-weight:700;margin-top:0.1rem;
        background:${done ? 'var(--color-success)' : 'var(--gray-200)'};
        color:${done ? 'white' : 'var(--gray-400)'}">
        ${done ? '✓' : icon}
      </div>
      <div style="flex:1;min-width:0">
        <div style="font-weight:600;font-size:var(--font-size-sm);color:${done ? 'var(--gray-400)' : 'var(--gray-800)'};${done ? 'text-decoration:line-through' : ''}">${title}</div>
        <div style="font-size:var(--font-size-xs);color:var(--gray-400);margin-top:0.125rem">${desc}</div>
      </div>
      ${!done ? `<div style="flex-shrink:0">${action}</div>` : ''}
    </div>
  `;

  const el = document.createElement('div');
  el.id = 'onboardingWizard';
  el.innerHTML = `
    <div class="card" style="margin-bottom:1.25rem;border-left:3px solid var(--brand-secondary)">
      <div class="card-header" style="padding-bottom:0.5rem">
        <span class="card-title">🚀 Configure seu salão em 3 passos</span>
        <button id="btnDismissOnboarding" style="background:none;border:none;cursor:pointer;color:var(--gray-400);font-size:1.1rem;line-height:1;padding:0.25rem" title="Fechar">✕</button>
      </div>
      <div class="card-body" style="padding-top:0.25rem">
        ${step(
          hasServices, '1',
          'Adicione seus serviços',
          'Corte, manicure, escova, hidratação…',
          `<button class="btn btn-primary btn-sm" id="btnGoServices">Adicionar</button>`
        )}
        ${step(
          hasAvailability, '2',
          'Configure seus horários',
          'Informe os dias e horários em que você atende',
          `<button class="btn btn-primary btn-sm" id="btnGoAvailability">Configurar</button>`
        )}
        <div style="display:flex;align-items:flex-start;gap:0.875rem;padding:0.75rem 0">
          <div style="width:26px;height:26px;border-radius:50%;flex-shrink:0;display:flex;align-items:center;justify-content:center;font-size:0.8rem;font-weight:700;margin-top:0.1rem;background:var(--gray-200);color:var(--gray-400)">
            3
          </div>
          <div style="flex:1;min-width:0">
            <div style="font-weight:600;font-size:var(--font-size-sm)">Compartilhe seu link de agendamento</div>
            <div style="font-size:var(--font-size-xs);color:var(--gray-400);margin-top:0.125rem">Envie para suas clientes pelo WhatsApp, Instagram ou onde quiser</div>
            <div style="display:flex;align-items:center;gap:0.5rem;margin-top:0.5rem;flex-wrap:wrap">
              <input readonly value="${catalogUrl}"
                style="flex:1;min-width:0;font-size:0.75rem;font-family:monospace;background:var(--gray-50);border:1px solid var(--gray-200);border-radius:var(--border-radius);padding:0.375rem 0.625rem;color:var(--gray-600);cursor:text">
              <button class="btn btn-secondary btn-sm" id="btnCopyOnboardingLink" style="flex-shrink:0">Copiar</button>
              <a href="${catalogUrl}" target="_blank" class="btn btn-secondary btn-sm" style="flex-shrink:0;text-decoration:none">Abrir</a>
            </div>
          </div>
        </div>
        ${allDone ? `
          <div style="margin-top:0.25rem;padding:0.625rem 0.75rem;background:rgba(16,185,129,0.08);border-radius:var(--border-radius);font-size:var(--font-size-xs);color:var(--color-success);font-weight:600">
            Tudo configurado! Seu salão está pronto para receber agendamentos.
          </div>
        ` : ''}
        <div style="margin-top:1rem;text-align:right">
          <button id="btnDismissOnboardingBottom" style="background:none;border:1px solid var(--gray-300);border-radius:var(--border-radius);cursor:pointer;color:var(--gray-500);font-size:var(--font-size-sm);padding:0.375rem 0.875rem" title="Fechar">Não mostrar mais</button>
        </div>
      </div>
    </div>
  `;

  container.prepend(el);

  function dismiss() {
    localStorage.setItem(DISMISSED_KEY, '1');
    el.remove();
  }

  document.getElementById('btnDismissOnboarding').addEventListener('click', dismiss);
  document.getElementById('btnDismissOnboardingBottom').addEventListener('click', dismiss);

  document.getElementById('btnGoServices')?.addEventListener('click', () => {
    location.hash = 'class-types';
  });

  document.getElementById('btnGoAvailability')?.addEventListener('click', () => {
    location.hash = 'availability';
  });

  document.getElementById('btnCopyOnboardingLink')?.addEventListener('click', () => {
    navigator.clipboard.writeText(catalogUrl).then(() => {
      const btn = document.getElementById('btnCopyOnboardingLink');
      if (!btn) return;
      btn.textContent = 'Copiado!';
      setTimeout(() => { btn.textContent = 'Copiar'; }, 2000);
    });
  });
}
