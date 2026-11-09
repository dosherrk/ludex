// Ludex — лендинг: клики по кнопкам скачивания с сайта (Upstash Redis).
// GET /api/clicks → { available, total, kinds: { setup: { clicks }, … } }

const { configured, mget } = require('./_lib/redis');

const KINDS = ['setup', 'portable', 'zip'];

function num(v) {
  const n = Number(v);
  return Number.isFinite(n) && n > 0 ? n : 0;
}

module.exports = async function handler(req, res) {
  res.setHeader('Content-Type', 'application/json; charset=utf-8');

  if (!configured()) {
    // Upstash ещё не подключён — фронтенд покажет только статистику GitHub.
    res.setHeader('Cache-Control', 'public, s-maxage=30');
    return res.status(200).send(JSON.stringify({ available: false }));
  }

  // Клики меняются часто — держим кэш коротким.
  res.setHeader('Cache-Control', 'public, s-maxage=30, stale-while-revalidate=300');

  try {
    const values = await mget(['clicks:total'].concat(KINDS.map((k) => `clicks:${k}`)));
    const total = num(values && values[0]);

    const kinds = {};
    let sum = 0;
    KINDS.forEach((k, i) => {
      const clicks = num(values && values[i + 1]);
      sum += clicks;
      kinds[k] = { clicks };
    });

    // На случай, если счётчик total потерялся, но по файлам цифры есть.
    res.status(200).send(JSON.stringify({ available: true, total: total || sum, kinds }));
  } catch (err) {
    res.setHeader('Cache-Control', 'public, s-maxage=30');
    res.status(200).send(JSON.stringify({ available: false, error: String((err && err.message) || err) }));
  }
};
