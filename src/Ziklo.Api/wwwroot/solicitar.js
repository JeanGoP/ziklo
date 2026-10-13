const form = document.querySelector('#application-form');
form.noValidate = true;
const primaryEmail = form.querySelector('[name="email"]');
function validatePrimaryEmail() {
  primaryEmail.setCustomValidity(primaryEmail.value.trim() ? '' : 'Ingresa tu correo electrónico para continuar.');
}
primaryEmail.addEventListener('input', validatePrimaryEmail);
const productSelect = document.querySelector('#product');
const feedback = document.querySelector('#feedback');
const submit = document.querySelector('#submit');
const products = [];
const money = value => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP' }).format(value);

const steps = [
  ['El crédito que buscas', 'Comienza con las condiciones que necesitas.'],
  ['Tus datos', 'Solo lo necesario para identificarte y contactarte.'],
  ['Actividad e ingresos', 'Información para estudiar tu capacidad de pago.'],
  ['Referencias', 'Personas que pueden confirmar tus datos.'],
  ['Codeudor', 'Agrégalo únicamente si aplica a esta solicitud.'],
  ['Revisar y radicar', 'Comprueba los datos y adjunta los soportes que tengas.']
];
let currentStep = 0;
const originalSections = [...form.querySelectorAll(':scope > fieldset')];
const [personalSection, incomeSection, creditSection, supportSection] = originalSections;
const acknowledgement = form.querySelector('.acknowledgement');
const submitRow = form.querySelector('.submit-row');
const wizardPages = steps.map((step, index) => {
  const page = document.createElement('section');
  page.className = 'wizard-page';
  page.dataset.step = index;
  page.hidden = index !== 0;
  page.setAttribute('aria-label', step[0]);
  form.insertBefore(page, personalSection);
  return page;
});
wizardPages[0].append(creditSection);
wizardPages[1].append(personalSection);
wizardPages[2].append(incomeSection);
incomeSection.querySelector('.application-fields').prepend(
  personalSection.querySelector('[name="economicActivity"]').closest('label'));
wizardPages[5].append(supportSection, acknowledgement);
submitRow.remove();

function makeField([name, label, type = 'text']) {
  const wrapper = document.createElement('label');
  wrapper.className = 'field';
  const caption = document.createElement('span');
  caption.textContent = label;
  const input = document.createElement('input');
  input.name = name;
  input.type = type;
  if (type === 'text' || type === 'email') input.maxLength = 80;
  if (type === 'number') { input.min = '0'; input.step = '0.01'; }
  const choices = {
    appCivilStatus: ['Soltero/a', 'Casado/a', 'Unión libre', 'Separado/a', 'Viudo/a'],
    coCivilStatus: ['Soltero/a', 'Casado/a', 'Unión libre', 'Separado/a', 'Viudo/a'],
    appHousing: ['Propia', 'Familiar', 'Arrendada', 'Otra'],
    coHousing: ['Propia', 'Familiar', 'Arrendada', 'Otra'],
    appWorkStatus: ['Empleado/a', 'Independiente', 'Pensionado/a', 'Desempleado/a'],
    coWorkStatus: ['Empleado/a', 'Independiente', 'Pensionado/a', 'Desempleado/a'],
    appWorkCategory: ['Asalariado/a', 'Independiente', 'Pensionado/a', 'Socio/a', 'Otro'],
    coWorkCategory: ['Asalariado/a', 'Independiente', 'Pensionado/a', 'Socio/a', 'Otro'],
    appContract: ['Indefinido', 'Fijo', 'Prestación de servicios', 'Otro'],
    coContract: ['Indefinido', 'Fijo', 'Prestación de servicios', 'Otro'],
    coIdType: ['CC', 'CE', 'PAS']
  };
  if (choices[name]) {
    const select = document.createElement('select');
    select.name = name;
    select.append(new Option('Selecciona una opción', ''));
    choices[name].forEach(value => select.append(new Option(value, value)));
    wrapper.append(caption, select);
  } else wrapper.append(caption, input);
  return wrapper;
}

function fieldGrid(fields) {
  const grid = document.createElement('div');
  grid.className = 'application-fields';
  fields.forEach(field => grid.append(makeField(field)));
  return grid;
}

function panel(title, description, fields) {
  const section = document.createElement('section');
  section.className = 'form-block';
  const heading = document.createElement('div');
  heading.className = 'block-heading';
  const h = document.createElement('h3');
  h.textContent = title;
  const p = document.createElement('p');
  p.textContent = description;
  heading.append(h, p);
  section.append(heading, fieldGrid(fields));
  return section;
}

function expandable(title, description, fields) {
  const details = document.createElement('details');
  details.className = 'extra-details';
  const summary = document.createElement('summary');
  const copy = document.createElement('span');
  const strong = document.createElement('strong');
  strong.textContent = title;
  const small = document.createElement('small');
  small.textContent = description;
  copy.append(strong, small);
  const plus = document.createElement('b');
  plus.textContent = '+';
  plus.setAttribute('aria-hidden', 'true');
  summary.append(copy, plus);
  details.append(summary, fieldGrid(fields));
  return details;
}

const [personalFields, workFields, referenceFields, coFields] = window.solicitudCampos.map(x => x.fields);
wizardPages[1].append(expandable('Identidad, vivienda y familia',
  'Datos complementarios para el formato impreso', personalFields));
wizardPages[2].append(panel('Situación laboral', 'Completa lo que aplique a tu actividad.', workFields.slice(0, 4)));
wizardPages[2].append(expandable('Más información económica',
  'Empleador, contrato, otros ingresos y patrimonio', workFields.slice(4)));
wizardPages[3].append(panel('Referencia familiar', 'Una persona de tu familia.', referenceFields.slice(0, 3)));
wizardPages[3].append(panel('Referencia personal', 'Una persona de confianza.', referenceFields.slice(6, 9)));
wizardPages[3].append(expandable('Datos de contacto adicionales',
  'Direcciones y correos de tus referencias', [...referenceFields.slice(3, 6), ...referenceFields.slice(9)]));

const coChoice = document.createElement('div');
coChoice.className = 'choice-cards';
coChoice.setAttribute('role', 'radiogroup');
coChoice.setAttribute('aria-label', '¿Tienes codeudor?');
coChoice.innerHTML = '<label><input type="radio" name="hasCodebtor" value="no" checked><span><strong>No tengo codeudor</strong><small>Continuar sin agregar otra persona</small></span></label><label><input type="radio" name="hasCodebtor" value="yes"><span><strong>Sí tengo codeudor</strong><small>Agregar sus datos</small></span></label>';
wizardPages[4].append(coChoice);
const coContent = document.createElement('div');
coContent.className = 'codebtor-content';
coContent.hidden = true;
const basicNames = ['coName', 'coIdType', 'coId', 'coEmail', 'coPhone'];
const coBasic = coFields.filter(([name]) => basicNames.includes(name));
const coHome = coFields.slice(3, 20).filter(([name]) => !basicNames.includes(name));
const coWork = coFields.slice(20, 39);
const coReferences = coFields.slice(39);
coContent.append(panel('Datos básicos del codeudor', 'Identidad y contacto.', coBasic));
coContent.append(expandable('Identidad, vivienda y familia', 'Información personal adicional', coHome));
coContent.append(expandable('Trabajo y finanzas', 'Empleo, ingresos y patrimonio', coWork));
coContent.append(expandable('Referencias del codeudor', 'Familiar y personal', coReferences));
wizardPages[4].append(coContent);
for (const name of ['coName', 'coIdType', 'coId']) form.elements[name].required = false;
function updateCodebtor() {
  const enabled = form.elements.hasCodebtor.value === 'yes';
  coContent.hidden = !enabled;
  for (const field of coContent.querySelectorAll('input,select')) {
    field.disabled = !enabled;
    if (!enabled) field.value = '';
  }
  for (const name of ['coName', 'coIdType', 'coId']) form.elements[name].required = enabled;
}
coChoice.addEventListener('change', updateCodebtor);
updateCodebtor();

const review = document.createElement('div');
review.className = 'review-card';
supportSection.before(review);
const nav = document.createElement('nav');
nav.className = 'wizard-nav';
nav.setAttribute('aria-label', 'Etapas de la solicitud');
nav.replaceChildren(...steps.map(([title], index) => {
  const button = document.createElement('button');
  button.type = 'button';
  button.innerHTML = `<span>${String(index + 1).padStart(2, '0')}</span><strong>${title}</strong>`;
  button.addEventListener('click', () => showStep(index));
  return button;
}));
document.querySelector('.application-panel').insertBefore(nav, form);
const actions = document.createElement('div');
actions.className = 'wizard-actions';
const previous = document.createElement('button');
previous.type = 'button';
previous.className = 'example-button';
previous.textContent = '← Anterior';
previous.addEventListener('click', () => showStep(currentStep - 1));
const hint = document.createElement('span');
hint.className = 'wizard-hint';
const next = document.createElement('button');
next.type = 'button';
next.className = 'primary';
next.textContent = 'Continuar →';
next.addEventListener('click', () => {
  validatePrimaryEmail();
  const invalid = wizardPages[currentStep].querySelector(':invalid');
  if (invalid) { invalid.focus(); invalid.reportValidity(); return; }
  showStep(currentStep + 1);
});
actions.append(previous, hint, next, submit);
form.insertBefore(actions, feedback);

function showStep(index) {
  if (index < 0 || index >= steps.length) return;
  currentStep = index;
  wizardPages.forEach((page, i) => page.hidden = i !== index);
  [...nav.children].forEach((button, i) => {
    button.classList.toggle('active', i === index);
    button.setAttribute('aria-current', i === index ? 'step' : 'false');
  });
  document.querySelector('#step-kicker').textContent = `Paso ${String(index + 1).padStart(2, '0')} / 06`;
  document.querySelector('#application-title').textContent = steps[index][0];
  document.querySelector('#step-description').textContent = steps[index][1];
  hint.textContent = `Paso ${index + 1} de ${steps.length}`;
  previous.hidden = index === 0;
  next.hidden = index === steps.length - 1;
  submit.hidden = index !== steps.length - 1;
  feedback.textContent = '';
  if (index === steps.length - 1) renderReview();
  document.querySelector('.application-panel').scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function renderReview() {
  review.replaceChildren();
  const lines = [
    ['Solicitante', form.elements.applicantName.value || 'Pendiente'],
    ['Producto', selectedProduct()?.productName || 'Pendiente'],
    ['Monto', form.elements.requestedAmount.value ? money(form.elements.requestedAmount.value) : 'Pendiente'],
    ['Cuotas', form.elements.requestedInstallments.value || 'Pendiente'],
    ['Codeudor', form.elements.hasCodebtor.value === 'yes' ? form.elements.coName.value || 'Pendiente' : 'No aplica']
  ];
  for (const [label, value] of lines) {
    const row = document.createElement('div');
    const title = document.createElement('span');
    const detail = document.createElement('strong');
    title.textContent = label;
    detail.textContent = value;
    row.append(title, detail);
    review.append(row);
  }
}

showStep(0);

function selectedProduct() {
  return products.find(item => item.productVersionId === productSelect.value);
}

function showLimits() {
  const product = selectedProduct();
  const label = document.querySelector('#product-limits');
  label.textContent = product
    ? `Monto permitido: ${money(product.minimumAmount)} a ${money(product.maximumAmount)} · ${product.minimumInstallments} a ${product.maximumInstallments} cuotas.`
    : '';
  if (product) {
    document.querySelector('#amount').min = product.minimumAmount;
    document.querySelector('#amount').max = product.maximumAmount;
    document.querySelector('#installments').min = product.minimumInstallments;
    document.querySelector('#installments').max = product.maximumInstallments;
  }
}

async function loadProducts() {
  try {
    const response = await fetch('/api/application-products', {cache: 'no-store'});
    if (!response.ok) throw new Error('El catálogo de productos no está disponible.');
    products.push(...await response.json());
    productSelect.replaceChildren();
    if (!products.length) throw new Error('No hay productos vigentes. Un administrador debe crear un producto y publicar sus condiciones en Productos.');
    for (const item of products) {
      const option = document.createElement('option');
      option.value = item.productVersionId;
      option.textContent = item.productName;
      productSelect.append(option);
    }
    showLimits();
  } catch (error) {
    feedback.textContent = error.message;
    productSelect.innerHTML = '<option value="">Sin productos disponibles</option>';
    submit.disabled = true;
  }
}

productSelect.addEventListener('change', showLimits);

form.addEventListener('submit', async event => {
  event.preventDefault();
  feedback.textContent = '';
  validatePrimaryEmail();
  const invalid = form.querySelector(':invalid');
  if (invalid) {
    const page = invalid.closest('.wizard-page');
    if (page) showStep(Number(page.dataset.step));
    for (let parent = invalid.closest('details'); parent; parent = parent.parentElement.closest('details'))
      parent.open = true;
    invalid.focus();
    invalid.reportValidity();
    feedback.textContent = 'Revisa el campo señalado para continuar.';
    return;
  }
  const product = selectedProduct();
  if (!product) { feedback.textContent = 'Selecciona un producto.'; return; }
  const payload = new FormData();
  payload.append('companyId', product.companyId);
  for (const name of ['productVersionId', 'identificationType', 'identificationNumber',
    'applicantName', 'email', 'phone', 'city', 'economicActivity', 'monthlyIncome',
    'monthlyObligations', 'requestedAmount', 'requestedInstallments', 'purpose']) {
    payload.append(name, form.elements[name].value.trim());
  }
  const formDetails = {};
  for (const group of window.solicitudCampos)
    for (const [name] of group.fields)
      formDetails[name] = form.elements[name].value.trim();
  payload.append('formDataJson', JSON.stringify(formDetails));
  payload.append('demoAcknowledged', form.elements.demoAcknowledged.checked ? 'true' : 'false');
  for (const kind of ['Identity', 'Income', 'Other']) {
    const file = form.elements[kind].files[0];
    if (!file) continue;
    if (file.size > 10 * 1024 * 1024) {
      feedback.textContent = `${file.name} supera 10 MB.`;
      return;
    }
    payload.append(kind, file);
  }
  submit.disabled = true;
  submit.textContent = 'Radicando…';
  try {
    const localDemo = ['127.0.0.1', 'localhost', '::1'].includes(location.hostname);
    const access = await window.zikloAccessReady;
    const companyId = access?.profile?.companies?.[0]?.companyId;
    if (!localDemo && !companyId)
      throw new Error('Para radicar pruebas internas, ingresa con una cuenta de radicador o administrador.');
    const endpoint = companyId
      ? `/api/companies/${encodeURIComponent(companyId)}/credit-applications/submit`
      : '/api/demo/credit-applications';
    const response = await zikloFetch(endpoint, { method: 'POST', body: payload });
    const data = await response.json();
    if (!response.ok) throw new Error(data.detail || data.title || 'No se pudo radicar la solicitud.');
    document.querySelector('#reference').textContent = data.reference;
    document.querySelector('#receipt-product').textContent = data.productName;
    document.querySelector('#receipt-amount').textContent = money(data.requestedAmount);
    document.querySelector('#receipt-installments').textContent = data.requestedInstallments;
    document.querySelector('#receipt-documents').textContent = data.documentCount;
    document.querySelector('#print-application').href =
      `/solicitud-impresa.html?radicado=${encodeURIComponent(data.reference)}`;
    document.querySelector('.application-layout').hidden = true;
    document.querySelector('#receipt').hidden = false;
    document.querySelector('#receipt').scrollIntoView({ behavior: 'smooth', block: 'start' });
  } catch (error) {
    feedback.textContent = error.message;
  } finally {
    submit.disabled = false;
    submit.textContent = 'Radicar solicitud';
  }
});

document.querySelector('#new-application').addEventListener('click', () => {
  form.reset();
  updateCodebtor();
  feedback.textContent = '';
  showLimits();
  document.querySelector('#receipt').hidden = true;
  document.querySelector('.application-layout').hidden = false;
  showStep(0);
  window.scrollTo({ top: 0, behavior: 'smooth' });
});

loadProducts();
