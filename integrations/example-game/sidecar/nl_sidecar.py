#!/usr/bin/env python3
"""NL Integration Spec v1 — sidecar stub for example-game (Phase T0 reference)."""

from __future__ import annotations

import argparse
import json
import sys
import time

try:
    import websocket
except ImportError:
    websocket = None

REQUIRED_EVENTS = ("sessionStart", "playerJoin", "playerLeave", "playerChat")
REQUIRED_ACTIONS = ("warn", "kick", "tell")


def emit(handle, event: str, player: str, props: dict | None = None) -> None:
    line = {
        "nl": 1,
        "event": event,
        "player": player,
        "ts": int(time.time() * 1000),
    }
    if props:
        line["props"] = props
    handle(json.dumps(line, separators=(",", ":")))


def on_action(raw: str) -> None:
    try:
        msg = json.loads(raw)
    except json.JSONDecodeError:
        print(f"[example-game] non-json action: {raw}", flush=True)
        return
    action = (msg.get("action") or "").lower()
    player = msg.get("player") or "?"
    print(f"[example-game] apply action={action} player={player} event={msg.get('event')}", flush=True)


def sample(send) -> None:
    emit(send, "sessionStart", "system", {"map.id": 1})
    emit(send, "playerJoin", "Alice", {"player.alive": 1})
    emit(send, "playerChat", "Alice", {"chat.length": 5, "chat.capsRatio": 0})
    emit(send, "playerLeave", "Alice")


def main() -> int:
    parser = argparse.ArgumentParser(description="NL sidecar stub for example-game")
    parser.add_argument("--url", required=True)
    parser.add_argument("--sample", action="store_true")
    parser.add_argument("--token", default="")
    args = parser.parse_args()

    if websocket is None:
        print("Install websocket-client: pip install websocket-client", file=sys.stderr)
        return 2

    url = args.url
    if args.token and "token=" not in url:
        sep = "&" if "?" in url else "?"
        url = f"{url}{sep}token={args.token}"

    ws = websocket.create_connection(url, timeout=10)

    def send(line: str) -> None:
        ws.send(line)
        print(f"[example-game] → {line}", flush=True)

    if args.sample:
        sample(send)
        ws.settimeout(1.0)
        try:
            while True:
                on_action(ws.recv())
        except Exception:
            pass
        ws.close()
        return 0

    try:
        while True:
            on_action(ws.recv())
    except KeyboardInterrupt:
        pass
    finally:
        ws.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
