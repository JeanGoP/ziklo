const params = new URLSearchParams(location.search);
const reference = params.get('radicado');
const $ = id => document.getElementById(id);

function group(title, fields, values) {
  const section = document.createElement('section');
  section.className = 'sheet-group';
  const heading = document.createElement('h2');
  heading.textContent = title.toUpperCase();
  const cells = document.createElement('div');
  cells.className = 'cells';
  for (const [key, label] of fields) {
    const cell = document.createElement('div');
    cell.className = 'cell';
    const caption = document.createElement('b');
    caption.textContent = label;
    const value = document.createElement('span');
    value.textContent = values[key] === null || values[key] === undefined ? '' : String(values[key]);
    cell.append(caption, value);
    cells.append(cell);
  }
  section.append(heading, cells);
  return section;
}

async function load() {
  if (!reference || !/^SOL-[A-Z0-9-]{8,35}$/.test(reference))
    throw new Error('Abre esta hoja desde el comprobante de una solicitud radicada.');
  const response = await fetch(`/api/demo/credit-applications/${encodeURIComponent(reference)}`);
  if (!response.ok) throw new Error('No se pudo cargar la solicitud para imprimir.');
  const application = await response.json();
  const details = JSON.parse(application.formDataJson || '{}');
  const company = application.name || 'Empresa';
  $('company-name').textContent = $('company-name-2').textContent = company;
  $('company-tax').textContent = application.taxIdentifier && application.taxIdentifier !== 'PENDIENTE'
    ? `NIT ${application.taxIdentifier}` : '';
  $('date').textContent = `Fecha: ${new Date(application.createdAtUtc).toLocaleDateString('es-CO')}`;
  $('reference').textContent = $('reference-2').textContent = `Radicado: ${application.reference}`;
  $('applicant-core').append(group('Datos del solicitante y crédito solicitado', [
    ['applicantName','Nombres y apellidos'], ['identificationType','Tipo de documento'],
    ['identificationNumber','Número de documento'], ['email','Correo electrónico'],
    ['phone','Celular'], ['city','Ciudad'], ['economicActivity','Actividad económica'],
    ['monthlyIncome','Ingresos mensuales (COP)'], ['monthlyObligations','Obligaciones mensuales (COP)'],
    ['requestedAmount','Valor solicitado (COP)'], ['requestedInstallments','Número de cuotas'],
    ['purpose','Destino del crédito']
  ], application));
  window.solicitudCampos.slice(0, 3).forEach(item =>
    $('applicant-details').append(group(item.title, item.fields, details)));
  $('codebtor-details').append(group('Datos del codeudor', window.solicitudCampos[3].fields, details));
  $('risk-text').textContent = `El solicitante y el codeudor podrán autorizar a ${company} a consultar y reportar información crediticia. La empresa incorporará aquí la versión aprobada de esta autorización antes del uso real.`;
  $('privacy-text').textContent = `El solicitante y el codeudor podrán autorizar a ${company} a tratar sus datos para estudiar la solicitud y gestionar la relación crediticia. La empresa incorporará aquí su texto y política vigentes antes del uso real.`;
  $('document').hidden = false;
}

$('print').addEventListener('click', () => window.print());
load().catch(error => { $('error').textContent = error.message; });
