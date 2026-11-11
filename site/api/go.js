// Ludex — лендинг: переход по кнопке скачивания со счётчиком кликов.
// GET /api/go?f=setup|win-zip|linux-tar|linux-deb
// → 302 на файл самого свежего релиза.
// Клик пишется в Upstash Redis (если он подключён) и не задерживает переход.

const { configured, incrMany, get } = require('./_lib/redis');

const KINDS = ['setup', 'win-zip', 'linux-tar', 'linux-deb'];

// Запасные имена файлов — на случай, если Redis ещё пуст.
const FALLBACK = {
  setup: 'Ludex-Setup-1.20.0.exe',
  'win-zip': 'Ludex-1.20.0-windows.zip',
  'linux-tar': 'Ludex-1.20.0-linux-x64.tar.gz',
  'linux-deb': 'Ludex-1.20.0-linux-x64.deb',
};

// Старые ссылки: «Архив» до 1.5.0 и кнопка «Портативный» до 1.19.4
// (раньше это был один exe, теперь — архив с файлами рядом с Ludex.exe).
const ALIASES = { zip: 'win-zip', portable: 'win-zip' };

function fallbackUrl(kind) {
  return `https://github.com/dosherrk/ludex/releases/latest/download/${FALLBACK[kind]}`;
}

module.exports = async function handler(req, res) {
  let kind = String((req.query && req.query.f) || '').toLowerCase();
  if (ALIASES[kind]) kind = ALIASES[kind];
  const at = KINDS.indexOf(kind);

  if (at === -1) {
    res.statusCode = 404;
    res.setHeader('Content-Type', 'text/plain; charset=utf-8');
    return res.end('unknown file');
  }

  // Куда вести: ссылка, сохранённая /api/downloads (всегда свежий релиз),
  // иначе — запасная ссылка на текущую версию.
  let target = fallbackUrl(kind);
  const tasks = [];

  if (configured()) {
    tasks.push(
      get(`url:${kind}`)
        .then((url) => { if (url && /^https:\/\//.test(url)) target = url; })
        .catch(() => {}),
      incrMany([`clicks:${kind}`, 'clicks:total']).catch(() => {})
    );
    // Ждём не дольше 1.5 с — переход важнее статистики.
    await Promise.race([Promise.all(tasks), new Promise((r) => setTimeout(r, 1500))]);
  }

  res.statusCode = 302;
  res.setHeader('Location', target);
  res.setHeader('Cache-Control', 'no-store, max-age=0');
  res.setHeader('X-Robots-Tag', 'noindex');
  res.end();
};
