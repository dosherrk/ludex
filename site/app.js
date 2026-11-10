// Ludex — лендинг: счётчик скачиваний, переключение языка RU/EN, просмотр скриншотов.

(function () {
  'use strict';

  // ======================= счётчик скачиваний =======================
  // Два источника:
  //   /api/downloads — download_count файлов из GitHub Releases (серверный кэш 10 мин);
//                   именно он идёт на кнопки: цифра = сколько раз файл скачали;
  //   /api/clicks    — клики по кнопкам на самом сайте (Upstash Redis, кэш 30 с).
  // Всё декоративно: нет данных — счётчик просто не показывается.

  var GH_KEY = 'ludex.gh.v2';
  var CLICKS_KEY = 'ludex.clicks.v1';
  var GH_TTL = 10 * 60 * 1000;
  var CLICKS_TTL = 30 * 1000;
  var GITHUB_API = 'https://api.github.com/repos/dosherrk/ludex/releases?per_page=100';

  var gh = null;        // { total, kinds: { kind: { count } } }
  var clicks = null;    // { total, kinds: { kind: { clicks } } }
  var animated = false;

  function nf(n) {
    var loc = document.documentElement.lang === 'en' ? 'en-US' : 'ru-RU';
    try { return new Intl.NumberFormat(loc).format(n); } catch (e) { return String(n); }
  }

  function isEn() { return document.documentElement.lang === 'en'; }

  // «1 скачивание / 2 скачивания / 5 скачиваний»
  function plural(n, forms) { // forms: [one, few, many]
    var a = Math.abs(n) % 100;
    var b = a % 10;
    if (a > 10 && a < 20) return forms[2];
    if (b === 1) return forms[0];
    if (b >= 2 && b <= 4) return forms[1];
    return forms[2];
  }

  function wordDownloads(n) { return isEn() ? (n === 1 ? 'download' : 'downloads') : plural(n, ['скачивание', 'скачивания', 'скачиваний']); }
  function wordClicks(n) { return isEn() ? (n === 1 ? 'click' : 'clicks') : plural(n, ['клик', 'клика', 'кликов']); }

  // Имена файлов релиза → ключи кнопок.
  // Ludex-Setup-1.15.0.exe → setup, Ludex-1.15.0-linux-x64.deb → linux-deb и т.д.
  function classify(name) {
    var n = String(name || '').toLowerCase();
    if (n.indexOf('sources') !== -1) return null;

    if (n.indexOf('.deb') === n.length - 4) return 'linux-deb';
    if (n.indexOf('.tar.gz') !== -1) return 'linux-tar';
    if (n.indexOf('.dmg') === n.length - 4) {
      // кнопки macOS ведут на .dmg; .zip остаётся в релизе как альтернатива
      if (n.indexOf('arm64') !== -1) return 'macos-arm';
      if (n.indexOf('x64') !== -1 || n.indexOf('intel') !== -1) return 'macos-x64';
      return null;
    }
    if (n.indexOf('.zip') === n.length - 4) {
      if (n.indexOf('macos') !== -1) return null;
      if (n.indexOf('windows') !== -1) return 'win-zip';
      return null;
    }
    if (n.indexOf('.exe') === n.length - 4) {
      if (n.indexOf('setup') !== -1 || n.indexOf('installer') !== -1) return 'setup';
      if (n.indexOf('portable') !== -1) return 'portable';
      return null;
    }
    return null;
  }

  // Запасной путь для статистики GitHub: считаем сами по ответу api.github.com.
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

  function readCache(key) {
    try {
      var raw = localStorage.getItem(key);
      if (!raw) return null;
      var obj = JSON.parse(raw);
      if (!obj || typeof obj.t !== 'number' || !obj.v) return null;
      return obj;
    } catch (e) { return null; }
  }

  function writeCache(key, v) {
    try { localStorage.setItem(key, JSON.stringify({ t: Date.now(), v: v })); } catch (e) { /* приватный режим */ }
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

  function htmlEscape(str) {
    return String(str).replace(/[&<>"]/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c];
    });
  }

  // Отрисовка по уже полученным данным (paint(false) — без анимации, для смены языка).
  function paint(animate) {
    if (!gh && !clicks) return;

    var pill = document.getElementById('dl-stat');
    var box = document.getElementById('dl-stat-text');

    if (box) {
      var parts = [];

      if (gh && typeof gh.total === 'number') {
        parts.push(
          '<b class="dl-stat-num' + (animate && !animated ? ' dl-anim' : '') + '">' +
            htmlEscape(nf(gh.total)) + '</b> ' + wordDownloads(gh.total) +
            (clicks ? (isEn() ? ' on GitHub' : ' на GitHub') : '')
        );
      }

      if (clicks && typeof clicks.total === 'number' && (clicks.total > 0 || !gh)) {
        if (parts.length) parts.push('<span class="dl-stat-sep">·</span>');
        parts.push(
          '<b class="dl-stat-num">' + htmlEscape(nf(clicks.total)) + '</b> ' +
            wordClicks(clicks.total) + (isEn() ? ' on this site' : ' с сайта')
        );
      }

      if (!clicks && parts.length) {
        parts.push('<span class="dl-stat-note">' + (isEn() ? 'from GitHub Releases' : 'с GitHub Releases') + '</span>');
      }

      box.innerHTML = parts.join(' ');
    }

    // Счётчики на кнопках: реальные скачивания файла с GitHub, а если Releases API
    // недоступен — клики по кнопке с сайта. Клик ≠ скачивание, поэтому клики —
    // только запасной вариант и отдельная строка в общей статистике.
    document.querySelectorAll('[data-count]').forEach(function (el) {
      var kind = el.getAttribute('data-kind') || el.getAttribute('data-count');
      var fromClicks = clicks && clicks.kinds && clicks.kinds[kind];
      var fromGh = gh && gh.kinds && gh.kinds[kind];
      var value = null;
      var caption = '';

      if (fromGh && typeof fromGh.count === 'number') {
        // ноли на кнопках не показываем — файл ещё никто не скачал
        value = fromGh.count || null;
        caption = wordDownloads(fromGh.count) + (isEn() ? ' on GitHub' : ' с GitHub');
      } else if (fromClicks && typeof fromClicks.clicks === 'number') {
        value = fromClicks.clicks || null;
        caption = wordClicks(fromClicks.clicks) + (isEn() ? ' on this site' : ' с сайта');
      }

      if (value === null) {
        el.hidden = true;
        return;
      }
      el.textContent = nf(value);
      el.title = nf(value) + ' ' + caption;
      el.hidden = false;
    });

    if (pill) pill.hidden = false;

    if (animate) {
      var anim = box && box.querySelector('.dl-anim');
      if (anim && gh) {
        anim.classList.remove('dl-anim');
        animateNumber(anim, gh.total);
      }
    }

    animated = true;
  }

  function loadGithub() {
    var cached = readCache(GH_KEY);
    if (cached) {
      gh = cached.v;
      paint(!animated);
      if (Date.now() - cached.t < GH_TTL) return;
    }

    fetch('/api/downloads', { headers: { Accept: 'application/json' } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (data) {
        if (data && data.available && typeof data.total === 'number') {
          gh = data;
          writeCache(GH_KEY, data);
          paint(true);
        } else if (!gh) {
          return githubFallback();
        }
      })
      .catch(function () { return githubFallback(); });
  }

  function githubFallback() {
    if (gh) return;
    fetch(GITHUB_API, { headers: { Accept: 'application/vnd.github+json' } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (arr) {
        if (!Array.isArray(arr)) return;
        gh = fromGithub(arr);
        writeCache(GH_KEY, gh);
        paint(true);
      })
      .catch(function () { /* нет сети — остаёмся без этой цифры */ });
  }

  function loadClicks() {
    var cached = readCache(CLICKS_KEY);
    if (cached) {
      clicks = cached.v;
      paint(false);
      if (Date.now() - cached.t < CLICKS_TTL) return;
    }

    fetch('/api/clicks', { headers: { Accept: 'application/json' } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (data) {
        if (data && data.available) {
          clicks = data;
          writeCache(CLICKS_KEY, data);
          paint(false);
        }
      })
      .catch(function () { /* трекер недоступен — показываем данные GitHub */ });
  }

  // ======================= выбор системы =======================
  // Определяем ОС браузера, подставляем её кнопки, но всегда можно переключить вручную.

  var OS_KEY = 'ludex.os';

  function detectOs() {
    var ua = (navigator.userAgent || '').toLowerCase();
    var platform = (navigator.platform || '').toLowerCase();

    if (ua.indexOf('mac') !== -1 || platform.indexOf('mac') !== -1) return 'macos';
    if (ua.indexOf('linux') !== -1 || platform.indexOf('linux') !== -1) return 'linux';
    if (ua.indexOf('android') !== -1) return 'linux';
    return 'windows';
  }

  function selectOs(os) {
    if (['windows', 'macos', 'linux'].indexOf(os) === -1) os = 'windows';

    document.querySelectorAll('.os-tab').forEach(function (tab) {
      tab.setAttribute('aria-selected', tab.getAttribute('data-os') === os ? 'true' : 'false');
    });

    document.querySelectorAll('[data-os-panel]').forEach(function (panel) {
      panel.hidden = panel.getAttribute('data-os-panel') !== os;
    });

    try { localStorage.setItem(OS_KEY, os); } catch (e) { /* приватный режим */ }
  }

  document.querySelectorAll('.os-tab').forEach(function (tab) {
    tab.addEventListener('click', function () { selectOs(tab.getAttribute('data-os')); });
  });

  var savedOs = null;
  try { savedOs = localStorage.getItem(OS_KEY); } catch (e) { /* ignore */ }

  // ?os=macos — ссылка сразу на нужную систему (удобно кидать другу)
  var fromUrlOs = new URLSearchParams(location.search).get('os');
  selectOs(fromUrlOs || savedOs || detectOs());

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

  loadGithub();
  loadClicks();

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
