#!/usr/bin/env python3
"""NL Integration Spec v1 — Kenshi sidecar (Phase T1).

Emits the full outpost vocabulary and applies inbound warn/kick/tell.
For in-game enforcement, use integrations/kenshi/mod (community MP hooks).
"""

from __future__ import annotations

import argparse
import json
import sys
import time

try:
    import websocket
except ImportError:
    websocket = None

REQUIRED_EVENTS = (
    "sessionStart",
    "playerJoin",
    "playerLeave",
    "playerChat",
    "entityDamage",
    "buildingDestroy",
    "steal",
    "squadOrder",
    "raid",
    "trade",
    "itemDestroy",
    "knockdown",
    "respawn",
    "move",
)
REQUIRED_ACTIONS = ("warn", "kick", "tell")
_kicked: set[str] = set()


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
        print(f"[kenshi] non-json action: {raw}", flush=True)
        return
    action = (msg.get("action") or "").lower()
    player = msg.get("player") or "?"
    if action == "kick":
        _kicked.add(player)
        print(f"[kenshi] KICK {player} event={msg.get('event')} msg={msg.get('message')}", flush=True)
        return
    if action in REQUIRED_ACTIONS:
        print(f"[kenshi] apply action={action} player={player} event={msg.get('event')} msg={msg.get('message')}", flush=True)
        return
    print(f"[kenshi] apply action={action} player={player} event={msg.get('event')}", flush=True)


def sample(send) -> None:
    emit(send, "sessionStart", "system", {"map.id": 1})
    emit(send, "playerJoin", "Beep", {"player.alive": 1})
    emit(send, "playerJoin", "Shinobi", {"player.alive": 1})
    emit(send, "playerChat", "Beep", {"chat.length": 5, "chat.capsRatio": 0})
    emit(send, "entityDamage", "Beep", {"weapon.damage": 8, "player.alive": 1, "target.alive": 1})
    emit(send, "steal", "Beep", {"steal.value": 1200})
    emit(send, "buildingDestroy", "Beep", {"building.value": 2500})
    emit(send, "raid", "Beep", {"raid.severity": 12})
    emit(send, "squadOrder", "Shinobi", {"squad.ordered": 1, "player.downed": 0})
    emit(send, "trade", "Beep", {"trade.value": 9000})
    emit(send, "itemDestroy", "Beep", {"item.value": 50})
    emit(send, "knockdown", "Shinobi", {"player.downed": 1, "player.health": 0})
    emit(send, "respawn", "Shinobi", {"player.health": 100})
    emit(send, "move", "Shinobi", {"player.x": 8, "player.y": 0, "player.z": 0})
    emit(send, "playerLeave", "Beep")


def main() -> int:
    parser = argparse.ArgumentParser(description="NL Kenshi sidecar")
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
        print(f"[kenshi] → {line}", flush=True)

    if args.sample:
        sample(send)
        ws.settimeout(1.0)
        try:
            while True:
                on_action(ws.recv())
        except Exception:
            pass
        ws.close()
        print(f"[kenshi] sample complete (events={','.join(REQUIRED_EVENTS)})", flush=True)
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
