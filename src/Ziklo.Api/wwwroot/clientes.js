const el = id => document.getElementById(id);
let companies = [];
let selectedClient = null;
let offset = 0;
let total = 0;
const roleLabel = role => ({Administrator: 'Administrador', Manager: 'Gerente',
  CreditAnalyst: 'Analista', Originator: 'Radicador', CreditApprover: 'Aprobador', Cashier: 'Cajero', Collector: 'Cobrador',
  Auditor: 'Auditor', ReadOnly: 'Consulta'})[role] || role;

function businessToday() {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/Bogota', year: 'numeric', month: '2-digit', day: '2-digit'
  }).formatToParts(new Date());
  const value = type => parts.find(part => part.type === type).value;
  return `${value('year')}-${value('month')}-${value('day')}`;
}

function base() { return `/api/companies/${encodeURIComponent(el('company').value)}`; }

async function request(path, options = {}) {
  const headers = new Headers(options.headers || {});
  const response = await zikloFetch(path, {...options, headers});
  if (!response.ok) {
    let detail = '';
    try { detail = (await response.json()).detail || ''; } catch { /* Sin detalle JSON. */ }
    throw new Error(detail || `La solicitud falló (${response.status}).`);
  }
  return response;
}

function money(value) {
  const currency = companies.find(x => x.companyId === el('company').value)?.currencyCode || 'COP';
  return new Intl.NumberFormat('es-CO', {style: 'currency', currency,
    minimumFractionDigits: 2, maximumFractionDigits: 2}).format(value);
}

async function initialize() {
  try {
    const session = await window.zikloAccessReady;
    const profile = session?.profile || await (await request('/api/auth/me')).json();
    companies = profile.companies || [];
    if (!companies.length) throw new Error('Tu usuario no tiene empresas activas asignadas.');
    el('company').replaceChildren(...companies.map(company => {
      const option = document.createElement('option');
      option.value = company.companyId;
      option.textContent = `${company.name} · ${roleLabel(company.role)}`;
      return option;
    }));
    el('company').hidden = true;
    el('company').insertAdjacentText('afterend', companies[0].name);
    el('workspace').hidden = false;
    await searchClients();
  } catch (error) {
    el('feedback').textContent = error.message;
    if (el('workspace').hidden) {
      el('access-error').textContent = error.message;
      el('access-panel').hidden = false;
    }
  }
}

async function searchClients() {
  el('feedback').textContent = '';
  const query = new URLSearchParams({skip: String(offset), take: '25'});
  const search = el('client-search').value.trim();
  if (search) query.set('search', search);
  try {
    const result = await (await request(`${base()}/clients?${query}`)).json();
    total = result.total;
    const items = result.items || [];
    el('result-count').textContent = `${total} ${total === 1 ? 'cliente' : 'clientes'}`;
    el('page-label').textContent = total ? `${offset + 1}–${offset + items.length} de ${total}` : 'Sin resultados';
    el('previous').disabled = offset === 0;
    el('next').disabled = offset + items.length >= total;
    const cards = items.map(client => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.clientId = client.id;
      button.className = `client-choice${selectedClient?.id === client.id ? ' selected' : ''}`;
      const name = document.createElement('strong');
      name.textContent = client.name;
      const identification = document.createElement('span');
      identification.textContent = `${client.identificationType} ${client.identificationNumber} · ${client.status === 'Active' ? 'Activo' : 'Inactivo'}`;
      button.append(name, identification);
      button.addEventListener('click', () => selectClient(client));
      return button;
    });
    el('client-results').replaceChildren(...cards);
    if (!cards.length) el('client-results').textContent = 'No se encontraron clientes.';
  } catch (error) { el('feedback').textContent = error.message; }
}

async function selectClient(client) {
  selectedClient = client;
  el('client-detail').hidden = false;
  el('detail-title').textContent = client.name;
  el('detail-identification').textContent = `${client.identificationType} ${client.identificationNumber}`;
  el('detail-status').textContent = client.status === 'Active' ? 'Activo' : 'Inactivo';
  el('reference-feedback').textContent = '';
  for (const button of el('client-results').querySelectorAll('button'))
    button.classList.toggle('selected', button.dataset.clientId === client.id);
  try {
    const clientBase = `${base()}/clients/${encodeURIComponent(client.id)}`;
    const [summaryResponse, referencesResponse, documentsResponse] = await Promise.all([
      request(`${clientBase}/summary?asOf=${businessToday()}`),
      request(`${clientBase}/references`),
      request(`${clientBase}/documents`)
    ]);
    if (selectedClient?.id !== client.id) return;
    renderSummary(await summaryResponse.json());
    renderReferences(await referencesResponse.json());
    renderDocuments(await documentsResponse.json());
  } catch (error) { el('feedback').textContent = error.message; }
}

function renderSummary(summary) {
  el('active-loans').textContent = String(summary.activeLoanCount);
  el('paid-off-loans').textContent = String(summary.paidOffLoanCount);
  el('total-outstanding').textContent = money(summary.totalOutstanding);
  el('principal-outstanding').textContent = money(summary.principalOutstanding);
  el('interest-outstanding').textContent = money(summary.currentInterestOutstanding);
  el('late-outstanding').textContent = money(summary.lateInterestOutstanding);
  el('payment-count').textContent = String(summary.paymentCount);
  el('summary-date').textContent = `Saldos publicados al ${summary.asOf}.`;
}

function renderReferences(references) {
  const cards = references.map(reference => {
    const card = document.createElement('div');
    card.className = 'reference-card';
    const name = document.createElement('strong');
    name.textContent = reference.name;
    const kind = document.createElement('small');
    kind.textContent = `${reference.kind === 'Commercial' ? 'Comercial' : 'Personal'}${reference.relationship ? ` · ${reference.relationship}` : ''}`;
    const contact = document.createElement('span');
    contact.textContent = [reference.phone, reference.email].filter(Boolean).join(' · ');
    card.append(name, kind, contact);
    if (reference.notes) {
      const notes = document.createElement('span');
      notes.textContent = reference.notes;
      card.append(notes);
    }
    return card;
  });
  el('reference-list').replaceChildren(...cards);
  if (!cards.length) el('reference-list').textContent = 'Aún no hay referencias registradas.';
}

function renderDocuments(documents) {
  const rows = documents.map(file => {
    const row = document.createElement('div');
    row.className = 'document-row';
    const description = document.createElement('span');
    const name = document.createElement('strong');
    name.textContent = file.fileName;
    const meta = document.createElement('small');
    meta.textContent = ` · ${(file.length / 1024).toFixed(1)} KB · ${file.createdAtUtc.slice(0, 10)}`;
    description.append(name, meta);
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = 'Descargar';
    button.addEventListener('click', () => downloadDocument(file));
    row.append(description, button);
    return row;
  });
  el('document-list').replaceChildren(...rows);
  if (!rows.length) el('document-list').textContent = 'Aún no hay documentos adjuntos.';
}

async function downloadDocument(file) {
  if (!selectedClient) return;
  try {
    const url = `${base()}/clients/${encodeURIComponent(selectedClient.id)}/documents/${encodeURIComponent(file.id)}`;
    const response = await request(url);
    const temporary = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = temporary;
    link.download = file.fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(temporary), 10000);
  } catch (error) { el('document-feedback').textContent = error.message; }
}

const optional = id => el(id).value.trim() || null;
el('search-form').addEventListener('submit', event => { event.preventDefault(); offset = 0; searchClients(); });
el('company').addEventListener('change', () => {
  selectedClient = null;
  offset = 0;
  el('client-detail').hidden = true;
  searchClients();
});
el('previous').addEventListener('click', () => { offset = Math.max(0, offset - 25); searchClients(); });
el('next').addEventListener('click', () => { offset += 25; searchClients(); });

el('create-form').addEventListener('submit', async event => {
  event.preventDefault();
  ZikloMoney.validateAll(event.currentTarget);
  if (!event.currentTarget.reportValidity()) return;
  const button = event.currentTarget.querySelector('button[type="submit"]');
  button.disabled = true;
  el('create-feedback').textContent = '';
  const income = optional('monthly-income');
  const command = {
    identificationType: el('identification-type').value.trim(),
    identificationNumber: el('identification-number').value.trim(),
    name: el('client-name').value.trim(),
    address: optional('address'), phone: optional('phone'), whatsApp: optional('whatsapp'),
    email: optional('email'), city: optional('city'), birthDate: optional('birth-date'),
    economicActivity: optional('economic-activity'),
    monthlyIncome: income === null ? null : ZikloMoney.parse(el('monthly-income')),
    employer: optional('employer')
  };
  try {
    const response = await request(`${base()}/clients`, {
      method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(command)
    });
    const created = await response.json();
    el('create-feedback').textContent = `Cliente ${created.name} guardado.`;
    el('create-form').reset();
    el('client-search').value = created.identificationNumber;
    offset = 0;
    await searchClients();
    await selectClient({...created, status: 'Active'});
  } catch (error) { el('create-feedback').textContent = error.message; }
  finally { button.disabled = false; }
});

el('reference-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!selectedClient) return;
  const button = event.currentTarget.querySelector('button[type="submit"]');
  button.disabled = true;
  el('reference-feedback').textContent = '';
  const command = {
    kind: el('reference-kind').value, name: el('reference-name').value.trim(),
    relationship: optional('relationship'), phone: optional('reference-phone'),
    email: optional('reference-email'), notes: optional('reference-notes')
  };
  try {
    const clientBase = `${base()}/clients/${encodeURIComponent(selectedClient.id)}`;
    await request(`${clientBase}/references`, {
      method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(command)
    });
    el('reference-feedback').textContent = 'Referencia guardada.';
    el('reference-form').reset();
    renderReferences(await (await request(`${clientBase}/references`)).json());
  } catch (error) { el('reference-feedback').textContent = error.message; }
  finally { button.disabled = false; }
});

el('document-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!selectedClient) return;
  const file = el('document-file').files[0];
  if (!file) return;
  if (file.size > 10 * 1024 * 1024) {
    el('document-feedback').textContent = 'El documento supera 10 MB.';
    return;
  }
  const button = event.currentTarget.querySelector('button[type="submit"]');
  button.disabled = true;
  el('document-feedback').textContent = '';
  const clientBase = `${base()}/clients/${encodeURIComponent(selectedClient.id)}`;
  const extension = file.name.split('.').pop()?.toLowerCase();
  const fallback = {pdf: 'application/pdf', png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg'};
  try {
    await request(`${clientBase}/documents?fileName=${encodeURIComponent(file.name)}`, {
      method: 'POST', headers: {'Content-Type': file.type || fallback[extension] || 'application/octet-stream'},
      body: file
    });
    el('document-feedback').textContent = 'Documento adjuntado.';
    el('document-form').reset();
  } catch (error) {
    el('document-feedback').textContent = error.message;
    button.disabled = false;
    return;
  }
  try {
    renderDocuments(await (await request(`${clientBase}/documents`)).json());
  } catch (error) {
    el('document-feedback').textContent = `Documento adjuntado. No se pudo actualizar la lista: ${error.message}`;
  } finally { button.disabled = false; }
});

initialize();
