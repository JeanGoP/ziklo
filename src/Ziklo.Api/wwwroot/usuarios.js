const roleNames = {
  Administrator: 'Administrador', Manager: 'Gerencia', Originator: 'Radicador',
  CreditAnalyst: 'Analista de crédito', CreditApprover: 'Aprobador',
  Cashier: 'Desembolsos y caja', Collector: 'Cobranza',
  Auditor: 'Auditoría', ReadOnly: 'Consulta'
};
const roleDescriptions = {
  Administrator: 'Gestiona usuarios, perfiles y toda la operación.',
  Manager: 'Supervisa la operación y puede tomar decisiones.',
  Originator: 'Registra clientes y prepara operaciones; no aprueba.',
  CreditAnalyst: 'Estudia solicitudes y registra observaciones; no aprueba.',
  CreditApprover: 'Decide sobre solicitudes; no desembolsa.',
  Cashier: 'Desembolsa y registra pagos; no aprueba.',
  Collector: 'Gestiona cobros y compromisos de pago.',
  Auditor: 'Consulta reportes y trazabilidad.',
  ReadOnly: 'Consulta la operación sin modificarla.'
};
const permissionNames = {
  ViewClients:'Consultar clientes', ManageClients:'Gestionar clientes',
  ViewLoans:'Consultar créditos y solicitudes', CreateLoan:'Crear créditos',
  StudyApplications:'Estudiar solicitudes',
  ApproveLoan:'Aprobar o rechazar', DisburseLoan:'Desembolsar',
  RegisterPayment:'Registrar pagos', RunDailyClose:'Ejecutar cierre diario',
  RecordCollection:'Gestionar cobranza', ViewReports:'Consultar informes',
  ViewAudit:'Consultar auditoría', ManageProducts:'Gestionar productos',
  ManagePermissions:'Administrar usuarios y permisos'
};
const el = id => document.getElementById(id);
let companyId;
let roles = [];
let customProfiles = [];
let permissionEntries = [];
let resetUser = null;
let currentUserId = null;
let editingProfileId = null;

async function api(path, options = {}) {
  const response = await zikloFetch(path, options);
  if (!response.ok) {
    let detail;
    try { const body = await response.json(); detail = body.detail || body.title; } catch {}
    throw new Error(detail || `No se pudo completar la operación (${response.status}).`);
  }
  return response.status === 204 ? null : response.json();
}

function status(message, error = false) {
  el('feedback').textContent = message;
  el('feedback').style.color = error ? '#b4232f' : '#1f5baa';
}

function roleSelect(user) {
  const select = document.createElement('select');
  for (const role of roles) select.add(new Option(roleNames[role.role] || role.role, role.role));
  for (const profile of customProfiles.filter(item => item.isActive))
    select.add(new Option(profile.name, `profile:${profile.id}`));
  select.value = user.profileId ? `profile:${user.profileId}` : user.role;
  return select;
}

const selectedRole = value => value.startsWith('profile:')
  ? {role:'ReadOnly', profileId:value.slice(8)} : {role:value, profileId:null};

function renderRoles() {
  const target = el('roles');
  target.replaceChildren();
  for (const role of roles) {
    const card = document.createElement('article');
    card.className = 'role-card';
    const title = document.createElement('strong');
    title.textContent = roleNames[role.role] || role.role;
    const description = document.createElement('p');
    description.textContent = roleDescriptions[role.role] || '';
    const count = document.createElement('p');
    count.textContent = `${role.permissions.length} permisos efectivos`;
    const edit = document.createElement('button');
    edit.type = 'button'; edit.textContent = 'Configurar permisos';
    edit.addEventListener('click', () => openRole(role.role));
    card.append(title, description, count, edit);
    target.append(card);
  }
  for (const profile of customProfiles) {
    const card = document.createElement('article'); card.className = 'role-card';
    const title = document.createElement('strong'); title.textContent = profile.name;
    const description = document.createElement('p');
    description.textContent = profile.isActive ? 'Perfil personalizado' : 'Perfil inactivo';
    const count = document.createElement('p');
    count.textContent = `${profile.permissions.length} permisos efectivos`;
    const edit = document.createElement('button');
    edit.type = 'button'; edit.textContent = 'Editar perfil';
    edit.addEventListener('click', () => openCustomProfile(profile));
    card.append(title, description, count, edit); target.append(card);
  }
  const createRole = el('create-user').elements.role;
  createRole.replaceChildren();
  for (const role of roles) createRole.add(new Option(roleNames[role.role] || role.role, role.role));
  for (const profile of customProfiles.filter(item => item.isActive))
    createRole.add(new Option(profile.name, `profile:${profile.id}`));
  createRole.value = 'Originator';
}

function openCustomProfile(profile = null) {
  editingProfileId = profile?.id || null;
  el('profile-title').textContent = profile ? `Editar perfil · ${profile.name}` : 'Crear perfil';
  const form = el('profile-form'); form.reset();
  form.elements.name.value = profile?.name || '';
  form.elements.active.checked = profile?.isActive ?? true;
  const selected = new Set(profile?.permissions || []);
  const list = el('custom-permissions'); list.replaceChildren();
  for (const [permission, caption] of Object.entries(permissionNames)) {
    if (permission === 'ManagePermissions') continue;
    const label = document.createElement('label');
    const check = document.createElement('input');
    check.type = 'checkbox'; check.value = permission;
    check.checked = selected.has(permission);
    const text = document.createElement('span'); text.textContent = caption;
    label.append(check, text); list.append(label);
  }
  el('profile-dialog').showModal();
}

el('new-profile').addEventListener('click', () => openCustomProfile());
el('cancel-profile').addEventListener('click', () => el('profile-dialog').close());
el('profile-form').addEventListener('submit', async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('[type="submit"]'); button.disabled = true;
  const payload = {name:form.elements.name.value.trim(),
    permissions:[...el('custom-permissions').querySelectorAll('input:checked')].map(x => x.value),
    isActive:form.elements.active.checked};
  try {
    await api(`/api/companies/${companyId}/profiles${editingProfileId ? `/${editingProfileId}` : ''}`, {
      method:editingProfileId ? 'PUT' : 'POST',
      headers:{'Content-Type':'application/json'}, body:JSON.stringify(payload)
    });
    customProfiles = await api(`/api/companies/${companyId}/profiles`);
    renderRoles(); await loadUsers();
    el('profile-dialog').close(); status('Perfil guardado. Ya puedes asignarlo a usuarios.');
  } catch (error) { status(error.message, true); }
  finally { button.disabled = false; }
});

function lockedPermission(role, permission) {
  if (permission === 'ManagePermissions') return true;
  if (['CreditAnalyst','Originator'].includes(role) &&
      ['ApproveLoan','DisburseLoan'].includes(permission)) return true;
  if (role === 'Originator' && permission === 'StudyApplications') return true;
  return role === 'CreditApprover' &&
    ['CreateLoan','DisburseLoan','StudyApplications'].includes(permission);
}

function openRole(role) {
  el('role-title').textContent = `Permisos · ${roleNames[role] || role}`;
  const list = el('permission-list');
  list.replaceChildren();
  for (const entry of permissionEntries.filter(item => item.role === role)) {
    const label = document.createElement('label');
    const check = document.createElement('input');
    check.type = 'checkbox'; check.checked = entry.allowed;
    check.disabled = lockedPermission(role, entry.permission);
    check.addEventListener('change', async () => {
      check.disabled = true;
      try {
        await api(`/api/companies/${companyId}/permissions/${role}/${entry.permission}`, {
          method:'PUT', headers:{'Content-Type':'application/json'},
          body:JSON.stringify({allowed:check.checked})
        });
        entry.allowed = check.checked;
        roles = await api(`/api/companies/${companyId}/roles`);
        renderRoles();
        status(`Permiso de ${roleNames[role]} actualizado.`);
      } catch (error) { check.checked = entry.allowed; status(error.message, true); }
      finally { check.disabled = lockedPermission(role, entry.permission); }
    });
    const text = document.createElement('span');
    text.textContent = permissionNames[entry.permission] || entry.permission;
    label.append(check, text); list.append(label);
  }
  el('role-dialog').showModal();
}

el('close-role').addEventListener('click', () => el('role-dialog').close());

async function loadUsers() {
  const users = await api(`/api/companies/${companyId}/users`);
  const target = el('users');
  target.replaceChildren();
  if (!users.length) { target.textContent = 'Aún no hay usuarios.'; return; }
  for (const user of users) {
    const row = document.createElement('div'); row.className = 'user-row';
    const identity = document.createElement('div');
    const email = document.createElement('strong'); email.textContent = user.email;
    const state = document.createElement('small');
    state.textContent = user.isActive ? 'Activo' : 'Inactivo';
    identity.append(email, state);
    const select = roleSelect(user);
    select.setAttribute('aria-label', `Perfil de ${user.email}`);
    const self = user.id === currentUserId;
    if (self) select.disabled = true;
    const actions = document.createElement('div'); actions.className = 'user-actions';
    const save = document.createElement('button'); save.type = 'button'; save.textContent = 'Guardar perfil';
    save.disabled = self;
    save.addEventListener('click', () => update(user, select.value, user.isActive));
    const toggle = document.createElement('button'); toggle.type = 'button';
    toggle.textContent = user.isActive ? 'Desactivar' : 'Activar';
    toggle.disabled = self;
    toggle.addEventListener('click', () => update(user, select.value, !user.isActive));
    const password = document.createElement('button'); password.type = 'button';
    password.textContent = 'Cambiar clave';
    password.addEventListener('click', () => {
      resetUser = user;
      el('password-for').textContent = user.email;
      el('password-form').reset();
      el('password-dialog').showModal();
    });
    actions.append(save, toggle, password);
    row.append(identity, select, actions);
    target.append(row);
  }
}

async function update(user, role, isActive) {
  try {
    await api(`/api/companies/${companyId}/users/${user.id}`, {
      method: 'PUT', headers: {'Content-Type':'application/json'},
      body: JSON.stringify({...selectedRole(role), isActive})
    });
    status(`Acceso de ${user.email} actualizado.`);
    await loadUsers();
  } catch (error) { status(error.message, true); }
}

el('create-user').addEventListener('submit', async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  button.disabled = true;
  try {
    await api(`/api/companies/${companyId}/users`, {
      method: 'POST', headers: {'Content-Type':'application/json'},
      body: JSON.stringify({email: form.elements.email.value.trim(),
        ...selectedRole(form.elements.role.value), password: form.elements.password.value})
    });
    status('Usuario creado. Ya puedes asignarle tareas según su perfil.');
    form.reset(); form.elements.role.value = 'Originator';
    await loadUsers();
  } catch (error) { status(error.message, true); }
  finally { button.disabled = false; }
});

el('cancel-password').addEventListener('click', () => el('password-dialog').close());
el('password-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!resetUser) return;
  const form = event.currentTarget;
  const button = form.querySelector('[type="submit"]');
  button.disabled = true;
  try {
    await api(`/api/companies/${companyId}/users/${resetUser.id}/password`, {
      method: 'PUT', headers: {'Content-Type':'application/json'},
      body: JSON.stringify({password: form.elements.password.value})
    });
    status(`Contraseña de ${resetUser.email} actualizada.`);
    el('password-dialog').close();
  } catch (error) { status(error.message, true); }
  finally { button.disabled = false; }
});

(async () => {
  try {
    const session = await window.zikloAccessReady;
    const profile = session?.profile || await api('/api/auth/me');
    currentUserId = profile.id;
    companyId = profile.companies[0]?.companyId;
    if (!companyId) throw new Error('No tienes una empresa activa.');
    roles = await api(`/api/companies/${companyId}/roles`);
    customProfiles = await api(`/api/companies/${companyId}/profiles`);
    permissionEntries = await api(`/api/companies/${companyId}/permissions`);
    renderRoles();
    await loadUsers();
    el('workspace').hidden = false;
  } catch (error) { status(error.message, true); }
})();
