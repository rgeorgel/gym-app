import { api } from './api.js';
import { loadTenantTheme } from './tenant.js';
import { showToast } from './ui.js';

export function getUser() {
  const raw = localStorage.getItem('user');
  return raw ? JSON.parse(raw) : null;
}

export function isLoggedIn() { return !!localStorage.getItem('access_token'); }

export function requireAuth(allowedRoles = []) {
  if (!isLoggedIn()) { location.href = '/index.html'; return false; }
  const user = getUser();
  if (allowedRoles.length && !allowedRoles.includes(user?.role)) {
    location.href = '/index.html';
    return false;
  }
  return true;
}

export async function initLoginPage() {
  // Slug from subdomain, URL param (?slug=boxe-elite), or stored value
  const host = location.hostname;
  const parts = host.split('.');
  const urlSlug = new URLSearchParams(location.search).get('slug');
  const tenantSlug = parts.length >= 3
    ? parts[0]
    : urlSlug || localStorage.getItem('tenant_slug') || null;

  if (tenantSlug) localStorage.setItem('tenant_slug', tenantSlug);

  const config = await loadTenantTheme();
  const hasTenant = !!config;

  if (isLoggedIn()) {
    redirectByRole(getUser()?.role);
    return;
  }

  // Toggle login/register — only show register link when tenant is resolved
  const toRegister = document.getElementById('toRegister');
  const toLogin = document.getElementById('toLogin');
  if (!hasTenant && toRegister) toRegister.classList.add('hidden');

  document.getElementById('linkRegister')?.addEventListener('click', (e) => {
    e.preventDefault();
    document.getElementById('loginForm').classList.add('hidden');
    document.getElementById('registerForm').classList.remove('hidden');
    toRegister.classList.add('hidden');
    toLogin.classList.remove('hidden');
  });

  document.getElementById('linkLogin')?.addEventListener('click', (e) => {
    e.preventDefault();
    document.getElementById('registerForm').classList.add('hidden');
    document.getElementById('loginForm').classList.remove('hidden');
    toLogin.classList.add('hidden');
    toRegister.classList.remove('hidden');
  });

  // Login form
  const loginForm = document.getElementById('loginForm');
  loginForm?.addEventListener('submit', async (e) => {
    e.preventDefault();
    const btn = loginForm.querySelector('button[type=submit]');
    const email = loginForm.querySelector('#email').value.trim();
    const password = loginForm.querySelector('#password').value;
    const errorEl = document.getElementById('loginError');

    btn.disabled = true;
    btn.textContent = 'Entrando...';
    errorEl?.classList.add('hidden');

    try {
      const body = { email, password };
      if (tenantSlug) body.tenantSlug = tenantSlug;
      const data = await api.post('/auth/login', body);
      storeSession(data);
      redirectByRole(data.role);
    } catch (err) {
      errorEl.textContent = 'E-mail ou senha incorretos.';
      errorEl?.classList.remove('hidden');
    } finally {
      btn.disabled = false;
      btn.textContent = 'Entrar';
    }
  });

  // Register form
  const registerForm = document.getElementById('registerForm');
  registerForm?.addEventListener('submit', async (e) => {
    e.preventDefault();
    const btn = registerForm.querySelector('button[type=submit]');
    const errorEl = document.getElementById('registerError');

    const name = document.getElementById('regName').value.trim();
    const email = document.getElementById('regEmail').value.trim();
    const phone = document.getElementById('regPhone').value.trim() || null;
    const birth = document.getElementById('regBirth').value || null;
    const password = document.getElementById('regPassword').value;
    const confirm = document.getElementById('regPasswordConfirm').value;

    errorEl?.classList.add('hidden');

    if (!name || !email || !password) {
      errorEl.textContent = 'Preencha nome, e-mail e senha.';
      errorEl?.classList.remove('hidden');
      return;
    }
    if (password !== confirm) {
      errorEl.textContent = 'As senhas não coincidem.';
      errorEl?.classList.remove('hidden');
      return;
    }
    if (password.length < 6) {
      errorEl.textContent = 'A senha deve ter pelo menos 6 caracteres.';
      errorEl?.classList.remove('hidden');
      return;
    }

    btn.disabled = true;
    btn.textContent = 'Criando conta...';

    try {
      const body = { name, email, password, phone, birthDate: birth || null };
      const data = await api.post('/auth/register', body);
      storeSession(data);
      redirectByRole(data.role);
    } catch (err) {
      errorEl.textContent = err.message === 'Conflict' || err.status === 409
        ? 'Este e-mail já está cadastrado.'
        : 'Erro ao criar conta. Tente novamente.';
      errorEl?.classList.remove('hidden');
    } finally {
      btn.disabled = false;
      btn.textContent = 'Criar conta';
    }
  });
}

function storeSession(data) {
  localStorage.setItem('access_token', data.accessToken);
  localStorage.setItem('refresh_token', data.refreshToken);
  localStorage.setItem('user', JSON.stringify({ id: data.userId, name: data.name, role: data.role }));
  if (data.tenantSlug) localStorage.setItem('tenant_slug', data.tenantSlug);
}

function redirectByRole(role) {
  if (role === 'SuperAdmin') { location.href = '/admin/index.html'; return; }
  if (role === 'Admin') { location.href = '/admin/index.html'; return; }
  location.href = '/app/index.html';
}
