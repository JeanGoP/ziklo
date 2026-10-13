const el = id => document.getElementById(id);
let companies = [];
let currentReport = null;
let currentQuery = '';

function businessToday() {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/Bogota', year: 'numeric', month: '2-digit', day: '2-digit'
  }).formatToParts(new Date());
  const value = type => parts.find(part => part.type === type).value;
  return `${value('year')}-${value('month')}-${value('day')}`;
}

const today = businessToday();
const start = new Date(`${today}T12:00:00Z`);
start.setUTCMonth(start.getUTCMonth() - 6);
start.setUTCDate(1);
el('from').value = start.toISOString().slice(0, 10);
el('to').value = today;
el('to').max = today;

async function request(path) {
  const response = await zikloFetch(path);
  if (!response.ok) {
    let detail = '';
    try { detail = (await response.json()).detail || ''; } catch { /* Sin detalle JSON. */ }
    throw new Error(detail || `La solicitud falló (${response.status}).`);
  }
  return response;
}

function base() { return `/api/companies/${encodeURIComponent(el('company').value)}`; }

function formatCell(value, column, report) {
  if (!report.numericColumns.includes(column)) return value;
  const numeric = Number(value);
  const header = report.headers[column].toLowerCase();
  if (header.includes('días') || header.includes('atraso') ||
      header.includes('préstamos') || header.includes('cuotas'))
    return new Intl.NumberFormat('es-CO', {maximumFractionDigits: 0}).format(numeric);
  const currency = companies.find(x => x.companyId === el('company').value)?.currencyCode || 'COP';
  return new Intl.NumberFormat('es-CO', {style: 'currency', currency,
    minimumFractionDigits: 2, maximumFractionDigits: 2}).format(numeric);
}

function render(report) {
  currentReport = report;
  el('report-title').textContent = report.title;
  el('report-period').textContent = `${report.from} al ${report.to}`;
  el('row-count').textContent = `${report.rows.length} ${report.rows.length === 1 ? 'registro' : 'registros'}`;
  el('report-note').textContent = report.note;
  const heading = document.createElement('tr');
  report.headers.forEach((header, column) => {
    const cell = document.createElement('th');
    cell.textContent = header;
    if (report.numericColumns.includes(column)) cell.className = 'numeric';
    heading.append(cell);
  });
  el('report-head').replaceChildren(heading);
  const rows = report.rows.map(values => {
    const row = document.createElement('tr');
    values.forEach((value, column) => {
      const cell = document.createElement('td');
      cell.textContent = formatCell(value, column, report);
      if (report.numericColumns.includes(column)) cell.className = 'numeric';
      row.append(cell);
    });
    return row;
  });
  if (!rows.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = report.headers.length;
    cell.className = 'empty';
    cell.textContent = 'No hay registros para este informe y periodo.';
    row.append(cell);
    rows.push(row);
  }
  el('report-body').replaceChildren(...rows);
  el('download-xlsx').disabled = false;
  el('download-csv').disabled = false;
  el('print-report').disabled = false;
}

async function generate() {
  const from = el('from').value;
  const to = el('to').value;
  if (!from || !to || from > to || to > today) {
    el('feedback').textContent = 'Selecciona un rango de fechas válido hasta hoy.';
    return;
  }
  const kind = el('kind').value;
  const query = new URLSearchParams({from, to});
  const button = el('generate');
  button.disabled = true;
  el('feedback').textContent = '';
  try {
    const report = await (await request(`${base()}/reports/catalog/${kind}?${query}`)).json();
    currentQuery = `${base()}/reports/catalog/${kind}?${query}`;
    render(report);
  } catch (error) { el('feedback').textContent = error.message; }
  finally { button.disabled = false; }
}

async function download(extension) {
  if (!currentReport) return;
  const button = el(`download-${extension}`);
  button.disabled = true;
  el('feedback').textContent = '';
  try {
    const path = currentQuery.replace('?', `.${extension}?`);
    const response = await request(path);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = `${currentReport.kind}-${currentReport.from}-${currentReport.to}.${extension}`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
  } catch (error) { el('feedback').textContent = error.message; }
  finally { button.disabled = false; }
}

el('generate').addEventListener('click', generate);
el('kind').addEventListener('change', generate);
el('company').addEventListener('change', generate);
el('download-xlsx').addEventListener('click', () => download('xlsx'));
el('download-csv').addEventListener('click', () => download('csv'));
el('print-report').addEventListener('click', () => window.print());
el('download-xlsx').disabled = true;
el('download-csv').disabled = true;
el('print-report').disabled = true;

(async () => {
  try {
    const profile = await (await request('/api/auth/me')).json();
    companies = profile.companies || [];
    if (!companies.length) throw new Error('No hay empresas disponibles.');
    el('company').replaceChildren(...companies.map(company => {
      const option = document.createElement('option');
      option.value = company.companyId;
      option.textContent = company.name;
      return option;
    }));
    el('company').hidden = true;
    el('company').insertAdjacentText('afterend', companies[0].name);
    el('workspace').hidden = false;
    await generate();
  } catch (error) {
    el('error-detail').textContent = error.message;
    el('error-panel').hidden = false;
  }
})();
