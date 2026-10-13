const backOfficePages = new Set([
  '/dashboard.html', '/solicitudes.html', '/clientes.html',
  '/recaudos.html', '/informes.html', '/usuarios.html'
]);
const requiredPermissions = {
  '/dashboard.html': 'ViewReports',
  '/solicitudes.html': 'ViewLoans',
  '/clientes.html': 'ViewClients',
  '/recaudos.html': 'ViewLoans',
  '/informes.html': 'ViewReports',
  '/usuarios.html': 'ManagePermissions'
};
const signInUrl = `/login.html?next=${encodeURIComponent(location.pathname + location.search)}`;
const tokenAtLoad = sessionStorage.getItem('zikloAccessToken');
if (backOfficePages.has(location.pathname) && !tokenAtLoad)
  location.replace(signInUrl);

function landingPage(role, permissions) {
  if (['Originator', 'CreditAnalyst', 'CreditApprover'].includes(role) &&
      permissions.has('ViewLoans')) return '/solicitudes.html';
  if (['Cashier', 'Collector'].includes(role) && permissions.has('ViewLoans'))
    return '/recaudos.html';
  if (permissions.has('ViewReports')) return '/dashboard.html';
  if (permissions.has('ViewLoans')) return '/solicitudes.html';
  if (permissions.has('ViewClients')) return '/clientes.html';
  if (permissions.has('ManagePermissions')) return '/usuarios.html';
  return '/';
}

function showSessionIdentity(profile, company) {
  const main = document.querySelector('main.container');
  if (!main) return;
  const roleNames = {
    Administrator: 'Administrador', Manager: 'Gerencia', Originator: 'Radicador',
    CreditAnalyst: 'Analista de crédito', CreditApprover: 'Aprobador',
    Cashier: 'Caja', Collector: 'Cobranza', Auditor: 'Auditoría', ReadOnly: 'Consulta'
  };
  const identity = document.createElement('div');
  identity.className = 'session-identity';
  identity.setAttribute('aria-label', 'Sesión actual');
  const label = document.createElement('span');
  label.textContent = 'Sesión actual';
  const value = document.createElement('strong');
  const role = company?.profileName || roleNames[company?.role] || company?.role;
  value.textContent = role ? `${profile.email} · ${role}` : profile.email;
  identity.append(label, value);
  main.prepend(identity);
}

function startAlertBadge(companyId, headers) {
  const nav = document.querySelector('.topbar nav');
  if (!nav) return;
  const link = document.createElement('a');
  link.href = '/solicitudes.html?avisos=1';
  link.hidden = true;
  nav.append(link);
  async function refresh() {
    if (document.hidden) return;
    const response = await fetch(
      `/api/companies/${encodeURIComponent(companyId)}/my-application-alerts`,
      {headers, cache:'no-store'});
    if (!response.ok) return;
    const alerts = await response.json();
    link.hidden = alerts.total === 0;
    link.textContent = `Avisos (${alerts.total})`;
    link.setAttribute('aria-label', `${alerts.total} solicitudes tuyas necesitan información`);
  }
  refresh().catch(() => {});
  setInterval(() => refresh().catch(() => {}), 30000);
}

async function checkPageAccess() {
  if (!tokenAtLoad) return;
  const headers = {Authorization: `Bearer ${tokenAtLoad}`};
  const profileResponse = await fetch('/api/auth/me', {headers, cache:'no-store'});
  if (profileResponse.status === 401) {
    sessionStorage.removeItem('zikloAccessToken');
    document.querySelector('.logout-button')?.remove();
    if (backOfficePages.has(location.pathname)) {
      location.replace(signInUrl);
      return new Promise(() => {});
    }
    return;
  }
  if (!profileResponse.ok) return;
  const profile = await profileResponse.json();
  const radicatorState = document.getElementById('radicator-state');
  if (radicatorState)
    radicatorState.textContent = `Radicando con la cuenta ${profile.email}. Si el analista pide información, verás el motivo en Avisos.`;
  const company = profile.companies?.[0];
  if (!company) {
    showSessionIdentity(profile, null);
    return;
  }
  const permissionResponse = await fetch(
    `/api/companies/${encodeURIComponent(company.companyId)}/my-permissions`,
    {headers, cache:'no-store'});
  if (permissionResponse.status === 401) {
    sessionStorage.removeItem('zikloAccessToken');
    document.querySelector('.logout-button')?.remove();
    if (backOfficePages.has(location.pathname)) {
      location.replace(signInUrl);
      return new Promise(() => {});
    }
    return;
  }
  if (!permissionResponse.ok) return;
  const permissions = new Set(await permissionResponse.json());
  for (const link of document.querySelectorAll('.topbar nav a[href]')) {
    const required = requiredPermissions[new URL(link.href).pathname];
    if (required) link.hidden = !permissions.has(required);
  }
  const signInLink = document.querySelector('.topbar nav [data-login]');
  if (signInLink) signInLink.hidden = true;
  const needed = requiredPermissions[location.pathname];
  if (needed && !permissions.has(needed)) {
    location.replace(landingPage(company.role, permissions));
    return new Promise(() => {});
  }
  showSessionIdentity(profile, company);
  if (permissions.has('ViewLoans')) startAlertBadge(company.companyId, headers);
}

window.zikloAccessReady = checkPageAccess();

window.zikloFetch = async function (path, options = {}) {
  if (backOfficePages.has(location.pathname) || tokenAtLoad)
    await window.zikloAccessReady;
  const token = sessionStorage.getItem('zikloAccessToken');
  if (tokenAtLoad && !token) {
    if (backOfficePages.has(location.pathname)) location.assign(signInUrl);
    throw new Error('La sesión expiró. Ingresa de nuevo antes de radicar.');
  }
  const headers = new Headers(options.headers || {});
  if (token && String(path).startsWith('/api/')) headers.set('Authorization', `Bearer ${token}`);
  const response = await fetch(path, {...options, headers});
  if (response.status === 401 && String(path).startsWith('/api/') &&
      !location.pathname.endsWith('/login.html')) {
    sessionStorage.removeItem('zikloAccessToken');
    location.replace(`/login.html?next=${encodeURIComponent(location.pathname + location.search)}`);
  }
  return response;
};

if (sessionStorage.getItem('zikloAccessToken')) {
  const nav = document.querySelector('.topbar nav');
  if (nav) {
    const logout = document.createElement('button');
    logout.type = 'button'; logout.className = 'logout-button';
    logout.textContent = 'Salir';
    logout.addEventListener('click', () => {
      sessionStorage.removeItem('zikloAccessToken');
      location.assign('/login.html');
    });
    nav.append(logout);
  }
}
