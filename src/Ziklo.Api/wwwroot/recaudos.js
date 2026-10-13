const el = id => document.getElementById(id);
let companies = [];
let loans = [];
let selectedLoan = null;
let offset = 0;
let total = 0;
let pendingPayment = null;
let scheduleLines = [];
let selectedInstallments = new Set();
let installmentNumberById = new Map();
const roleLabel = role => ({Administrator: 'Administrador', Manager: 'Gerente',
  CreditAnalyst: 'Analista', Originator: 'Radicador', CreditApprover: 'Aprobador', Cashier: 'Cajero', Collector: 'Cobrador',
  Auditor: 'Auditor', ReadOnly: 'Consulta'})[role] || role;
const loanStatusLabel = status => ({Draft: 'Borrador', Requested: 'Solicitado',
  Approved: 'Aprobado', Rejected: 'Rechazado', Active: 'Activo',
  InArrears: 'En mora', PaidOff: 'Cancelado', Refinanced: 'Refinanciado',
  ChargedOff: 'Castigado'})[status] || status;

function businessToday() {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/Bogota', year: 'numeric', month: '2-digit', day: '2-digit'
  }).formatToParts(new Date());
  const value = type => parts.find(part => part.type === type).value;
  return `${value('year')}-${value('month')}-${value('day')}`;
}

const today = businessToday();
el('payment-date').value = today;
el('payment-date').max = today;
el('report-from').value = `${today.slice(0, 8)}01`;
el('report-to').value = today;
el('report-to').max = today;

function companyBase() {
  return `/api/companies/${encodeURIComponent(el('company').value)}`;
}

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

function money(value, currencyCode) {
  const currency = currencyCode || companies.find(x => x.companyId === el('company').value)?.currencyCode || 'COP';
  return new Intl.NumberFormat('es-CO', {style: 'currency', currency,
    minimumFractionDigits: 2, maximumFractionDigits: 2}).format(value);
}

function fieldCell(value) {
  const td = document.createElement('td');
  td.textContent = String(value);
  return td;
}

function emptyRow(body, columns, message) {
  const row = document.createElement('tr');
  const cell = fieldCell(message);
  cell.colSpan = columns;
  cell.className = 'empty';
  row.append(cell);
  body.replaceChildren(row);
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
    const requestedLoan = new URLSearchParams(location.search).get('loan');
    if (requestedLoan) el('loan-search').value = requestedLoan;
    await searchLoans();
    if (requestedLoan) {
      const match = loans.find(loan => loan.number === requestedLoan);
      if (match) await selectLoan(match);
    }
  } catch (error) {
    el('feedback').textContent = error.message;
    if (el('workspace').hidden) {
      el('access-error').textContent = error.message;
      el('access-panel').hidden = false;
    }
  }
}

async function searchLoans() {
  el('feedback').textContent = '';
  const query = new URLSearchParams({skip: String(offset), take: '25'});
  const search = el('loan-search').value.trim();
  if (search) query.set('search', search);
  try {
    const result = await (await request(`${companyBase()}/loans?${query}`)).json();
    loans = result.items || [];
    total = result.total;
    el('result-count').textContent = `${total} ${total === 1 ? 'resultado' : 'resultados'}`;
    el('page-label').textContent = total ? `${offset + 1}–${offset + loans.length} de ${total}` : 'Sin resultados';
    el('previous').disabled = offset === 0;
    el('next').disabled = offset + loans.length >= total;
    const cards = loans.map(loan => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = `loan-choice${selectedLoan?.id === loan.id ? ' selected' : ''}`;
      const title = document.createElement('strong');
      title.textContent = loan.number;
      const client = document.createElement('small');
      client.textContent = loan.clientName || 'Cliente sin nombre';
      const status = document.createElement('span');
      status.textContent = `${loanStatusLabel(loan.status)} · ${money(loan.originalPrincipal, loan.currencyCode)}`;
      button.append(title, client, status);
      button.addEventListener('click', () => selectLoan(loan));
      return button;
    });
    el('loan-results').replaceChildren(...cards);
    if (!cards.length) el('loan-results').textContent = 'No se encontraron préstamos.';
  } catch (error) { el('feedback').textContent = error.message; }
}

async function selectLoan(loan) {
  selectedLoan = loan;
  pendingPayment = null;
  scheduleLines = [];
  selectedInstallments = new Set();
  installmentNumberById = new Map();
  el('payment-amount').value = '';
  updateSelectedInstallments();
  el('feedback').textContent = '';
  el('payment-feedback').textContent = '';
  el('loan-detail').hidden = false;
  el('loan-title').textContent = loan.number;
  el('client-name').textContent = loan.clientName || 'Cliente sin nombre';
  el('loan-status').textContent = loanStatusLabel(loan.status);
  el('original-principal').textContent = money(loan.originalPrincipal, loan.currencyCode);
  el('payment-form').hidden = !['Active', 'InArrears'].includes(loan.status);
  for (const button of el('loan-results').querySelectorAll('button'))
    button.classList.toggle('selected', button.querySelector('strong')?.textContent === loan.number);
  if (!loan.disbursementDate) {
    el('balance-total').textContent = money(0, loan.currencyCode);
    el('balance-principal').textContent = money(0, loan.currencyCode);
    el('balance-interest').textContent = money(0, loan.currencyCode);
    el('balance-late').textContent = money(0, loan.currencyCode);
    el('past-due').textContent = '0';
    emptyRow(el('schedule-body'), 9, 'El préstamo aún no tiene plan de pagos.');
    renderReceipts({total: 0, items: []}, loan.currencyCode);
    if (loan.status === 'Rejected') {
      try {
        const rejection = await (await request(`${companyBase()}/loans/${loan.id}/rejection`)).json();
        el('feedback').textContent = `Solicitud rechazada: ${rejection.reason}`;
      } catch (error) { el('feedback').textContent = error.message; }
    }
    return;
  }
  try {
    const loanBase = `${companyBase()}/loans/${encodeURIComponent(loan.id)}`;
    const [statementResponse, scheduleResponse, receiptsResponse] = await Promise.all([
      request(`${loanBase}/statement?asOf=${today}`),
      request(`${loanBase}/schedule`),
      request(`${loanBase}/payments?take=20`)
    ]);
    if (selectedLoan?.id !== loan.id) return;
    renderStatement(await statementResponse.json(), loan.currencyCode);
    renderSchedule(await scheduleResponse.json(), loan.currencyCode);
    renderReceipts(await receiptsResponse.json(), loan.currencyCode);
  } catch (error) { el('feedback').textContent = error.message; }
}

function renderStatement(statement, currency) {
  el('balance-total').textContent = money(statement.totalOutstanding, currency);
  el('balance-principal').textContent = money(statement.principalOutstanding, currency);
  el('balance-interest').textContent = money(statement.currentInterestOutstanding, currency);
  el('balance-late').textContent = money(statement.lateInterestOutstanding, currency);
  el('past-due').textContent = String(statement.pastDueInstallments);
}

function renderSchedule(schedule, currency) {
  const body = el('schedule-body');
  scheduleLines = schedule.installments || [];
  installmentNumberById = new Map(scheduleLines.map(line => [line.id, line.number]));
  if (!scheduleLines.length) {
    emptyRow(body, 9, 'Este préstamo aún no tiene un plan de pagos.');
    return;
  }
  const effectiveDate = el('payment-date').value;
  body.replaceChildren(...scheduleLines.map(line => {
    const row = document.createElement('tr');
    const eligible = line.dueDate <= effectiveDate && Number(line.remaining) > 0 &&
      ['Active', 'InArrears'].includes(selectedLoan?.status);
    if (!eligible) selectedInstallments.delete(line.number);
    const choice = document.createElement('td');
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.className = 'installment-choice';
    checkbox.setAttribute('aria-label', `Pagar cuota ${line.number}, saldo registrado ${money(line.remaining, currency)}`);
    checkbox.value = String(line.number);
    checkbox.disabled = !eligible;
    checkbox.checked = selectedInstallments.has(line.number);
    checkbox.addEventListener('change', () => {
      if (checkbox.checked) selectedInstallments.add(line.number);
      else selectedInstallments.delete(line.number);
      row.classList.toggle('chosen', checkbox.checked);
      updateSelectedInstallments(true);
    });
    choice.append(checkbox);
    row.append(choice);
    row.classList.toggle('chosen', checkbox.checked);
    [line.number, line.dueDate, money(line.principal, currency),
      money(line.interest, currency), money(line.insurance, currency),
      money(line.otherCharges, currency), money(line.remaining, currency),
      money(line.payment, currency)]
      .forEach(value => row.append(fieldCell(value)));
    return row;
  }));
  updateSelectedInstallments();
}

function updateSelectedInstallments(setAmount = false) {
  const chosen = scheduleLines.filter(line => selectedInstallments.has(line.number));
  const balance = chosen.reduce((sum, line) => sum + Number(line.remaining), 0);
  el('selected-installments').textContent = chosen.length
    ? `Cuotas ${chosen.map(line => line.number).join(', ')} · saldo registrado ${money(balance, selectedLoan?.currencyCode)}. La mora adicional se calcula al registrar.`
    : 'Ninguna cuota seleccionada.';
  if (setAmount) el('payment-amount').value = chosen.length
    ? ZikloMoney.clean(balance.toFixed(2)) : '';
}

function renderReceipts(result, currency) {
  el('receipt-count').textContent = `${result.total} recibos`;
  const body = el('receipts-body');
  if (!result.items?.length) { emptyRow(body, 6, 'No hay recaudos registrados.'); return; }
  body.replaceChildren(...result.items.map(receipt => {
    const row = document.createElement('tr');
    [receipt.effectiveDate, receipt.externalReference, receipt.method,
      money(receipt.received, currency), money(receipt.applied, currency)]
      .forEach(value => row.append(fieldCell(value)));
    const cell = document.createElement('td');
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'receipt-link';
    button.textContent = 'Ver';
    button.addEventListener('click', () => showReceipt(receipt.id));
    cell.append(button);
    row.append(cell);
    return row;
  }));
}

function addReceiptMeta(container, label, value) {
  const box = document.createElement('div');
  const small = document.createElement('small');
  const strong = document.createElement('strong');
  small.textContent = label;
  strong.textContent = value;
  box.append(small, strong);
  container.append(box);
}

async function showReceipt(receiptId) {
  if (!selectedLoan) return;
  try {
    const receipt = await (await request(`${companyBase()}/loans/${selectedLoan.id}/payments/${receiptId}`)).json();
    const detail = el('receipt-detail');
    const meta = document.createElement('div');
    meta.className = 'receipt-meta';
    addReceiptMeta(meta, 'Préstamo', selectedLoan.number);
    addReceiptMeta(meta, 'Fecha efectiva', receipt.effectiveDate);
    addReceiptMeta(meta, 'Referencia', receipt.externalReference);
    addReceiptMeta(meta, 'Medio', receipt.method);
    addReceiptMeta(meta, 'Identificador', receipt.id);
    addReceiptMeta(meta, 'Recibido', money(receipt.received, receipt.currencyCode));
    const allocations = document.createElement('div');
    for (const line of receipt.allocations) {
      const row = document.createElement('div');
      row.className = 'receipt-allocation';
      const label = document.createElement('span');
      label.textContent = `Cuota ${installmentNumberById.get(line.installmentId) || '—'} · ${line.component}`;
      const amount = document.createElement('strong');
      amount.textContent = money(line.amount, receipt.currencyCode);
      row.append(label, amount);
      allocations.append(row);
    }
    const totalRow = document.createElement('div');
    totalRow.className = 'receipt-total';
    totalRow.append(document.createTextNode('Total aplicado'));
    const totalValue = document.createElement('strong');
    totalValue.textContent = money(receipt.applied, receipt.currencyCode);
    totalRow.append(totalValue);
    detail.replaceChildren(meta, allocations, totalRow);
    el('receipt-dialog').showModal();
  } catch (error) { el('feedback').textContent = error.message; }
}

el('search-form').addEventListener('submit', event => {
  event.preventDefault();
  offset = 0;
  searchLoans();
});
el('company').addEventListener('change', () => {
  selectedLoan = null;
  pendingPayment = null;
  offset = 0;
  el('loan-detail').hidden = true;
  searchLoans();
});
el('previous').addEventListener('click', () => { offset = Math.max(0, offset - 25); searchLoans(); });
el('next').addEventListener('click', () => { offset += 25; searchLoans(); });
el('payment-date').addEventListener('change', () => renderSchedule({installments: scheduleLines}, selectedLoan?.currencyCode));
el('payment-form').addEventListener('submit', async event => {
  event.preventDefault();
  ZikloMoney.validateAll(event.currentTarget);
  if (!event.currentTarget.reportValidity()) return;
  if (!selectedLoan) return;
  if (selectedInstallments.size === 0) {
    el('payment-feedback').textContent = 'Selecciona al menos una cuota exigible del plan de pagos.';
    return;
  }
  const payload = {
    effectiveDate: el('payment-date').value,
    amount: ZikloMoney.parse(el('payment-amount')),
    method: el('payment-method').value,
    externalReference: el('payment-reference').value.trim(),
    installmentNumbers: [...selectedInstallments].sort((a, b) => a - b)
  };
  const fingerprint = JSON.stringify([el('company').value, selectedLoan.id, payload]);
  if (pendingPayment?.fingerprint !== fingerprint)
    pendingPayment = {fingerprint, key: crypto.randomUUID()};
  const button = el('submit-payment');
  button.disabled = true;
  el('payment-feedback').textContent = '';
  let receipt;
  try {
    const response = await request(`${companyBase()}/loans/${selectedLoan.id}/payments`, {
      method: 'POST', headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({...payload, idempotencyKey: pendingPayment.key})
    });
    receipt = await response.json();
  } catch (error) {
    el('payment-feedback').textContent = error.message;
    button.disabled = false;
    return;
  }
  pendingPayment = null;
  el('payment-feedback').textContent = `Recaudo registrado. Comprobante ${receipt.id}.`;
  el('payment-amount').value = '';
  el('payment-reference').value = '';
  button.disabled = false;
  try {
    const loan = await (await request(`${companyBase()}/loans/${selectedLoan.id}`)).json();
    await selectLoan(loan);
    el('payment-feedback').textContent = `Recaudo registrado. Comprobante ${receipt.id}.`;
    await searchLoans();
    await showReceipt(receipt.id);
  } catch (error) {
    el('payment-feedback').textContent = `Recaudo registrado. Comprobante ${receipt.id}. No se pudo actualizar la vista: ${error.message}`;
  }
});
el('close-receipt').addEventListener('click', () => el('receipt-dialog').close());
el('print-receipt').addEventListener('click', () => window.print());
el('download-report').addEventListener('click', async () => {
  const from = el('report-from').value;
  const to = el('report-to').value;
  if (!from || !to || from > to) {
    el('feedback').textContent = 'Selecciona un rango de fechas válido.';
    return;
  }
  const button = el('download-report');
  button.disabled = true;
  el('feedback').textContent = '';
  try {
    const query = new URLSearchParams({from, to});
    const response = await request(`${companyBase()}/reports/payments.csv?${query}`);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = `recaudos-${from}-${to}.csv`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
  } catch (error) { el('feedback').textContent = error.message; }
  finally { button.disabled = false; }
});
initialize();
