// Ludex — лендинг: переход по кнопке скачивания со счётчиком кликов.
// GET /api/go?f=setup|portable|zip → 302 на файл самого свежего релиза.
// Клик пишется в Upstash Redis (если он подключён) и не задерживает переход.

const { configured, incrMany, get } = require('./_lib/redis');

const KINDS = ['setup', 'portable', 'win-zip', 'linux-tar', 'linux-deb', 'macos-arm', 'macos-x64'];

// Запасные имена файлов — на случай, если Redis ещё пуст.
const FALLBACK = {
  setup: 'Ludex-Setup-1.5.0.exe',
  portable: 'Ludex-1.5.0-portable.exe',
  'win-zip': 'Ludex-1.5.0-windows.zip',
  'linux-tar': 'Ludex-1.5.0-linux-x64.tar.gz',
  'linux-deb': 'Ludex-1.5.0-linux-x64.deb',
  'macos-arm': 'Ludex-1.5.0-macos-arm64.zip',
  'macos-x64': 'Ludex-1.5.0-macos-x64.zip',
};

// Старые ссылки (кнопка «Архив» до 1.5.0) ведут на windows-архив.
const ALIASES = { zip: 'win-zip' };

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
