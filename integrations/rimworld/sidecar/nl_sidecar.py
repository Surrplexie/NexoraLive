#!/usr/bin/env python3
"""NL Integration Spec v1 — RimWorld sidecar (Phase T1).

Emits the full colony vocabulary and applies inbound warn/kick/tell.
For in-game enforcement, use integrations/rimworld/mod (Harmony / Together).
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
    "zoneEdit",
    "animalRelease",
    "colonistDraft",
    "trade",
    "itemDestroy",
    "colonistDown",
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
        print(f"[rimworld] non-json action: {raw}", flush=True)
        return
    action = (msg.get("action") or "").lower()
    player = msg.get("player") or "?"
    if action == "kick":
        _kicked.add(player)
        print(f"[rimworld] KICK {player} event={msg.get('event')} msg={msg.get('message')}", flush=True)
        return
    if action in REQUIRED_ACTIONS:
        print(f"[rimworld] apply action={action} player={player} event={msg.get('event')} msg={msg.get('message')}", flush=True)
        return
    print(f"[rimworld] apply action={action} player={player} event={msg.get('event')}", flush=True)


def sample(send) -> None:
    emit(send, "sessionStart", "system", {"map.id": 1})
    emit(send, "playerJoin", "Randy", {"player.alive": 1})
    emit(send, "playerJoin", "Cassandra", {"player.alive": 1})
    emit(send, "playerChat", "Randy", {"chat.length": 5, "chat.capsRatio": 0})
    emit(send, "entityDamage", "Randy", {"weapon.damage": 8, "player.alive": 1, "target.alive": 1})
    emit(send, "buildingDestroy", "Randy", {"building.value": 2500})
    emit(send, "zoneEdit", "Cassandra", {"zone.tiles": 40})
    emit(send, "animalRelease", "Randy", {"animal.count": 6})
    emit(send, "colonistDraft", "Cassandra", {"colonist.drafted": 1, "player.downed": 0})
    emit(send, "trade", "Randy", {"trade.value": 9000})
    emit(send, "itemDestroy", "Randy", {"item.value": 50})
    emit(send, "colonistDown", "Cassandra", {"player.downed": 1, "player.health": 0})
    emit(send, "respawn", "Cassandra", {"player.health": 100})
    emit(send, "move", "Cassandra", {"player.x": 8, "player.y": 0, "player.z": 0})
    emit(send, "playerLeave", "Randy")


def main() -> int:
    parser = argparse.ArgumentParser(description="NL RimWorld sidecar")
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
        print(f"[rimworld] → {line}", flush=True)

    if args.sample:
        sample(send)
        ws.settimeout(1.0)
        try:
            while True:
                on_action(ws.recv())
        except Exception:
            pass
        ws.close()
        print(f"[rimworld] sample complete (events={','.join(REQUIRED_EVENTS)})", flush=True)
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
