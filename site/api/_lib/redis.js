// Общий помощник для работы с Upstash Redis (Vercel Marketplace) по REST API,
// без npm-зависимостей. Поддерживаются оба набора переменных окружения:
//   KV_REST_API_URL / KV_REST_API_TOKEN        (интеграция Upstash в режиме KV)
//   UPSTASH_REDIS_REST_URL / UPSTASH_REDIS_REST_TOKEN (нативные имена Upstash)
// Если ничего не задано — все функции просто работают без счётчика.

const BASE = process.env.KV_REST_API_URL || process.env.UPSTASH_REDIS_REST_URL || '';
const TOKEN = process.env.KV_REST_API_TOKEN || process.env.UPSTASH_REDIS_REST_TOKEN || '';

const PREFIX = 'ludex:';
const TIMEOUT = 1500;

function configured() {
  return Boolean(BASE && TOKEN);
}

function headers() {
  return { Authorization: `Bearer ${TOKEN}`, 'Content-Type': 'application/json' };
}

async function call(path, init) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT);
  try {
    const r = await fetch(BASE + path, { ...init, headers: headers(), signal: controller.signal });
    if (!r.ok) throw new Error(`upstash ${r.status}`);
    const data = await r.json();
    return data.result;
  } finally {
    clearTimeout(timer);
  }
}

// ['mget', 'a', 'b'] → /mget/a/b
async function command(parts) {
  const path = '/' + parts.map((p) => encodeURIComponent(String(p))).join('/');
  return call(path, { method: 'GET' });
}

// [['incr','a'], ['set','b','1']] → POST /pipeline
async function pipeline(commands) {
  const result = await call('/pipeline', { method: 'POST', body: JSON.stringify(commands) });
  return result || [];
}

async function mget(keys) {
  return command(['mget'].concat(keys.map((k) => PREFIX + k)));
}

async function incrMany(keys) {
  return pipeline(keys.map((k) => ['INCR', PREFIX + k]));
}

async function setMany(pairs) {
  return pipeline(pairs.map(([k, v]) => ['SET', PREFIX + k, v]));
}

async function get(key) {
  return command(['get', PREFIX + key]);
}

module.exports = { configured, mget, incrMany, setMany, get, PREFIX };
