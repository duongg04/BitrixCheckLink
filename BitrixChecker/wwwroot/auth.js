const tokenKey = 'token';

async function api(url, options = {}) {
    const headers = new Headers(options.headers || {});
    const token = sessionStorage.getItem(tokenKey);
    if (token) headers.set('Authorization', `Bearer ${token}`);
    const response = await fetch(url, { ...options, headers });
    if (response.status === 401) {
        sessionStorage.removeItem(tokenKey);
        if (!location.pathname.endsWith('/login.html')) location.assign('/login.html');
    }
    return response;
}

async function requireAuth() {
    const response = await api('/api/auth/me');
    if (!response.ok) return null;
    return response.json();
}

function logout() {
    sessionStorage.removeItem(tokenKey);
    location.assign('/login.html');
}

function authHeaders() {
    const token = sessionStorage.getItem(tokenKey);
    const headers = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return headers;
}
