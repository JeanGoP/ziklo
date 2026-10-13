(() => {
  const signedIn = Boolean(sessionStorage.getItem('zikloAccessToken'));
  for (const preview of document.querySelectorAll('.preview')) {
    if (preview.hasAttribute('data-protected') && !signedIn) continue;
    const frame = preview.querySelector('iframe[data-live-src]');
    if (!frame) continue;
    frame.addEventListener('load', () => {
      try {
        if (frame.contentWindow.location.pathname !== new URL(frame.dataset.liveSrc, location.href).pathname) {
          preview.querySelector('.preview-message').textContent = 'Este perfil no tiene acceso a la pantalla. Usa «Abrir pantalla» con el usuario correspondiente.';
          return;
        }
      } catch {
        preview.querySelector('.preview-message').textContent = 'No se pudo mostrar esta vista. Usa «Abrir pantalla».';
        return;
      }
      preview.classList.add('loaded');
    });
    frame.src = frame.dataset.liveSrc;
  }
})();
