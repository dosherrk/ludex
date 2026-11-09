// Ludex — лендинг: счётчик скачиваний, переключение языка RU/EN, просмотр скриншотов.

(function () {
  'use strict';

  // ======================= счётчик скачиваний =======================
  // Данные берём из GitHub Releases: /api/downloads (кэш на edge) → при сбое
  // напрямую из api.github.com → при полном отсутствии сети просто прячем счётчик.

  var CACHE_KEY = 'ludex.dl.v1';
  var CACHE_TTL = 10 * 60 * 1000; // 10 минут, как s-maxage у /api/downloads
  var GITHUB_API = 'https://api.github.com/repos/dosherrk/ludex/releases?per_page=100';

  var stats = null;        // { total, kinds: { setup: {count,url}, … } }
  var animated = false;    // анимацию цифры проигрываем один раз

  function nf(n) {
    var loc = document.documentElement.lang === 'en' ? 'en-US' : 'ru-RU';
    try { return new Intl.NumberFormat(loc).format(n); } catch (e) { return String(n); }
  }

  function pluralRu(n) {
    var a = Math.abs(n) % 100;
    var b = a % 10;
    if (a > 10 && a < 20) return 'скачиваний';
    if (b === 1) return 'скачивание';
    if (b >= 2 && b <= 4) return 'скачивания';
    return 'скачиваний';
  }

  function wordFor(n) {
    if (document.documentElement.lang === 'en') return n === 1 ? 'download' : 'downloads';
    return pluralRu(n);
  }

  // Ludex-Setup-1.4.0.exe → setup, Ludex-1.4.0-portable.exe → portable, *.zip → zip
  function classify(name) {
    var n = String(name || '').toLowerCase();
    if (n.indexOf('.exe') === n.length - 4) {
      if (n.indexOf('setup') !== -1 || n.indexOf('installer') !== -1) return 'setup';
      if (n.indexOf('portable') !== -1) return 'portable';
      return null;
    }
    if (n.indexOf('.zip') === n.length - 4) return 'zip';
    return null;
  }

  // Запасной путь: если /api/downloads недоступен, считаем сами по ответу GitHub API.
  function fromGithub(releases) {
    var kinds = {};
    var total = 0;

    releases.forEach(function (rel) {
      if (!rel || rel.draft) return;
      (rel.assets || []).forEach(function (asset) {
        var kind = classify(asset.name);
        if (!kind) return;
        var count = Number(asset.download_count) || 0;
        total += count;
        if (!kinds[kind]) kinds[kind] = { count: 0, url: '', name: asset.name };
        kinds[kind].count += count;
        if (!kinds[kind].url) {
          kinds[kind].url = asset.browser_download_url;
          kinds[kind].name = asset.name;
        }
      });
    });

    return { available: true, total: total, kinds: kinds };
  }

  function readCache() {
    try {
      var raw = localStorage.getItem(CACHE_KEY);
      if (!raw) return null;
      var obj = JSON.parse(raw);
      if (!obj || typeof obj.t !== 'number' || !obj.v || typeof obj.v.total !== 'number') return null;
      return obj;
    } catch (e) { return null; }
  }

  function writeCache(v) {
    try { localStorage.setItem(CACHE_KEY, JSON.stringify({ t: Date.now(), v: v })); } catch (e) { /* приватный режим */ }
  }

  function animateNumber(el, target) {
    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduce || target < 20 || !window.requestAnimationFrame) {
      el.textContent = nf(target);
      return;
    }
    var duration = 700;
    var start = null;
    function step(now) {
      if (start === null) start = now;
      var p = Math.min(1, (now - start) / duration);
      var eased = 1 - Math.pow(1 - p, 3);
      el.textContent = nf(Math.round(target * eased));
      if (p < 1) requestAnimationFrame(step);
    }
    requestAnimationFrame(step);
  }

  // Отрисовка по уже полученным данным (paint(false) — без анимации, для смены языка).
  function paint(animate) {
    if (!stats || typeof stats.total !== 'number') return;

    var totalEl = document.getElementById('dl-total');
    var pill = document.getElementById('dl-stat');
    var labelEl = document.getElementById('dl-label');

    if (totalEl) {
      if (animate && !animated) animateNumber(totalEl, stats.total);
      else totalEl.textContent = nf(stats.total);
    }

    if (labelEl) labelEl.textContent = wordFor(stats.total);

    document.querySelectorAll('[data-count]').forEach(function (el) {
      var item = stats.kinds && stats.kinds[el.getAttribute('data-count')];
      if (item && typeof item.count === 'number') {
        el.textContent = nf(item.count);
        el.hidden = false;
      } else {
        el.hidden = true;
      }
    });

    // Заодно чиним ссылки: они всегда указывают на файлы самого свежего релиза,
    // даже если версия в разметке устарела.
    document.querySelectorAll('a[data-kind]').forEach(function (a) {
      var item = stats.kinds && stats.kinds[a.getAttribute('data-kind')];
      if (item && item.url) a.href = item.url;
    });

    if (pill) pill.hidden = false;
    animated = true;
  }

  function loadStats() {
    var cached = readCache();

    if (cached) {
      stats = cached.v;
      paint(true);
      if (Date.now() - cached.t < CACHE_TTL) return; // свежий кэш — в сеть не идём
    }

    fetch('/api/downloads', { headers: { Accept: 'application/json' } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (data) {
        if (data && data.available) {
          writeCache(data);
          stats = data;
          paint(true);
          return;
        }
        return fallbackToGithub();
      })
      .catch(function () { return fallbackToGithub(); });
  }

  function fallbackToGithub() {
    if (stats) return; // кэш уже на экране — не дёргаем GitHub зря
    fetch(GITHUB_API, { headers: { Accept: 'application/vnd.github+json' } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (arr) {
        if (!Array.isArray(arr)) return;
        var data = fromGithub(arr);
        stats = data;
        writeCache(data);
        paint(true);
      })
      .catch(function () { /* нет сети — счётчик остаётся скрытым */ });
  }

  // ======================= язык =======================

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

    paint(false); // «скачивание/скачивания/скачиваний» и формат чисел зависят от языка

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

  loadStats();

  // ======================= лайтбокс =======================

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
