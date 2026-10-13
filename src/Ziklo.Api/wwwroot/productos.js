const $ = id => document.getElementById(id);
let companyId;
let currencyCode;
let selectedProduct;
let products = [];

const money = amount => new Intl.NumberFormat('es-CO', {
  style: 'currency', currency: currencyCode || 'COP', maximumFractionDigits: 2
}).format(amount);

function feedback(message, error = false) {
  $('feedback').textContent = message;
  $('feedback').style.color = error ? '#b4232f' : '#1f5baa';
}

async function api(path, options = {}) {
  const response = await zikloFetch(path, options);
  if (!response.ok) {
    let detail;
    try { const body = await response.json(); detail = body.detail || body.title; } catch {}
    throw new Error(detail || `No se pudo completar la operación (${response.status}).`);
  }
  return response.status === 204 ? null : response.json();
}

function productPath(suffix = '') {
  return `/api/companies/${encodeURIComponent(companyId)}/products${suffix}`;
}

async function loadProducts() {
  products = await api(productPath());
  const list = $('products');
  list.replaceChildren();
  if (!products.length) {
    const empty = document.createElement('p');
    empty.textContent = 'Todavía no hay productos. Crea uno y luego publica sus condiciones.';
    list.append(empty);
    $('conditions-panel').hidden = true;
    return;
  }
  for (const product of products) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'product-row';
    button.classList.toggle('selected', product.id === selectedProduct?.id);
    const name = document.createElement('strong');
    name.textContent = product.name;
    const detail = document.createElement('small');
    detail.textContent = `${product.code} · ${product.isActive ? 'Activo' : 'Inactivo'}`;
    button.append(name, detail);
    button.addEventListener('click', () => selectProduct(product));
    list.append(button);
  }
  if (selectedProduct) {
    selectedProduct = products.find(item => item.id === selectedProduct.id);
    if (!selectedProduct) $('conditions-panel').hidden = true;
  }
}

async function selectProduct(product) {
  selectedProduct = product;
  await loadProducts();
  $('conditions-title').textContent = `Condiciones de ${product.name}`;
  $('conditions-panel').hidden = false;
  const editForm = $('edit-product');
  editForm.elements.name.value = product.name;
  editForm.elements.description.value = product.description || '';
  editForm.elements.isActive.checked = product.isActive;
  const versions = await api(productPath(`/${encodeURIComponent(product.id)}/versions`));
  const list = $('versions');
  list.replaceChildren();
  if (!versions.length) {
    const empty = document.createElement('p');
    empty.textContent = 'Sin condiciones publicadas. Completa el formulario para que este producto aparezca en las solicitudes.';
    list.append(empty);
  }
  for (const version of versions) {
    const row = document.createElement('div');
    row.className = 'version-row';
    const title = document.createElement('strong');
    title.textContent = `Versión ${version.number} · ${version.isPublished ? 'Publicada' : 'Borrador'} · Desde ${version.validFrom}`;
    const detail = document.createElement('span');
    detail.textContent = `${money(version.minimumAmount)} a ${money(version.maximumAmount)} · ${version.minimumInstallments} a ${version.maximumInstallments} cuotas · Tasa ${(version.rate * 100).toLocaleString('es-CO')} % (${version.rateKind})`;
    row.append(title, detail);
    list.append(row);
  }
}

$('edit-product').addEventListener('submit', async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type=submit]');
  button.disabled = true;
  try {
    selectedProduct = await api(productPath(`/${encodeURIComponent(selectedProduct.id)}`), {
      method: 'PUT', headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({name: form.elements.name.value.trim(),
        description: form.elements.description.value.trim(),
        isActive: form.elements.isActive.checked})
    });
    await loadProducts();
    $('conditions-title').textContent = `Condiciones de ${selectedProduct.name}`;
    feedback('Producto actualizado.');
  } catch (error) { feedback(error.message, true); }
  finally { button.disabled = false; }
});

$('create-product').addEventListener('submit', async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type=submit]');
  button.disabled = true;
  try {
    const product = await api(productPath(), {
      method: 'POST', headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({code: form.elements.code.value.trim(),
        name: form.elements.name.value.trim(), description: form.elements.description.value.trim()})
    });
    form.reset();
    await loadProducts();
    await selectProduct(products.find(item => item.id === product.id));
    feedback('Producto creado. Publica sus condiciones para mostrarlo en el formulario de solicitud.');
  } catch (error) { feedback(error.message, true); }
  finally { button.disabled = false; }
});

$('create-version').addEventListener('submit', async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type=submit]');
  const amount = key => Number(form.elements[key].value);
  if (amount('maximumAmount') < amount('minimumAmount') ||
      amount('maximumInstallments') < amount('minimumInstallments')) {
    feedback('El monto y plazo máximos deben ser mayores o iguales a los mínimos.', true);
    return;
  }
  button.disabled = true;
  try {
    await api(productPath(`/${encodeURIComponent(selectedProduct.id)}/versions`), {
      method: 'POST', headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({
        minimumAmount: amount('minimumAmount'), maximumAmount: amount('maximumAmount'),
        minimumInstallments: amount('minimumInstallments'), maximumInstallments: amount('maximumInstallments'),
        rate: amount('rate') / 100, rateKind: form.elements.rateKind.value,
        dayCount: form.elements.dayCount.value, frequency: form.elements.frequency.value,
        method: form.elements.method.value, currencyDecimals: 2,
        allowsCapitalization: false,
        insurancePerInstallment: amount('insurancePerInstallment'),
        otherChargePerInstallment: amount('otherChargePerInstallment'),
        validFrom: form.elements.validFrom.value, currencyCode,
        lateAnnualRate: amount('lateAnnualRate') / 100,
        lateGraceDays: amount('lateGraceDays'), lateGraceMode: 'Prospective'
      })
    });
    await selectProduct(selectedProduct);
    feedback('Condiciones publicadas. El producto aparecerá en la solicitud desde la fecha de vigencia.');
  } catch (error) { feedback(error.message, true); }
  finally { button.disabled = false; }
});

(async () => {
  try {
    const access = await window.zikloAccessReady;
    if (!access) return;
    const company = access.profile.companies?.[0];
    if (!company) throw new Error('Tu usuario no tiene una empresa activa.');
    companyId = company.companyId;
    currencyCode = company.currencyCode;
    $('create-version').elements.validFrom.value = new Date().toISOString().slice(0, 10);
    await loadProducts();
  } catch (error) { feedback(error.message, true); }
})();
