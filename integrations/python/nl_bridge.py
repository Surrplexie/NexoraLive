#!/usr/bin/env python3
"""NL Game Integration Spec v1 — reference bridge (WebSocket or TCP).

Usage:
  python nl_bridge.py --url ws://127.0.0.1:27021/nl/v1 --sample
  python nl_bridge.py --url ws://127.0.0.1:27021/nl/v1 --loop --interval 8
  python nl_bridge.py --tcp-events 127.0.0.1:27021 --tcp-actions 127.0.0.1:27023 --sample
"""

from __future__ import annotations

import argparse
import json
import socket
import sys
import threading
import time
import urllib.error
import urllib.request

try:
    import websocket  # pip install websocket-client
except ImportError:
    websocket = None


def emit_event(handle, event: str, player: str, props: dict | None = None, ts: int | None = None):
    line = {
        "nl": 1,
        "event": event,
        "player": player,
        "ts": ts or int(time.time() * 1000),
    }
    if props:
        line["props"] = props
    payload = json.dumps(line, separators=(",", ":"))
    handle(payload)


def admit_player(admit_url: str, player_id: str, display_name: str | None = None, streamer_id: str | None = None) -> dict:
    payload = {
        "playerId": player_id,
        "displayName": display_name or player_id,
    }
    if streamer_id:
        payload["streamerId"] = streamer_id
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        admit_url,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=10) as resp:
        return json.loads(resp.read().decode("utf-8"))


def sample_script(send, admit_url: str | None = None):
    emit_event(send, "sessionStart", "Alice", {"map.id": 1})
    players = ["Alice", "Bob"]
    for player in players:
        if admit_url:
            try:
                result = admit_player(admit_url, player, player)
                print(
                    f"[nl admit] {player} -> {result.get('decision')} admit={result.get('admit')} ({result.get('reason') or 'ok'})",
                    flush=True,
                )
                if not result.get("admit"):
                    continue
            except (urllib.error.URLError, TimeoutError) as ex:
                print(f"[nl admit] failed for {player}: {ex}", flush=True)
                continue
        emit_event(send, "playerJoin", player, {"player.alive": 1})
    emit_event(send, "shoot", "Alice", {"weapon.damage": 12})
    emit_event(send, "shoot", "Bob", {"weapon.damage": 50})
    emit_event(send, "playerChat", "Bob", {"chat.length": 22, "chat.capsRatio": 1, "chat.isCommand": 0})
    emit_event(send, "respawn", "Bob", {"player.health": 40})
    emit_event(send, "playerLeave", "Alice")


def tcp_action_listener(host: str, port: int):
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind((host, port))
    server.listen(1)
    print(f"[nl bridge] action listen {host}:{port}", flush=True)

    while True:
        conn, addr = server.accept()
        print(f"[nl bridge] NL action client from {addr}", flush=True)
        with conn:
            buf = b""
            while True:
                chunk = conn.recv(4096)
                if not chunk:
                    break
                buf += chunk
                while b"\n" in buf:
                    line, buf = buf.split(b"\n", 1)
                    text = line.decode("utf-8").strip()
                    if text:
                        print(f"[nl action] {text}", flush=True)


def run_tcp(events_host: str, events_port: int, actions_host: str, actions_port: int, sample: bool, loop: bool, interval: float, admit_url: str | None):
    if actions_port:
        threading.Thread(
            target=tcp_action_listener, args=(actions_host, actions_port), daemon=True
        ).start()
        time.sleep(0.2)

    while True:
        sock = socket.create_connection((events_host, events_port))
        print(f"[nl bridge] connected events → {events_host}:{events_port}", flush=True)

        def send(line: str):
            sock.sendall((line + "\n").encode("utf-8"))

        try:
            if sample or loop:
                while True:
                    sample_script(send, admit_url)
                    if not loop:
                        break
                    time.sleep(interval)
            else:
                print("[nl bridge] stdin → NL (Ctrl+C to quit)", flush=True)
                for line in sys.stdin:
                    send(line.rstrip("\n"))
        finally:
            sock.close()

        if not loop:
            break

        print(f"[nl bridge] reconnecting in {interval}s …", flush=True)
        time.sleep(interval)


def run_ws(url: str, sample: bool, loop: bool, interval: float, reconnect_delay: float, admit_url: str | None):
    if websocket is None:
        raise SystemExit("Install websocket-client: pip install websocket-client")

    if loop:
        run_ws_loop(url, interval, reconnect_delay, admit_url)
        return

    def on_message(_, message):
        for line in message.splitlines():
            line = line.strip()
            if line:
                print(f"[nl action] {line}", flush=True)

    def on_open(ws):
        print(f"[nl bridge] connected {url}", flush=True)

        def send(line: str):
            ws.send(line + "\n")

        if sample:
            sample_script(send, admit_url)
            time.sleep(1)
            ws.close()
        else:
            print("[nl bridge] stdin → NL (Ctrl+C to quit)", flush=True)
            for line in sys.stdin:
                ws.send(line.rstrip("\n") + "\n")

    ws_app = websocket.WebSocketApp(url, on_open=on_open, on_message=on_message)
    ws_app.run_forever()


def run_ws_loop(url: str, interval: float, reconnect_delay: float, admit_url: str | None):
    """Phase G — keep emitting sample events for public demo dashboards."""
    while True:
        ws = None
        try:
            ws = websocket.create_connection(url, timeout=30)
            print(f"[nl bridge] connected {url}", flush=True)

            def on_action(line: str):
                print(f"[nl action] {line}", flush=True)

            def send(line: str):
                ws.send(line + "\n")

            reader = threading.Thread(
                target=_ws_action_reader,
                args=(ws, on_action),
                daemon=True,
            )
            reader.start()

            cycle = 0
            while True:
                cycle += 1
                print(f"[nl bridge] demo cycle {cycle}", flush=True)
                sample_script(send, admit_url)
                time.sleep(interval)
        except KeyboardInterrupt:
            raise
        except Exception as ex:
            print(f"[nl bridge] disconnected: {ex}; retry in {reconnect_delay}s", flush=True)
            time.sleep(reconnect_delay)
        finally:
            if ws is not None:
                try:
                    ws.close()
                except Exception:
                    pass


def _ws_action_reader(ws, on_action):
    try:
        while True:
            message = ws.recv()
            if not message:
                break
            text = message.decode("utf-8") if isinstance(message, bytes) else message
            for line in text.splitlines():
                line = line.strip()
                if line:
                    on_action(line)
    except Exception:
        pass


def main():
    parser = argparse.ArgumentParser(description="NL Integration Spec v1 reference bridge")
    parser.add_argument("--url", help="WebSocket URL, e.g. ws://127.0.0.1:27021/nl/v1")
    parser.add_argument("--tcp-events", metavar="HOST:PORT", help="TCP events target")
    parser.add_argument("--tcp-actions", metavar="HOST:PORT", help="TCP action listen endpoint")
    parser.add_argument("--admit-url", help="Phase D join admission URL (POST before playerJoin)")
    parser.add_argument("--sample", action="store_true", help="Emit sample events then exit")
    parser.add_argument("--loop", action="store_true", help="Repeat sample events until interrupted (Phase G demo)")
    parser.add_argument("--interval", type=float, default=8.0, help="Seconds between demo cycles (--loop)")
    parser.add_argument("--reconnect-delay", type=float, default=3.0, help="Seconds before WS reconnect")
    args = parser.parse_args()

    if args.loop:
        args.sample = True

    if args.url:
        run_ws(args.url, args.sample, args.loop, args.interval, args.reconnect_delay, args.admit_url)
        return

    if args.tcp_events:
        host, port = args.tcp_events.rsplit(":", 1)
        actions = args.tcp_actions.rsplit(":", 1) if args.tcp_actions else (host, 0)
        action_host = actions[0]
        action_port = int(actions[1]) if len(actions) > 1 and actions[1] else 0
        run_tcp(host, int(port), action_host, action_port, args.sample, args.loop, args.interval, args.admit_url)
        return

    parser.error("Provide --url or --tcp-events")


if __name__ == "__main__":
    main()
