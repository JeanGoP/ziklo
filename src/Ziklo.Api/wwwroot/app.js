const form = document.querySelector('#simulator-form');
const feedback = document.querySelector('#feedback');
const submit = form.querySelector('button[type="submit"]');
const money = new Intl.NumberFormat('es-CO', {style: 'currency', currency: 'COP', minimumFractionDigits: 2});
const dateText = value => new Intl.DateTimeFormat('es-CO', {day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC'}).format(new Date(`${value}T00:00:00Z`));
const input = id => document.getElementById(id);
function syncOptions() {
  const custom = input('method').value === 'Custom';
  input('customField').hidden = !custom;
  input('capitalize').disabled = input('method').value !== 'Bullet';
  if (input('capitalize').disabled) input('capitalize').checked = false;
}
input('method').addEventListener('change', syncOptions);
syncOptions();
input('customPrincipal').addEventListener('blur', event => {
  event.target.value = event.target.value.split(';')
    .map(value => value.trim() ? ZikloMoney.clean(value.trim()) : '')
    .join('; ');
});

const today = new Date();
const first = new Date(Date.UTC(today.getFullYear(), today.getMonth() + 1, today.getDate()));
input('disbursementDate').value = today.toISOString().slice(0, 10);
input('firstDueDate').value = first.toISOString().slice(0, 10);

function payload() {
  return {
    principal: ZikloMoney.parse(input('principal')),
    rate: ZikloMoney.parse(input('rate')) / 100,
    rateKind: input('rateKind').value,
    dayCount: input('dayCount').value,
    frequency: input('frequency').value,
    installments: Number(input('installments').value),
    disbursementDate: input('disbursementDate').value,
    firstDueDate: input('firstDueDate').value,
    method: input('method').value,
    currencyDecimals: 2,
    capitalizeInterest: input('capitalize').checked,
    insurancePerInstallment: ZikloMoney.parse(input('insurance')) || 0,
    otherChargePerInstallment: ZikloMoney.parse(input('charges')) || 0,
    customPrincipal: input('method').value === 'Custom'
      ? input('customPrincipal').value.split(';').map(value => ZikloMoney.parse(value.trim()))
      : null
  };
}

function render(data) {
  input('estimatedPayment').textContent = money.format(data.estimatedPayment);
  input('originalPrincipal').textContent = money.format(data.originalPrincipal);
  input('totalInterest').textContent = money.format(data.totalInterest);
  input('totalCharges').textContent = money.format(data.totalInsurance + data.totalOtherCharges);
  input('totalPayable').textContent = money.format(data.totalPayable);
  input('rowCount').textContent = `${data.schedule.length} ${data.schedule.length === 1 ? 'periodo' : 'periodos'}`;
  const body = input('scheduleBody');
  body.replaceChildren(...data.schedule.map(row => {
    const tr = document.createElement('tr');
    [row.number, dateText(row.dueDate), money.format(row.openingPrincipal),
      money.format(row.principal), money.format(row.interest), money.format(row.capitalizedInterest),
      money.format(row.insurance), money.format(row.otherCharges), money.format(row.payment),
      money.format(row.closingPrincipal)].forEach(value => {
      const td = document.createElement('td');
      td.textContent = value;
      tr.append(td);
    });
    return tr;
  }));
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  feedback.textContent = '';
  ZikloMoney.validateAll(form);
  if (!form.reportValidity()) return;
  submit.disabled = true;
  try {
    const response = await fetch('/api/simulations/loans', {
      method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(payload())
    });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || 'No fue posible calcular la simulación.');
    render(data);
  } catch (error) {
    feedback.textContent = error.message;
  } finally {
    submit.disabled = false;
  }
});

form.requestSubmit();
