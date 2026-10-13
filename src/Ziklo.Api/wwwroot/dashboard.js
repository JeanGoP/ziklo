const byId = id => document.getElementById(id);
let companies = [];
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
byId('as-of').value = businessToday();

function showWorkspace(visible) {
  byId('workspace').hidden = !visible;
  byId('demo-error-panel').hidden = visible;
}

async function request(path, options = {}) {
  const headers = new Headers(options.headers || {});
  const response = await zikloFetch(path, {...options, headers});
  if (!response.ok) {
    let detail = '';
    try { detail = (await response.json()).detail || ''; } catch { /* Sin cuerpo JSON. */ }
    throw new Error(detail || `La solicitud falló (${response.status}).`);
  }
  return response;
}

function formatMoney(value) {
  const selected = companies.find(company => company.companyId === byId('company').value);
  const currency = selected?.currencyCode || 'COP';
  try {
    return new Intl.NumberFormat('es-CO', {style: 'currency', currency,
      minimumFractionDigits: 2, maximumFractionDigits: 2}).format(value);
  } catch {
    return new Intl.NumberFormat('es-CO', {maximumFractionDigits: 2}).format(value);
  }
}

async function loadProfile() {
  const session = await window.zikloAccessReady;
  const profile = session?.profile || await (await request('/api/auth/me')).json();
  companies = profile.companies || [];
  if (!companies.length) throw new Error('Tu usuario no tiene empresas activas asignadas.');
  const select = byId('company');
  select.replaceChildren(...companies.map(company => {
    const option = document.createElement('option');
    option.value = company.companyId;
    option.textContent = `${company.name} · ${roleLabel(company.role)}`;
    return option;
  }));
  select.hidden = true;
  select.insertAdjacentText('afterend', ` · ${companies[0].name}`);
  showWorkspace(true);
  await refresh();
}

function setText(id, value) { byId(id).textContent = value; }

function renderDashboard(data) {
  const currencyFields = {
    'portfolio-total': data.portfolioTotal,
    'principal-outstanding': data.principalOutstanding,
    'current-interest': data.currentInterestOutstanding,
    'late-interest': data.lateInterestOutstanding,
    'receipts-today': data.receiptsToday,
    'receipts-month': data.receiptsThisMonth,
    'capital-disbursed': data.capitalDisbursed,
    'current-accrued': data.currentInterestAccrued,
    'current-collected': data.currentInterestCollected,
    'late-accrued': data.lateInterestAccrued,
    'late-collected': data.lateInterestCollected
  };
  for (const [id, value] of Object.entries(currencyFields)) setText(id, formatMoney(value));
  setText('active-loans', String(data.activeLoanCount));
  setText('delinquent-loans', String(data.delinquentLoanCount));
  setText('paid-off', String(data.paidOffLoanCount));
  const max = Math.max(1, ...data.aging.map(item => item.total));
  byId('aging-list').replaceChildren(...data.aging.map(item => {
    const row = document.createElement('div');
    row.className = 'aging-row';
    const label = document.createElement('span');
    label.textContent = item.bucket;
    const track = document.createElement('div');
    track.className = 'track';
    const fill = document.createElement('i');
    fill.style.width = `${Math.max(0, item.total / max * 100)}%`;
    track.append(fill);
    const value = document.createElement('strong');
    value.textContent = formatMoney(item.total);
    row.append(label, track, value);
    return row;
  }));
}

function renderPortfolio(data) {
  setText('loan-count', `${data.loanCount} ${data.loanCount === 1 ? 'préstamo' : 'préstamos'}`);
  const body = byId('loans-body');
  if (!data.loans.length) {
    const tr = document.createElement('tr');
    const td = document.createElement('td');
    td.colSpan = 7;
    td.className = 'empty';
    td.textContent = 'No hay préstamos con saldo a esta fecha.';
    tr.append(td);
    body.replaceChildren(tr);
    return;
  }
  body.replaceChildren(...data.loans.map(loan => {
    const tr = document.createElement('tr');
    [loan.loanNumber, loan.agingBucket, loan.daysPastDue,
      formatMoney(loan.principal), formatMoney(loan.currentInterest),
      formatMoney(loan.lateInterest), formatMoney(loan.total)].forEach(value => {
      const td = document.createElement('td');
      td.textContent = value;
      tr.append(td);
    });
    return tr;
  }));
}

async function refresh() {
  const companyId = byId('company').value;
  const asOf = byId('as-of').value;
  if (!companyId || !asOf) return;
  const button = byId('refresh');
  button.disabled = true;
  byId('workspace-feedback').textContent = '';
  try {
    const base = `/api/companies/${encodeURIComponent(companyId)}`;
    const query = `?asOf=${encodeURIComponent(asOf)}`;
    const [dashboardResponse, portfolioResponse] = await Promise.all([
      request(`${base}/dashboard${query}`), request(`${base}/portfolio${query}`)
    ]);
    renderDashboard(await dashboardResponse.json());
    renderPortfolio(await portfolioResponse.json());
    byId('print-context').textContent = `${companies.find(x => x.companyId === companyId)?.name || ''} · Corte ${asOf}`;
  } catch (error) {
    byId('workspace-feedback').textContent = error.message;
  } finally {
    button.disabled = false;
  }
}

byId('refresh').addEventListener('click', refresh);
byId('company').addEventListener('change', refresh);
byId('as-of').addEventListener('change', refresh);
async function exportPortfolio(extension) {
  const companyId = byId('company').value;
  const asOf = byId('as-of').value;
  try {
    const response = await request(`/api/companies/${encodeURIComponent(companyId)}` +
      `/reports/portfolio.${extension}?asOf=${encodeURIComponent(asOf)}`);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = `cartera-${asOf}.${extension}`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  } catch (error) {
    byId('workspace-feedback').textContent = error.message;
  }
}
byId('export').addEventListener('click', () => exportPortfolio('csv'));
byId('export-xlsx').addEventListener('click', () => exportPortfolio('xlsx'));
byId('print').addEventListener('click', () => window.print());

loadProfile().catch(error => {
  byId('demo-error').textContent = error.message;
  showWorkspace(false);
});
