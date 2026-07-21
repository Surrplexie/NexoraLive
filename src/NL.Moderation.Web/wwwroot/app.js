async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || res.statusText);
  }
  return res.json();
}

function streamerId() {
  const v = document.getElementById("streamer").value.trim();
  return v || "default-streamer";
}

function modId() {
  const v = document.getElementById("mod-id").value.trim();
  return v || "mod-web";
}

function playerId() {
  return document.getElementById("player-id").value.trim();
}

function actionBody() {
  return {
    streamerId: streamerId(),
    playerId: playerId(),
    issuedBy: modId(),
    reason: document.getElementById("reason").value.trim(),
  };
}

function setStatus(msg, isError = false) {
  const el = document.getElementById("status");
  el.textContent = msg;
  el.style.color = isError ? "#f88" : "";
}

function renderRecent(records) {
  const tbody = document.querySelector("#recent-table tbody");
  tbody.innerHTML = "";
  for (const r of records) {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${(r.timestampUtc || r.TimestampUtc || "").replace("T", " ").slice(0, 19)}</td>
      <td>${r.kind ?? r.Kind ?? ""}</td>
      <td>${r.playerName ?? r.PlayerName ?? r.playerId ?? r.PlayerId ?? "?"}</td>
      <td>${r.eventName ?? r.EventName ?? ""}</td>
      <td>${r.decision ?? r.Decision ?? "-"}</td>
      <td>${r.issuedBy ?? r.IssuedBy ?? "-"}</td>
      <td>${r.message ?? r.Message ?? ""}</td>`;
    tr.onclick = () => {
      const pid = r.playerId ?? r.PlayerId;
      if (pid) {
        document.getElementById("player-id").value = pid;
        loadHistory();
      }
    };
    tbody.appendChild(tr);
  }
  document.getElementById("recent-count").textContent = `(${records.length})`;
}

function renderHistory(history) {
  const standing = document.getElementById("standing");
  const tbody = document.querySelector("#offense-table tbody");
  tbody.innerHTML = "";

  if (!history) {
    standing.textContent = "Unknown SP.";
    standing.className = "standing muted";
    return;
  }

  const st = history.standing ?? history.Standing;
  standing.textContent = `Standing: ${st} (active offenses: ${history.activeOffenseCount ?? history.ActiveOffenseCount ?? 0})`;
  standing.className = "standing " + (st === "Banned" ? "bad" : st === "Graylist" ? "warn" : "ok");

  const offenses = history.offenses ?? history.Offenses ?? [];
  const twoYearsMs = 730 * 24 * 60 * 60 * 1000;
  for (const o of offenses) {
    const issued = o.issuedAtUtc ?? o.IssuedAtUtc ?? "";
    const issuedMs = issued ? new Date(issued).getTime() : 0;
    const active = issuedMs > 0 && (Date.now() - issuedMs) < twoYearsMs;
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${issued.replace("T", " ").slice(0, 19)}</td>
      <td>${o.issuedBy ?? o.IssuedBy ?? ""}</td>
      <td>${o.reason ?? o.Reason ?? ""}</td>
      <td>${o.game ?? o.Game ?? "-"}</td>
      <td>${active ? "Yes" : "Archived"}</td>`;
    tbody.appendChild(tr);
  }
}

async function loadStatus() {
  const data = await api("/api/v1/moderation");
  document.getElementById("paths").innerHTML = `
    <dt>Data root</dt><dd>${data.dataRoot}</dd>
    <dt>Moderation log</dt><dd>${data.moderationLog}</dd>
    <dt>SP store</dt><dd>${data.spStore}</dd>`;
}

async function loadRecent() {
  const records = await api(`/api/v1/moderation/recent?streamer=${encodeURIComponent(streamerId())}&count=100`);
  renderRecent(records);
  setStatus(`Loaded ${records.length} recent action(s) for '${streamerId()}'.`);
}

async function loadHistory() {
  const pid = playerId();
  if (!pid) {
    setStatus("Enter a player id to look up.", true);
    return;
  }

  try {
    const history = await api(`/api/v1/moderation/players/${encodeURIComponent(pid)}/history?streamer=${encodeURIComponent(streamerId())}`);
    renderHistory(history);
    setStatus(`Loaded offense history for '${pid}'.`);
  } catch (e) {
    renderHistory(null);
    setStatus(e.message, true);
  }
}

async function issue(path) {
  if (!playerId()) {
    setStatus("Enter a player id first.", true);
    return;
  }

  await api(path, { method: "POST", body: JSON.stringify(actionBody()) });
  setStatus(`Action applied to '${playerId()}'.`);
  await loadHistory();
  await loadRecent();
}

document.getElementById("refresh").onclick = async () => {
  await loadRecent();
  if (playerId()) await loadHistory();
};

document.getElementById("lookup").onclick = loadHistory;
document.getElementById("create-profile").onclick = async () => {
  if (!playerId()) {
    setStatus("Enter a player id first.", true);
    return;
  }
  await api("/api/v1/moderation/profiles", {
    method: "POST",
    body: JSON.stringify({ playerId: playerId(), displayName: playerId() }),
  });
  setStatus(`Profile '${playerId()}' ready.`);
  await loadHistory();
};

document.getElementById("warn").onclick = () => issue("/api/v1/moderation/warning");
document.getElementById("ban").onclick = () => issue("/api/v1/moderation/ban");
document.getElementById("graylist").onclick = () => issue("/api/v1/moderation/graylist");
document.getElementById("clear").onclick = () => issue("/api/v1/moderation/clear");

loadStatus();
loadRecent();
setInterval(loadRecent, 5000);
