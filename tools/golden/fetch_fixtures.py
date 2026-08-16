#!/usr/bin/env python
"""Fetch golden bar fixtures from Massive (Polygon-compatible aggregates API).

Writes the aggregates response verbatim (ticker/status/results, all pages merged) plus a
`_provenance` block, into tests/.../TestData/Golden/bars/<TICKER>_<tf>_<from>_<to>.json and
updates manifest.json with the query and a sha256 of the file.

Usage:
  python tools/golden/fetch_fixtures.py --ticker AAPL --from 2025-06-02 --to 2025-06-06 --tf 1m
  python tools/golden/fetch_fixtures.py --ticker AAPL --from 2023-06-01 --to 2025-06-06 --tf 1d

Requires MASSIVE_TOKEN (or MASSIVE_API_KEY) in the environment or in ./local.env.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import sys
from pathlib import Path

import requests

REPO = Path(__file__).resolve().parents[2]
GOLDEN = REPO / "tests/marketviewer-filters-unit-tests/MarketViewer.Filters.UnitTests/TestData/Golden"
BARS = GOLDEN / "bars"
MANIFEST = GOLDEN / "manifest.json"
BASE_URL = "https://api.massive.com"

TF_RE = re.compile(r"^(\d+)(m|h|d)$")
UNIT = {"m": "minute", "h": "hour", "d": "day"}


def load_token() -> str:
    for key in ("MASSIVE_TOKEN", "MASSIVE_API_KEY"):
        if os.environ.get(key):
            return os.environ[key]
    env = REPO / "local.env"
    if env.exists():
        for line in env.read_text().splitlines():
            if "=" in line and not line.strip().startswith("#"):
                k, v = line.split("=", 1)
                if k.strip() in ("MASSIVE_TOKEN", "MASSIVE_API_KEY"):
                    return v.strip().strip('"').strip("'")
    sys.exit("MASSIVE_TOKEN not found in environment or local.env")


def fetch(ticker: str, mult: int, span: str, date_from: str, date_to: str, token: str, adjusted: bool, limit: int):
    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {token}"
    url = (f"{BASE_URL}/v2/aggs/ticker/{ticker}/range/{mult}/{span}/{date_from}/{date_to}"
           f"?adjusted={'true' if adjusted else 'false'}&sort=asc&limit={limit}")
    query = url
    results = []
    first = None
    while url:
        r = session.get(url, timeout=60)
        r.raise_for_status()
        body = r.json()
        if first is None:
            first = body
        results.extend(body.get("results") or [])
        url = body.get("next_url")
    assert first is not None
    return query, {"ticker": first.get("ticker", ticker), "status": first.get("status", "OK"), "results": results}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ticker", required=True)
    ap.add_argument("--from", dest="date_from", required=True)
    ap.add_argument("--to", dest="date_to", required=True)
    ap.add_argument("--tf", required=True, help="e.g. 1m, 5m, 1h, 1d")
    ap.add_argument("--unadjusted", action="store_true")
    ap.add_argument("--limit", type=int, default=50000)
    args = ap.parse_args()

    m = TF_RE.match(args.tf)
    if not m:
        sys.exit("--tf must look like 1m, 5m, 1h, 1d")
    mult, span = int(m.group(1)), UNIT[m.group(2)]
    ticker = args.ticker.upper()
    adjusted = not args.unadjusted

    token = load_token()
    query, payload = fetch(ticker, mult, span, args.date_from, args.date_to, token, adjusted, args.limit)
    if not payload["results"]:
        sys.exit("no bars returned")

    name = f"{ticker}_{args.tf}_{args.date_from}_{args.date_to}"
    payload["_provenance"] = {
        "source": "massive.com aggregates v2",
        "query": query.replace(BASE_URL, ""),
        "ticker": ticker,
        "timeframe": args.tf,
        "from": args.date_from,
        "to": args.date_to,
        "adjusted": adjusted,
        "barCount": len(payload["results"]),
        "fetchedAtUtc": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat(),
    }

    BARS.mkdir(parents=True, exist_ok=True)
    out = BARS / f"{name}.json"
    out.write_text(json.dumps(payload, separators=(",", ":")), encoding="utf-8")

    manifest = json.loads(MANIFEST.read_text()) if MANIFEST.exists() else {"fixtures": {}}
    manifest["fixtures"][name] = {**payload["_provenance"], "file": f"bars/{name}.json", "sha256": sha256(out)}
    MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {out.relative_to(REPO)} ({len(payload['results'])} bars, {out.stat().st_size/1024:.0f} KB)")


if __name__ == "__main__":
    main()
