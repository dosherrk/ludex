// Ludex — лендинг: счётчик скачиваний.
// Прокси к GitHub Releases API с кэшем на edge (s-maxage), чтобы не упираться
// в лимит 60 запросов/час и не светить токен в браузере.

const REPO = 'dosherrk/ludex';

// Ludex-Setup-1.4.0.exe → setup, Ludex-1.4.0-portable.exe → portable, Ludex-1.4.0.zip → zip
function classify(name) {
  const n = String(name || '').toLowerCase();
  if (n.endsWith('.exe')) {
    if (n.includes('setup') || n.includes('installer')) return 'setup';
    if (n.includes('portable')) return 'portable';
    return null;
  }
  if (n.endsWith('.zip')) return 'zip';
  return null;
}

function build(releases) {
  const kinds = {};
  let total = 0;

  for (const rel of releases) {
    if (!rel || rel.draft) continue;
    for (const asset of rel.assets || []) {
      const kind = classify(asset.name);
      if (!kind) continue;
      const count = Number(asset.download_count) || 0;
      total += count;
      if (!kinds[kind]) kinds[kind] = { count: 0, url: '', name: asset.name };
      kinds[kind].count += count;
      // ссылка на самый свежий релиз (список приходит от новых к старым)
      if (!kinds[kind].url) {
        kinds[kind].url = asset.browser_download_url;
        kinds[kind].name = asset.name;
      }
    }
  }

  const head = releases.find((r) => r && !r.draft && !r.prerelease) || releases[0] || {};

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

    res.status(200).send(JSON.stringify(build(data)));
  } catch (err) {
    // Счётчик — украшение, а не функциональность: при сбое просто просим
    // фронтенд его спрятать и не кэшируем ошибку надолго.
    res.setHeader('Cache-Control', 'public, s-maxage=60');
    res.status(200).send(
      JSON.stringify({ available: false, error: String((err && err.message) || err) })
    );
  }
};
