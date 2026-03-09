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
  await loadTenantTheme();

  if (isLoggedIn()) {
    redirectByRole(getUser()?.role);
    return;
  }

  const form = document.getElementById('loginForm');
  if (!form) return;

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const btn = form.querySelector('button[type=submit]');
    const email = form.querySelector('#email').value.trim();
    const password = form.querySelector('#password').value;
    const errorEl = document.getElementById('loginError');

    btn.disabled = true;
    btn.textContent = 'Entrando...';
    errorEl?.classList.add('hidden');

    try {
      const data = await api.post('/auth/login', { email, password });
      localStorage.setItem('access_token', data.accessToken);
      localStorage.setItem('refresh_token', data.refreshToken);
      localStorage.setItem('user', JSON.stringify({ id: data.userId, name: data.name, role: data.role }));
      redirectByRole(data.role);
    } catch (err) {
      errorEl.textContent = 'E-mail ou senha incorretos.';
      errorEl?.classList.remove('hidden');
    } finally {
      btn.disabled = false;
      btn.textContent = 'Entrar';
    }
  });
}

function redirectByRole(role) {
  if (role === 'SuperAdmin') { location.href = '/admin/index.html'; return; }
  if (role === 'Admin') { location.href = '/admin/index.html'; return; }
  location.href = '/app/index.html';
}
