const $ = id => document.getElementById(id);
const labels = {Submitted:'Radicada',InReview:'En estudio',NeedsInformation:'Falta información',Approved:'Aprobada',Rejected:'Rechazada',Withdrawn:'Retirada',Disbursed:'Desembolsada'};
const money = value => new Intl.NumberFormat('es-CO',{style:'currency',currency:'COP'}).format(value);
function businessToday() {
  const parts = new Intl.DateTimeFormat('en-US', {timeZone:'America/Bogota',
    year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(new Date());
  const value = type => parts.find(part => part.type === type).value;
  return `${value('year')}-${value('month')}-${value('day')}`;
}
let companyId, items = [], offset = 0, total = 0, selectedId = null;
let currentUserId;
let permissions = [];

async function api(path, options = {}) {
  const response = await zikloFetch(path, options);
  if (!response.ok) {
    let detail;
    try { const body = await response.json(); detail = body.detail || body.title; } catch {}
    throw new Error(detail || `No se pudo completar la operación (${response.status}).`);
  }
  return response.json();
}

function node(tag, value, className) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  element.textContent = value == null ? '—' : String(value);
  return element;
}

function fact(label, value) {
  const wrap = document.createElement('div');
  const name = node('dt', label), data = node('dd', value);
  wrap.append(name, data);
  return wrap;
}

function renderList() {
  $('applications').replaceChildren();
  if (!items.length) $('applications').append(node('p', 'No hay solicitudes con ese filtro.', 'study-muted'));
  for (const item of items) {
    const button = document.createElement('button');
    button.type = 'button'; button.className = 'application-item';
    button.setAttribute('aria-current', String(item.id === selectedId));
    button.append(node('strong', item.applicantName),
      node('small', `${item.reference} · ${labels[item.status] || item.status}`),
      node('span', money(item.requestedAmount)));
    button.addEventListener('click', () => showDetail(item.id));
    $('applications').append(button);
  }
  $('more').hidden = items.length >= total;
}

async function loadList(reset = true) {
  if (reset) {offset = 0; items = [];}
  const params = new URLSearchParams({skip:String(offset),take:'50'});
  if ($('search').value.trim()) params.set('search', $('search').value.trim());
  if ($('status-filter').value) params.set('status', $('status-filter').value);
  const result = await api(`/api/companies/${companyId}/credit-applications?${params}`);
  total = result.total;
  items.push(...result.items);
  offset = items.length;
  renderList();
  if (reset && !items.some(item => item.id === selectedId)) {
    selectedId = null;
    if (items.length) await showDetail(items[0].id);
    else $('detail').replaceChildren(node('p', 'No hay solicitudes con ese filtro.', 'study-empty'));
  }
}

async function loadMyAlerts() {
  const result = await api(`/api/companies/${companyId}/my-application-alerts`);
  const panel = $('my-alerts');
  panel.hidden = result.total === 0;
  const list = $('my-alert-list');
  list.replaceChildren();
  for (const alert of result.items) {
    const button = node('button', '', 'study-alert-item');
    button.type = 'button';
    button.append(node('strong', `${alert.reference} · ${alert.applicantName}`),
      node('span', alert.note || 'El analista necesita información adicional.'));
    button.addEventListener('click', () => showDetail(alert.applicationId)
      .catch(error => $('feedback').textContent = error.message));
    list.append(button);
  }
  if (result.total > result.items.length)
    list.append(node('small', `Hay ${result.total - result.items.length} avisos más. Busca el radicado en la bandeja.`, 'study-muted'));
}

function renderNextStep(a, documents, id, panel) {
  const guidance = {
    Submitted: 'Radicada. Está en la bandeja compartida. Un analista debe iniciar el estudio.',
    InReview: 'En estudio. El analista revisa datos y soportes; un aprobador distinto registra la decisión.',
    NeedsInformation: 'Falta información. El radicador puede responder aquí y adjuntar un PDF; la solicitud volverá al estudio.',
    Approved: a.loanId
      ? 'Préstamo aprobado. Adjunta el contrato firmado y registra el desembolso.'
      : 'Aprobada. Ahora se puede crear el préstamo vinculado.',
    Rejected: 'Rechazada. El motivo y el responsable aparecen en el historial.',
    Disbursed: 'Desembolsada. Consulta el préstamo vinculado para continuar con la cartera.',
    Withdrawn: 'Retirada. No hay acciones de estudio disponibles.'
  };
  const section = node('section', '', 'study-next');
  section.append(node('h3', 'Siguiente paso'),
    node('p', guidance[a.status] || 'Consulta el historial de esta solicitud.'));
  if (a.status === 'NeedsInformation' && !a.submittedByUserId)
    section.append(node('small', 'Esta solicitud anterior no tiene un radicador identificado. Contacta al solicitante por el canal acordado.', 'study-muted'));
  panel.append(section);

  if (a.status === 'NeedsInformation' && a.submittedByUserId === currentUserId) {
    const form = document.createElement('form');
    form.className = 'study-review';
    form.innerHTML = `<h3>Responder al analista</h3>
      <label>Información solicitada<textarea name="notes" minlength="10" maxlength="2000" required placeholder="Explica qué información aportas"></textarea></label>
      <label>Soporte adicional en PDF (opcional)<input name="supplement" type="file" accept="application/pdf,.pdf"></label>
      <button class="study-convert" type="submit">Enviar respuesta</button>`;
    form.addEventListener('submit', async event => {
      event.preventDefault();
      const notes = form.elements.notes.value.trim();
      const file = form.elements.supplement.files[0];
      if (notes.length < 10 || (file && (file.size > 10 * 1024 * 1024 ||
          !file.name.toLowerCase().endsWith('.pdf')))) {
        $('feedback').textContent = 'Escribe al menos 10 caracteres y adjunta solo PDF de máximo 10 MB.'; return;
      }
      const button = form.querySelector('button');
      button.disabled = true;
      try {
        if (file) await api(`/api/companies/${companyId}/credit-applications/${id}/supplements?fileName=${encodeURIComponent(file.name)}`,
          {method:'POST',headers:{'Content-Type':'application/pdf'},body:file});
        await api(`/api/companies/${companyId}/credit-applications/${id}/response`, {
          method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({notes})
        });
        $('feedback').textContent = 'Respuesta enviada. La solicitud volvió a la bandeja de estudio.';
        await Promise.all([loadList(), loadMyAlerts()]);
        await showDetail(id);
      } catch (error) { $('feedback').textContent = error.message; button.disabled = false; }
    });
    section.append(form);
    return;
  }

  if (a.status === 'Approved' && !a.loanId && permissions.includes('CreateLoan')) {
    const convert = node('button', 'Crear préstamo aprobado');
    convert.type = 'button'; convert.className = 'study-convert';
    convert.addEventListener('click', async () => {
      if (!confirm('Se creará un préstamo aprobado para esta solicitud. No se desembolsarán fondos. ¿Continuar?')) return;
      convert.disabled = true;
      try {
        await api(`/api/companies/${companyId}/credit-applications/${id}/loan?approvalDate=${businessToday()}`,
          {method:'POST'});
        $('feedback').textContent = 'Préstamo aprobado creado y vinculado.';
        await showDetail(id);
      } catch (error) { $('feedback').textContent = error.message; convert.disabled = false; }
    });
    section.append(convert);
    return;
  }

  if (a.status === 'Approved' && a.loanId) {
    const signedContract = documents.find(document => document.kind === 'SignedContract');
    if (!permissions.includes('DisburseLoan')) {
      section.append(node('small', signedContract
        ? 'El contrato está adjunto. Un usuario con permiso de desembolso debe continuar.'
        : 'Un usuario con permiso de desembolso debe adjuntar el contrato firmado y continuar.',
      'study-muted'));
      return;
    }
    if (!signedContract) {
      const form = document.createElement('form');
      form.className = 'study-review';
      form.innerHTML = `<h3>Contrato firmado</h3>
        <p>Adjunta el contrato aprobado por la empresa y firmado por el solicitante. Verifica su autenticidad antes de continuar.</p>
        <label>Archivo PDF firmado<input name="contract" type="file" accept="application/pdf,.pdf" required></label>
        <button class="study-convert" type="submit">Guardar contrato firmado</button>`;
      form.addEventListener('submit', async event => {
        event.preventDefault();
        const file = form.elements.contract.files[0];
        if (!file || file.size > 10 * 1024 * 1024 || !file.name.toLowerCase().endsWith('.pdf')) {
          $('feedback').textContent = 'Selecciona un PDF de máximo 10 MB.'; return;
        }
        const button = form.querySelector('button');
        button.disabled = true;
        try {
          await api(`/api/companies/${companyId}/credit-applications/${id}/signed-contract?fileName=${encodeURIComponent(file.name)}`,
            {method:'POST',headers:{'Content-Type':'application/pdf'},body:file});
          $('feedback').textContent = 'Contrato firmado guardado. Verifica las fechas antes de registrar el desembolso.';
          await showDetail(id);
        } catch (error) { $('feedback').textContent = error.message; button.disabled = false; }
      });
      section.append(form);
      return;
    }
    const form = document.createElement('form');
    form.className = 'study-review';
    form.innerHTML = `<h3>Registrar desembolso</h3>
      <p>Este registro activa el préstamo y genera su plan de cuotas. Confirma la entrega de fondos por el medio acordado antes de continuar.</p>
      <label>Fecha del desembolso<input name="disbursementDate" type="date" required></label>
      <label>Fecha de la primera cuota<input name="firstDueDate" type="date" required></label>
      <label class="study-confirm"><input name="confirmed" type="checkbox" required> Confirmo que se verificó el contrato firmado y la entrega de fondos.</label>
      <button class="study-convert" type="submit">Registrar desembolso</button>`;
    form.elements.disbursementDate.value = businessToday();
    form.elements.firstDueDate.value = (() => {
      const [year, month, day] = businessToday().split('-').map(Number);
      const first = new Date(Date.UTC(year, month, 1));
      const lastDay = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth() + 1, 0)).getUTCDate();
      return `${first.getUTCFullYear()}-${String(first.getUTCMonth() + 1).padStart(2, '0')}-${String(Math.min(day, lastDay)).padStart(2, '0')}`;
    })();
    const idempotencyKey = crypto.randomUUID();
    form.addEventListener('submit', async event => {
      event.preventDefault();
      const disbursementDate = form.elements.disbursementDate.value;
      const firstDueDate = form.elements.firstDueDate.value;
      if (firstDueDate <= disbursementDate) {
        $('feedback').textContent = 'La primera cuota debe ser posterior al desembolso.'; return;
      }
      if (!confirm('¿Confirmas que los fondos ya fueron entregados y deseas activar el préstamo?')) return;
      const button = form.querySelector('button');
      button.disabled = true;
      try {
        await api(`/api/companies/${companyId}/loans/${a.loanId}/disburse`, {
          method:'POST',headers:{'Content-Type':'application/json'},
          body:JSON.stringify({disbursementDate,firstDueDate,idempotencyKey})
        });
        $('feedback').textContent = 'Desembolso registrado. El préstamo ya tiene plan de cuotas.';
        await loadList(); await showDetail(id);
      } catch (error) { $('feedback').textContent = error.message; button.disabled = false; }
    });
    section.append(form);
    return;
  }

  const canStudy = permissions.includes('StudyApplications');
  const canDecide = permissions.includes('ApproveLoan');
  const choices = a.status === 'InReview'
    ? [
        ...(canStudy ? [['NeedsInformation','Solicitar información']] : []),
        ...(canDecide ? [['Approved','Aprobar'],['Rejected','Rechazar']] : [])
      ]
    : ['Submitted','NeedsInformation'].includes(a.status) && canStudy
      ? [['InReview','Iniciar o retomar estudio']] : [];
  if (!choices.length) {
    if (['Submitted','InReview','NeedsInformation'].includes(a.status))
      section.append(node('small', 'Tu perfil puede consultar esta solicitud, pero no tiene acciones disponibles en este estado.', 'study-muted'));
    return;
  }
  const form = document.createElement('form'); form.className = 'study-review';
  form.innerHTML = `<h3>Registrar actuación</h3>
    <label>Ingresos mensuales verificados<input name="income" type="number" min="0" step="0.01" value="${a.monthlyIncome}"></label>
    <label>Obligaciones mensuales verificadas<input name="obligations" type="number" min="0" step="0.01" value="${a.monthlyObligations}"></label>
    <label>Observaciones o motivo<textarea name="notes" maxlength="2000" placeholder="Deja constancia de la revisión"></textarea></label>
    <div class="study-actions"></div>`;
  const actions = form.querySelector('.study-actions');
  for (const [status, caption] of choices) {
    const button = node('button', caption); button.type = 'button';
    button.addEventListener('click', async () => {
      const notes = form.elements.notes.value.trim();
      if (['NeedsInformation','Rejected'].includes(status) && notes.length < 10) {
        $('feedback').textContent = 'Explica el motivo con al menos 10 caracteres.'; return;
      }
      if (['Approved','Rejected'].includes(status) &&
          !confirm(`¿Confirmas la decisión: ${labels[status]}?`)) return;
      button.disabled = true;
      try {
        await api(`/api/companies/${companyId}/credit-applications/${id}/reviews`, {
          method:'POST', headers:{'Content-Type':'application/json'},
          body:JSON.stringify({status,notes,
            verifiedMonthlyIncome: form.elements.income.value === '' ? null : Number(form.elements.income.value),
            verifiedMonthlyObligations: form.elements.obligations.value === '' ? null : Number(form.elements.obligations.value)})
        });
        $('feedback').textContent = status === 'NeedsInformation'
          ? a.submittedByUserId
            ? 'Se registró la solicitud de información. El radicador verá un aviso en Ziklo; no se envía correo automáticamente.'
            : 'Se registró la solicitud de información. Esta solicitud no tiene radicador identificado; contacta al solicitante por el canal acordado.'
          : 'Actuación registrada.';
        await loadList(); await showDetail(id);
      } catch (error) { $('feedback').textContent = error.message; button.disabled = false; }
    });
    actions.append(button);
  }
  section.append(form);
}

async function showDetail(id) {
  selectedId = id;
  renderList();
  const data = await api(`/api/companies/${companyId}/credit-applications/${id}`);
  const a = data.application, panel = $('detail');
  panel.replaceChildren();
  panel.append(node('div', a.reference, 'eyebrow'), node('h2', a.applicantName));
  const summary = node('div', '', 'study-summary');
  summary.append(node('span', labels[a.status] || a.status, 'study-badge'),
    node('span', new Date(a.createdAtUtc).toLocaleString('es-CO'), 'study-muted'));
  panel.append(summary);
  renderNextStep(a, data.documents, id, panel);
  const print = document.createElement('a');
  print.href = `/solicitud-impresa.html?radicado=${encodeURIComponent(a.reference)}`;
  print.target = '_blank'; print.rel = 'noopener';
  print.textContent = 'Ver solicitud en formato impreso ↗';
  panel.append(print);
  const facts = node('dl', '', 'study-facts');
  for (const [label, value] of [
    ['Documento', `${a.identificationType} ${a.identificationNumber}`],
    ['Correo', a.email], ['Teléfono', a.phone], ['Ciudad', a.city],
    ['Actividad', a.economicActivity], ['Ingresos declarados', money(a.monthlyIncome)],
    ['Obligaciones declaradas', money(a.monthlyObligations)],
    ['Monto solicitado', money(a.requestedAmount)], ['Cuotas', a.requestedInstallments],
    ['Destino', a.purpose]]) facts.append(fact(label, value));
  panel.append(facts);
  panel.append(node('h3', 'Soportes'));
  const documents = node('ul', '', 'study-history');
  if (!data.documents.length) documents.append(node('li', 'Sin soportes adjuntos.'));
  for (const support of data.documents) {
    const item = document.createElement('li'), link = document.createElement('a');
    link.href = `/api/companies/${companyId}/credit-applications/${id}/documents/${support.id}`;
    link.textContent = `${support.kind === 'SignedContract' ? 'Contrato firmado' :
      support.kind.startsWith('Sup-') ? 'Soporte adicional' : support.kind}: ${support.fileName}`;
    link.addEventListener('click', async event => {
      event.preventDefault();
      try {
        const response = await zikloFetch(link.href.replace(location.origin, ''));
        if (!response.ok) throw new Error(`No se pudo descargar el soporte (${response.status}).`);
        const url = URL.createObjectURL(await response.blob());
        const download = document.createElement('a');
        download.href = url; download.download = support.fileName;
        download.click();
        setTimeout(() => URL.revokeObjectURL(url), 60000);
      } catch (error) { $('feedback').textContent = error.message; }
    });
    item.append(link); documents.append(item);
  }
  panel.append(documents);
  panel.append(node('h3', 'Historial de estudio'));
  const history = node('ol', '', 'study-history');
  if (!data.reviews.length) history.append(node('li', 'Aún no hay revisiones.'));
  for (const review of data.reviews) {
    const item = document.createElement('li');
    item.append(node('strong', `${labels[review.previousStatus]} → ${labels[review.newStatus]}`),
      node('p', review.notes || 'Sin observaciones.'),
      node('small', `${review.actorEmail} · ${new Date(review.recordedAtUtc).toLocaleString('es-CO')}`));
    history.append(item);
  }
  panel.append(history);
  if (a.loanId) {
    const loan = await api(`/api/companies/${companyId}/loans/${a.loanId}`);
    panel.append(node('h3', 'Préstamo vinculado'),
      node('p', `${loan.number} · ${loan.status} · ${money(loan.originalPrincipal)}`));
    if (a.status === 'Disbursed') {
      panel.append(node('p', `Desembolsado: ${loan.disbursementDate} · Primera cuota: ${loan.firstDueDate}`));
      const schedule = document.createElement('a');
      schedule.href = `/recaudos.html?loan=${encodeURIComponent(loan.number)}`;
      schedule.textContent = 'Consultar préstamo y cuotas ↗';
      panel.append(schedule);
    }
  }
}

let searchTimer;
$('search').addEventListener('input', () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => loadList().catch(error => $('feedback').textContent = error.message), 250);
});
$('status-filter').addEventListener('change', () => loadList().catch(error => $('feedback').textContent = error.message));
$('refresh').addEventListener('click', () => Promise.all([loadList(), loadMyAlerts()])
  .catch(error => $('feedback').textContent = error.message));
$('more').addEventListener('click', () => loadList(false).catch(error => $('feedback').textContent = error.message));
(async () => {
  try {
    const session = await window.zikloAccessReady;
    const profile = session?.profile || await api('/api/auth/me');
    currentUserId = profile.id;
    companyId = profile.companies[0]?.companyId;
    if (!companyId) throw new Error('No tienes una empresa activa para revisar solicitudes.');
    permissions = session?.permissions
      ? [...session.permissions]
      : await api(`/api/companies/${companyId}/my-permissions`);
    $('workspace').hidden = false;
    await Promise.all([loadList(), loadMyAlerts()]);
    setInterval(() => {
      if (!document.hidden) loadMyAlerts().catch(() => {});
    }, 30000);
  } catch (error) { $('feedback').textContent = error.message; }
})();
