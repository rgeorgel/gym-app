import { setLocale, loadTranslations, t } from './i18n.js';
import ptBR from './locales/pt-BR.js';
import enUS from './locales/en-US.js';

// Pre-load both locale dictionaries
loadTranslations('pt-BR', ptBR);
loadTranslations('en-US', enUS);

// Loads tenant config and applies white label theme + locale
export async function loadTenantTheme() {
  // Default locale so t() works even if tenant config is unavailable (e.g. super admin)
  setLocale('pt-BR');

  try {
    const host = location.hostname;
    const parts = host.split('.');
    const slug = parts.length >= 3 ? parts[0] : localStorage.getItem('tenant_slug') || 'boxe-elite';

    const res = await fetch(`/api/tenant/config`, {
      headers: { 'X-Tenant-Slug': slug }
    });

    if (!res.ok) return;

    const config = await res.json();

    // Set locale before any rendering
    const lang = config.language ?? 'pt-BR';
    setLocale(lang);
    document.documentElement.lang = lang;

    // Apply CSS custom properties
    const root = document.documentElement;
    root.style.setProperty('--brand-primary', config.primaryColor);
    root.style.setProperty('--brand-secondary', config.secondaryColor);
    root.style.setProperty('--brand-primary-light', shadeColor(config.primaryColor, -20));

    // Update page title
    document.title = config.name + ' — Gym App';

    // Update logo if present
    const logos = document.querySelectorAll('[data-tenant-logo]');
    logos.forEach(el => {
      if (config.logoUrl) el.src = config.logoUrl;
      el.alt = config.name;
    });

    // Update tenant name placeholders
    const names = document.querySelectorAll('[data-tenant-name]');
    names.forEach(el => el.textContent = config.name);

    // Apply static translations
    document.querySelectorAll('[data-i18n]').forEach(el => {
      el.textContent = t(el.dataset.i18n);
    });

    return config;
  } catch (e) {
    console.warn('Could not load tenant config:', e);
  }
}

function shadeColor(color, percent) {
  const num = parseInt(color.replace('#', ''), 16);
  const amt = Math.round(2.55 * percent);
  const R = (num >> 16) + amt;
  const G = (num >> 8 & 0x00FF) + amt;
  const B = (num & 0x0000FF) + amt;
  return '#' + (0x1000000 +
    (R < 255 ? R < 1 ? 0 : R : 255) * 0x10000 +
    (G < 255 ? G < 1 ? 0 : G : 255) * 0x100 +
    (B < 255 ? B < 1 ? 0 : B : 255)
  ).toString(16).slice(1);
}
