// Ludex — лендинг: счётчик скачиваний.
// Прокси к GitHub Releases API с кэшем на edge (s-maxage), чтобы не упираться
// в лимит 60 запросов/час и не светить токен в браузере.

const { configured, setMany } = require('./_lib/redis');

const REPO = 'dosherrk/ludex';

// Ключи кнопок: те же, что понимают /api/go и /api/clicks.
const KINDS = ['setup', 'portable', 'win-zip', 'linux-tar', 'linux-deb'];

// Тег релиза → сравнимое «число»: v1.21.4 и Ludex-Setup-1.21.4 считаются одной версией.
function versionKey(tag) {
  const m = String(tag || '').match(/(\d+)\.(\d+)\.(\d+)/);
  if (!m) return String(tag || '');
  return m[1] + '.' + m[2] + '.' + m[3];
}

// Имена файлов релиза → ключи кнопок на сайте.
// sources.zip и прочее, что не скачивают кнопками, не считаем.
function classify(name) {
  const n = String(name || '').toLowerCase();
  if (n.includes('sources')) return null;

  if (n.endsWith('.deb')) return 'linux-deb';
  if (n.includes('.tar.gz')) return 'linux-tar';
  // файлов для macOS в релизах больше нет — .dmg/.zip с «macos» не считаем
  if (n.includes('macos')) return null;
  if (n.endsWith('.zip')) {
    if (n.includes('windows')) return 'win-zip';
    return null;
  }
  if (n.endsWith('.exe')) {
    if (n.includes('setup') || n.includes('installer')) return 'setup';
    if (n.includes('portable')) return 'portable';
    return null;
  }
  return null;
}

function build(releases) {
  const kinds = {};
  let total = 0;

  // Тег текущего релиза — того, что стоит первым в списке (он от новых к старым).
  // Файлы других версий на кнопки не попадают: подпись «Installer» без версии
  // означала бы, что человек скачивает не то, что видит в заголовке. Если файла
  // в текущем релизе ещё нет (сборка не догнала тег), кнопка просто остаётся
  // без ссылки и включится, когда файл появится.
  const head = releases.find((r) => r && !r.draft && !r.prerelease) || releases[0] || {};
  const headVersion = versionKey(head.tag_name);

  for (const rel of releases) {
    if (!rel || rel.draft) continue;
    if (versionKey(rel.tag_name) !== headVersion) continue;
    for (const asset of rel.assets || []) {
      const kind = classify(asset.name);
      if (!kind) continue;
      const count = Number(asset.download_count) || 0;
      total += count;
      if (!kinds[kind]) kinds[kind] = { count: 0, url: '', name: asset.name };
      kinds[kind].count += count;
      if (!kinds[kind].url) {
        kinds[kind].url = asset.browser_download_url;
        kinds[kind].name = asset.name;
      }
    }
  }

  return {
    available: true,
    total,
    releases: releases.filter((r) => r && !r.draft).length,
    version: head.tag_name || '',
    updated: new Date().toISOString(),
    kinds,
  };
}

module.exports = async function handler(req, res) {
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  // 10 минут на edge + час «протухшее отдаём, обновляем в фоне»
  res.setHeader('Cache-Control', 'public, s-maxage=600, stale-while-revalidate=3600');

  const headers = {
    Accept: 'application/vnd.github+json',
    'User-Agent': 'ludex-landing',
  };
  if (process.env.GITHUB_TOKEN) headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;

  try {
    const r = await fetch(`https://api.github.com/repos/${REPO}/releases?per_page=100`, {
      headers,
    });
    if (!r.ok) throw new Error(`github ${r.status}`);

    const data = await r.json();
    if (!Array.isArray(data)) throw new Error('unexpected payload');

    const payload = build(data);

    // Пригодится /api/go: ссылки на файлы свежего релиза, чтобы переход
    // с кнопки не зависел от версии, вписанной в разметку. Ошибки не важны.
    if (configured()) {
      try {
        await setMany(
          Object.keys(payload.kinds).map((k) => [`url:${k}`, payload.kinds[k].url])
        );
      } catch (e) { /* без Redis страница работает как раньше */ }
    }

    res.status(200).send(JSON.stringify(payload));
  } catch (err) {
    // Счётчик — украшение, а не функциональность: при сбое просто просим
    // фронтенд его спрятать и не кэшируем ошибку надолго.
    res.setHeader('Cache-Control', 'public, s-maxage=60');
    res.status(200).send(
      JSON.stringify({ available: false, error: String((err && err.message) || err) })
    );
  }
};
