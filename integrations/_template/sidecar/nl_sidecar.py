#!/usr/bin/env python3
"""NL Integration Spec v1 — sidecar stub for {{GAME_ID}} (Phase T0 template).

Replace emit points with real game-server hooks. Handle inbound warn/kick at minimum.

Usage:
  python nl_sidecar.py --url ws://127.0.0.1:27021/nl/v1?token=TOKEN --sample
"""

from __future__ import annotations

import argparse
import json
import sys
import time

try:
    import websocket  # pip install websocket-client
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
        print(f"[{{GAME_ID}}] non-json action: {raw}", flush=True)
        return
    action = (msg.get("action") or "").lower()
    player = msg.get("player") or "?"
    if action not in REQUIRED_ACTIONS and action not in ("recover", "mute", "despawn", "custom"):
        print(f"[{{GAME_ID}}] ignoring unknown action {action}", flush=True)
        return
    # TODO: map to game-native kick / chat / HUD
    print(f"[{{GAME_ID}}] apply action={action} player={player} event={msg.get('event')}", flush=True)


def sample(send) -> None:
    emit(send, "sessionStart", "system", {"map.id": 1})
    emit(send, "playerJoin", "Alice", {"player.alive": 1})
    emit(send, "playerChat", "Alice", {"chat.length": 5, "chat.capsRatio": 0})
    emit(send, "playerLeave", "Alice")


def main() -> int:
    parser = argparse.ArgumentParser(description="NL sidecar stub for {{GAME_ID}}")
    parser.add_argument("--url", required=True, help="Session bus WebSocket URL")
    parser.add_argument("--sample", action="store_true", help="Emit required sample events then exit")
    parser.add_argument("--token", default="", help="Optional bus token if not in URL")
    args = parser.parse_args()

    if websocket is None:
        print("Install websocket-client: pip install websocket-client", file=sys.stderr)
        return 2

    url = args.url
    if args.token and "token=" not in url:
        sep = "&" if "?" in url else "?"
        url = f"{url}{sep}token={args.token}"

    print(f"[{{GAME_ID}}] connecting {url}", flush=True)
    ws = websocket.create_connection(url, timeout=10)

    def send(line: str) -> None:
        ws.send(line)
        print(f"[{{GAME_ID}}] → {line}", flush=True)

    if args.sample:
        sample(send)
        # drain a few action frames if NL responds quickly
        ws.settimeout(1.0)
        try:
            while True:
                on_action(ws.recv())
        except Exception:
            pass
        ws.close()
        print(f"[{{GAME_ID}}] sample complete (events={','.join(REQUIRED_EVENTS)})", flush=True)
        return 0

    ws.settimeout(None)
    print(f"[{{GAME_ID}}] listening for actions; Ctrl+C to stop", flush=True)
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
