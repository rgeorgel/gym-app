// Base API client with JWT auth + tenant header support
const API_BASE = '/api';

function getToken() { return localStorage.getItem('access_token'); }
function getTenantSlug() {
  // From subdomain or stored value
  const host = location.hostname;
  const parts = host.split('.');
  return parts.length >= 3 ? parts[0] : localStorage.getItem('tenant_slug');
}

function getLocationId() {
  return localStorage.getItem('location_id') || null;
}

function setLocationId(locationId) {
  if (locationId) {
    localStorage.setItem('location_id', locationId);
  } else {
    localStorage.removeItem('location_id');
  }
}

async function request(path, options = {}) {
  const token = getToken();
  const slug = getTenantSlug();
  const locationId = getLocationId();

  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (slug) headers['X-Tenant-Slug'] = slug;
  if (locationId) headers['X-Location-Id'] = locationId;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });

  if (res.status === 401) {
    // Try refresh
    const refreshed = await tryRefresh();
    if (refreshed) return request(path, options);
    logout();
    return;
  }

  if (!res.ok) {
    const err = await res.json().catch(() => ({ title: `Error ${res.status}` }));
    throw new ApiError(res.status, err.title || err.detail || 'Request failed', err);
  }

  if (res.status === 204) return null;
  const contentType = res.headers.get('content-type');
  if (!contentType || !contentType.includes('application/json')) return null;
  return res.json();
}

async function tryRefresh() {
  const refreshToken = localStorage.getItem('refresh_token');
  if (!refreshToken) return false;
  try {
    const res = await fetch(`${API_BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken })
    });
    if (!res.ok) return false;
    const data = await res.json();
    localStorage.setItem('access_token', data.accessToken);
    localStorage.setItem('refresh_token', data.refreshToken);
    return true;
  } catch { return false; }
}

function logout() {
  localStorage.removeItem('access_token');
  localStorage.removeItem('refresh_token');
  localStorage.removeItem('user');
  location.href = '/index.html';
}

class ApiError extends Error {
  constructor(status, message, details) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

export const api = {
  get: (path) => request(path),
  post: (path, body) => request(path, { method: 'POST', body: JSON.stringify(body) }),
  put: (path, body) => request(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: (path, body) => request(path, { method: 'DELETE', body: body ? JSON.stringify(body) : undefined }),
  patch: (path, body) => request(path, { method: 'PATCH', body: JSON.stringify(body) }),
};

export { logout, getToken, getTenantSlug, getLocationId, setLocationId };
