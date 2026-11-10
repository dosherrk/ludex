#!/usr/bin/env python3
"""
Ludex — сборщик каталога игр.

Обходит поиск магазина Steam целиком (все одиночные игры), вычитает бесплатные
сетевые поделки и MMO и складывает компактный файл каталога:

    {"v":1, "utc":"...", "count":N, "genres":[...], "games":[[appid,"Name","0 3"],...]}

Жанры берутся из самих строк выдачи (`data-ds-tagids`) — поэтому весь каталог
собирается за один проход, без обхода по жанрам.

Запуск:
    python3 tools/build-catalog.py --out catalog.json.gz
    python3 tools/build-catalog.py --limit-pages 30 --out /tmp/try.json.gz   # проба

Никаких ключей API не нужно. Скрипт сознательно вежливый: не больше двух
запросов одновременно и общий шаг 0.6 с; на 429 замедляется, на 403 — выходит.
"""

import argparse
import gzip
import html as htmllib
import json
import os
import random
import re
import sys
import threading
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

SEARCH = (
    "https://store.steampowered.com/search/results/?query"
    "&count=100&infinite=1&json=1&cc={cc}&l={lang}"
    "&category1=998&category2=2"          # только игры, только одиночные
)

# Теги Steam → подписи жанров в приложении (совпадают с Loc.cs)
GENRE_TAGS = [
    ("19", "Экшен"),
    ("21", "Приключения"),
    ("492", "Инди"),
    ("122", "RPG"),
    ("9", "Стратегия"),
    ("599", "Симулятор"),
    ("597", "Казуальные"),
    ("699", "Гонки"),
    ("701", "Спорт"),
    ("493", "Ранний доступ"),
]

ROW = re.compile(r'data-ds-appid="(\d+)"[^>]*?data-ds-tagids="\[([^\]]*)\]".*?</a>', re.S)
ROW_NO_TAGS = re.compile(r'data-ds-appid="(\d+)".*?</a>', re.S)
TITLE = re.compile(r'<span class="title">(.*?)</span>', re.S)


class Rate:
    """Общий темп запросов: один слот в gap_ms на весь процесс."""

    def __init__(self, gap_ms):
        self.gap = gap_ms / 1000.0
        self.next = time.time()
        self.lock = threading.Lock()

    def wait(self):
        with self.lock:
            now = time.time()
            if self.next < now:
                self.next = now
            slot = self.next
            self.next = slot + self.gap
        delay = slot - time.time()
        if delay > 0:
            time.sleep(delay)

    def slow_down(self):
        with self.lock:
            self.gap = min(self.gap * 1.5, 2.5)

    def speed_up(self):
        with self.lock:
            if self.gap > 0.6:
                self.gap = max(0.6, self.gap - 0.04)


class Blocked(Exception):
    pass


class Fetcher:
    def __init__(self, cc, lang, gap_ms, retries=4):
        self.base = SEARCH.format(cc=cc, lang=lang)
        self.rate = Rate(gap_ms)
        self.retries = retries
        self.blocked = False
        self.requests = 0
        self.lock = threading.Lock()

    def page(self, start, extra=""):
        url = f"{self.base}&start={start}{('&' + extra) if extra else ''}"

        for attempt in range(self.retries):
            if self.blocked:
                raise Blocked()

            self.rate.wait()
            try:
                req = urllib.request.Request(url, headers={
                    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Ludex/1.18",
                    "Accept-Language": "ru,en;q=0.8",
                })
                with urllib.request.urlopen(req, timeout=45) as response:
                    raw = response.read()

                with self.lock:
                    self.requests += 1
                self.rate.speed_up()
                return json.loads(raw).get("results_html", "") or ""

            except urllib.error.HTTPError as e:
                if e.code == 403:
                    # «хватит» — не повторяем
                    self.blocked = True
                    print("  ! магазин ограничил запросы (403) — останавливаемся", flush=True)
                    raise Blocked()
                if e.code == 429:
                    self.rate.slow_down()
                    time.sleep(2 + attempt * 3 + random.random())
                    continue
                time.sleep(1 + attempt * 2)

            except Exception as e:                     # сеть, таймаут, битый json
                if attempt == self.retries - 1:
                    print(f"  ! страница {start}: {e}", flush=True)
                time.sleep(1 + attempt * 2)

        return ""

    def total_count(self, extra=""):
        url = f"{self.base}&start=0{('&' + extra) if extra else ''}"
        self.rate.wait()
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 Ludex/1.18"})
        with urllib.request.urlopen(req, timeout=45) as response:
            return int(json.loads(response.read()).get("total_count") or 0)


def parse(html_text, with_tags=True):
    """Пары «appid — название — теги» из куска выдачи."""
    out = []
    pattern = ROW if with_tags else ROW_NO_TAGS

    for match in pattern.finditer(html_text):
        app_id = int(match.group(1))
        tags = []
        if with_tags and match.lastindex and match.lastindex >= 2:
            tags = [t.strip() for t in (match.group(2) or "").split(",") if t.strip()]

        title = TITLE.search(match.group(0))
        if not title:
            continue
        name = htmllib.unescape(re.sub(r"<.*?>", "", title.group(1))).strip()
        if not name:
            continue
        out.append((app_id, name, tags))
    return out


def crawl(fetcher, label, extra="", limit_pages=0, checkpoint=None, with_tags=False):
    """Полный обход одной подборки (по 100 строк на страницу)."""
    total = fetcher.total_count(extra)
    pages = (total + 99) // 100
    if limit_pages:
        pages = min(pages, limit_pages)

    print(f"{label}: {total} записей → {pages} страниц", flush=True)
    rows = {}

    def work(page):
        try:
            html_text = fetcher.page(page * 100, extra)
        except Blocked:
            return
        for app_id, name, tags in parse(html_text, with_tags=with_tags):
            rows.setdefault(app_id, (name, tags))

    done = 0
    with ThreadPoolExecutor(max_workers=2) as pool:
        for _ in pool.map(work, range(pages)):
            done += 1
            if done % 25 == 0 or done == pages:
                print(f"  {done}/{pages} страниц · собрано {len(rows)} · запросов {fetcher.requests}",
                      flush=True)
                if checkpoint:
                    save_checkpoint(checkpoint, rows)

    if checkpoint:
        save_checkpoint(checkpoint, rows)
    return rows


def save_checkpoint(path, rows):
    try:
        tmp = path + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump({"rows": [[k, v[0], v[1]] for k, v in rows.items()]}, f, ensure_ascii=False)
        os.replace(tmp, path)
    except Exception as e:
        print(f"  ! чекпойнт: {e}", flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="catalog.json.gz")
    ap.add_argument("--limit-pages", type=int, default=0, help="только для проб")
    ap.add_argument("--gap-ms", type=int, default=600)
    ap.add_argument("--cc", default="ru")
    ap.add_argument("--lang", default="russian")
    ap.add_argument("--exclude-pages", type=int, default=0, help="страниц для бесплатных/MMO (0 — все)")
    ap.add_argument("--checkpoint", default="")
    args = ap.parse_args()

    started = time.time()
    fetcher = Fetcher(args.cc, args.lang, args.gap_ms)

    ex_limited = args.exclude_pages
    try:
        free = crawl(fetcher, "бесплатные", "maxprice=free", ex_limited)
        mmo = crawl(fetcher, "MMO", "tags=128", ex_limited)
        games = crawl(fetcher, "все игры", "", args.limit_pages,
                      args.checkpoint or None, with_tags=True)
    except Blocked:
        print("Обход прерван: магазин ограничил запросы. Файл не записан.", flush=True)
        return 2

    if not games:
        print("Ничего не собрано.", flush=True)
        return 1

    tag_to_genre = dict(GENRE_TAGS)
    labels = [g for _, g in GENRE_TAGS]

    out = []
    for app_id, (name, tags) in games.items():
        if app_id in free or app_id in mmo:
            continue
        idx = sorted({labels.index(tag_to_genre[t]) for t in tags if t in tag_to_genre})
        out.append([app_id, name, " ".join(str(i) for i in idx)])

    out.sort(key=lambda row: row[0])

    payload = {
        "v": 1,
        "utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "count": len(out),
        "genres": labels,
        "games": out,
    }

    raw = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    with gzip.GzipFile(args.out, "wb", compresslevel=9, mtime=0) as f:
        f.write(raw)

    print(f"готово: {len(out)} игр · json {len(raw) / 1048576:.1f} МБ → "
          f"gz {os.path.getsize(args.out) / 1048576:.2f} МБ · "
          f"{fetcher.requests} запросов за {time.time() - started:.0f} с", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
