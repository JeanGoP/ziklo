// Entradas monetarias en formato colombiano: 1.234.567,89.
window.ZikloMoney = (() => {
  function clean(value, maxDecimals = 2) {
    const source = String(value ?? '').trim().replace(/\s|\$/g, '');
    if (!source) return '';
    const lastComma = source.lastIndexOf(',');
    const lastDot = source.lastIndexOf('.');
    let decimalIndex = -1;
    if (lastComma >= 0 && lastDot >= 0)
      decimalIndex = Math.max(lastComma, lastDot);
    else if (lastComma >= 0)
      decimalIndex = source.indexOf(',') === lastComma ? lastComma : -1;
    else if (lastDot >= 0 && !/^\d{1,3}(\.\d{3})+$/.test(source))
      decimalIndex = lastDot;
    const integer = (decimalIndex < 0 ? source : source.slice(0, decimalIndex))
      .replace(/\D/g, '').replace(/^0+(?=\d)/, '');
    const decimals = decimalIndex < 0 ? '' : source.slice(decimalIndex + 1)
      .replace(/\D/g, '');
    if (!integer && decimalIndex < 0) return '';
    return `${(integer || '0').replace(/\B(?=(\d{3})+(?!\d))/g, '.')}` +
      (decimalIndex < 0 ? '' : `,${decimals}`);
  }

  function parse(value) {
    const maxDecimals = typeof value === 'object' && value !== null
      ? Number(value.dataset.decimals || 2) : 2;
    const formatted = clean(typeof value === 'object' && value !== null ? value.value : value,
      maxDecimals);
    if (!formatted || !/^\d{1,3}(\.\d{3})*(,\d*)?$/.test(formatted) ||
        (formatted.split(',')[1]?.length || 0) > maxDecimals) return NaN;
    return Number(formatted.replace(/\./g, '').replace(',', '.'));
  }

  function validate(input) {
    if (input.disabled) { input.setCustomValidity(''); return true; }
    if (!input.value.trim()) { input.setCustomValidity(''); return !input.required; }
    const value = parse(input);
    const minimum = input.dataset.min === undefined ? 0 : Number(input.dataset.min);
    const maximum = input.dataset.max === undefined ? Infinity : Number(input.dataset.max);
    const valid = Number.isFinite(value) && value >= minimum && value <= maximum;
    input.setCustomValidity(valid ? '' : Number.isNaN(value)
      ? `Ingresa un número válido con máximo ${input.dataset.decimals || 2} decimales.`
      : `Ingresa un valor entre ${clean(minimum)} y ${Number.isFinite(maximum) ? clean(maximum) : 'el máximo permitido'}.`);
    return valid;
  }

  function formatInput(input) {
    const previous = input.value;
    const cursor = input.selectionStart ?? previous.length;
    const digitsBefore = previous.slice(0, cursor).replace(/\D/g, '').length;
    const commaBefore = previous.slice(0, cursor).includes(',');
    const formatted = clean(previous, Number(input.dataset.decimals || 2));
    input.value = formatted;
    if (document.activeElement === input && input.setSelectionRange) {
      let position = 0, digits = 0;
      while (position < formatted.length && digits < digitsBefore) {
        if (/\d/.test(formatted[position])) digits++;
        position++;
      }
      if (commaBefore && formatted.includes(','))
        position = Math.max(position, formatted.indexOf(',') + 1);
      input.setSelectionRange(position, position);
    }
    validate(input);
  }

  function formatList(input) {
    const previous = input.value;
    const cursor = input.selectionStart ?? previous.length;
    const format = value => value.split(';').map(part => clean(part.trim())).join('; ');
    input.value = format(previous);
    if (document.activeElement === input && input.setSelectionRange) {
      const position = format(previous.slice(0, cursor)).length;
      input.setSelectionRange(position, position);
    }
  }

  function bind(root = document) {
    for (const input of root.querySelectorAll('[data-money]')) {
      if (input.dataset.moneyBound) continue;
      input.dataset.moneyBound = 'true';
      input.addEventListener('input', () => formatInput(input));
      input.addEventListener('blur', () => formatInput(input));
      formatInput(input);
    }
    for (const input of root.querySelectorAll('[data-money-list]')) {
      if (input.dataset.moneyBound) continue;
      input.dataset.moneyBound = 'true';
      input.addEventListener('input', () => formatList(input));
      input.addEventListener('blur', () => formatList(input));
      formatList(input);
    }
  }

  function validateAll(root) {
    return [...root.querySelectorAll('[data-money]')].map(validate).every(Boolean);
  }

  function apiValue(input) {
    const value = parse(input);
    return Number.isFinite(value) ? String(value) : '';
  }

  bind();
  return {bind, clean, parse, validate, validateAll, apiValue};
})();
