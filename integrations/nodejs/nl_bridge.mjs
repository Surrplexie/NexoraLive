#!/usr/bin/env node
/**
 * NL Game Integration Spec v1 — reference bridge (Node 18+ WebSocket).
 *
 *   node integrations/nodejs/nl_bridge.mjs --url ws://127.0.0.1:27021/nl/v1 --sample
 */

import { createInterface } from "node:readline";

const args = process.argv.slice(2);
function flag(name) {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : null;
}
const url = flag("--url") ?? "ws://127.0.0.1:27021/nl/v1";
const sample = args.includes("--sample");

function emit(ws, event, player, props = undefined) {
  const line = {
    nl: 1,
    event,
    player,
    ts: Date.now(),
    ...(props ? { props } : {}),
  };
  ws.send(JSON.stringify(line));
}

const ws = new WebSocket(url);

ws.addEventListener("open", () => {
  console.log(`[nl bridge] connected ${url}`);
  if (sample) {
    emit(ws, "sessionStart", "Alice", { "map.id": 1 });
    emit(ws, "playerJoin", "Alice", { "player.alive": 1 });
    emit(ws, "shoot", "Alice", { "weapon.damage": 12 });
    emit(ws, "shoot", "Bob", { "weapon.damage": 50 });
    setTimeout(() => ws.close(), 1000);
    return;
  }

  const rl = createInterface({ input: process.stdin });
  rl.on("line", (line) => ws.send(line.trim()));
});

ws.addEventListener("message", (ev) => {
  for (const line of String(ev.data).split("\n")) {
    const trimmed = line.trim();
    if (trimmed) console.log(`[nl action] ${trimmed}`);
  }
});

ws.addEventListener("error", (ev) => console.error("[nl bridge] error", ev.message ?? ev));
