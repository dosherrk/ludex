// Ludex — лендинг: переключение языка RU/EN и просмотр скриншотов.

(function () {
  'use strict';

  // ---------- язык ----------
  var STORAGE_KEY = 'ludex.lang';
  var toggle = document.getElementById('lang-toggle');

  function applyLang(lang) {
    var isEn = lang === 'en';

    document.documentElement.lang = isEn ? 'en' : 'ru';

    document.querySelectorAll('[data-ru]').forEach(function (el) {
      var text = el.getAttribute(isEn ? 'data-en' : 'data-ru');
      if (text) el.textContent = text;
    });

    if (toggle) toggle.textContent = isEn ? 'RU' : 'EN';

    try { localStorage.setItem(STORAGE_KEY, lang); } catch (e) { /* приватный режим */ }
  }

  var saved = null;
  try { saved = localStorage.getItem(STORAGE_KEY); } catch (e) { /* ignore */ }

  // ?lang=ru / ?lang=en — ссылка сразу на нужном языке
  var fromUrl = new URLSearchParams(location.search).get('lang');
  if (fromUrl === 'ru' || fromUrl === 'en') saved = fromUrl;

  if (!saved) {
    // первый заход: если браузер не русский — показываем английский
    saved = (navigator.language || 'ru').toLowerCase().indexOf('ru') === 0 ? 'ru' : 'en';
  }

  applyLang(saved);

  if (toggle) {
    toggle.addEventListener('click', function () {
      applyLang(document.documentElement.lang === 'en' ? 'ru' : 'en');
    });
  }

  // ---------- лайтбокс ----------
  var box = document.getElementById('lightbox');
  var boxImg = document.getElementById('lightbox-img');
  var closeBtn = document.getElementById('lightbox-close');

  function open(src, alt) {
    if (!box || !boxImg) return;
    boxImg.src = src;
    boxImg.alt = alt || '';
    box.hidden = false;
    document.body.style.overflow = 'hidden';
  }

  function close() {
    if (!box) return;
    box.hidden = true;
    boxImg.removeAttribute('src');
    document.body.style.overflow = '';
  }

  document.querySelectorAll('.shot').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var img = btn.querySelector('img');
      open(btn.getAttribute('data-full'), img ? img.alt : '');
    });
  });

  if (box) box.addEventListener('click', function (e) { if (e.target === box) close(); });
  if (closeBtn) closeBtn.addEventListener('click', close);
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && box && !box.hidden) close();
  });
})();
