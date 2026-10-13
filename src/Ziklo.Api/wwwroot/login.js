const loginForm = document.getElementById('login-form');
const setupForm = document.getElementById('setup-form');
const feedback = document.getElementById('feedback');

function destination() {
  const requested = new URLSearchParams(location.search).get('next') || '/dashboard.html';
  return requested.startsWith('/') && !requested.startsWith('//')
    ? requested : '/dashboard.html';
}

async function signIn(email, password) {
  const response = await fetch('/api/auth/login', {
    method: 'POST', headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({email, password})
  });
  if (!response.ok) throw new Error(response.status === 401
    ? 'Correo o contraseña incorrectos.' : 'No fue posible iniciar sesión.');
  const result = await response.json();
  sessionStorage.removeItem('zikloAccessCache');
  sessionStorage.setItem('zikloAccessToken', result.accessToken);
  location.replace(destination());
}

loginForm.addEventListener('submit', async event => {
  event.preventDefault();
  const button = loginForm.querySelector('button');
  feedback.textContent = '';
  button.disabled = true;
  try {
    await signIn(loginForm.elements.email.value.trim(), loginForm.elements.password.value);
  } catch (error) {
    feedback.textContent = error.message;
    button.disabled = false;
  }
});

setupForm.addEventListener('submit', async event => {
  event.preventDefault();
  const button = setupForm.querySelector('button');
  feedback.textContent = '';
  const email = setupForm.elements.email.value.trim();
  const password = setupForm.elements.password.value;
  if (password !== setupForm.elements.confirmation.value) {
    feedback.textContent = 'Las contraseñas no coinciden.';
    return;
  }
  button.disabled = true;
  try {
    const response = await fetch('/api/auth/setup', {
      method: 'POST', headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({email, password})
    });
    if (!response.ok) {
      let detail = '';
      try { detail = (await response.json()).detail; } catch {}
      throw new Error(detail || 'No fue posible crear el acceso.');
    }
    await signIn(email, password);
  } catch (error) {
    feedback.textContent = error.message;
    button.disabled = false;
  }
});

(async () => {
  try {
    const response = await fetch('/api/auth/setup-status');
    if (!response.ok) return;
    const status = await response.json();
    if (!status.required) return;
    loginForm.hidden = true;
    setupForm.hidden = false;
    document.getElementById('login-title').textContent = 'Configura el acceso inicial';
    document.getElementById('login-hint').textContent =
      'Crea el usuario administrador para este equipo. Después podrás crear los demás usuarios y asignar sus perfiles.';
  } catch {
    feedback.textContent = 'No fue posible comprobar el acceso inicial.';
  }
})();
