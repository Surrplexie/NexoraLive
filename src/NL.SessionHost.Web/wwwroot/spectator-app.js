const decisionClass = (d) => {
  const v = (d || "").toLowerCase();
  if (v === "allow") return "decision-allow";
  if (v === "block") return "decision-block";
  if (v === "warn") return "decision-warn";
  return "";
};

function formatTime(iso) {
  try {
    return new Date(iso).toISOString().replace("T", " ").replace(/\.\d{3}Z$/, "Z");
  } catch {
    return iso;
  }
}

async function fetchJson(path, options) {
  const res = await fetch(path, options);
  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(data.error || res.statusText);
  }
  return data;
}

function renderStats(status, demo) {
  document.getElementById("stat-session").textContent = status.sessionRunning ? "Running" : status.state;
  document.getElementById("stat-decisions").textContent = String(status.decisions ?? 0);
  document.getElementById("stat-demo").textContent = demo?.enabled ? "Auto" : "Manual";
}

function renderDecisions(decisions) {
  const body = document.getElementById("decision-feed-body");
  const countEl = document.getElementById("feed-count");
  countEl.textContent = decisions.length ? `(${decisions.length} shown)` : "";

  if (!decisions.length) {
    body.innerHTML = '<tr><td colspan="5" class="empty-row">Waiting for decisions…</td></tr>';
    return;
  }

  body.innerHTML = decisions
    .map(
      (d) => `<tr>
        <td>${formatTime(d.timestampUtc)}</td>
        <td>${escapeHtml(d.playerName)}</td>
        <td><code>${escapeHtml(d.eventName)}</code></td>
        <td><span class="decision-pill ${decisionClass(d.decision)}">${escapeHtml(d.decision)}</span></td>
        <td>${escapeHtml(d.message || "")}</td>
      </tr>`
    )
    .join("");
}

function escapeHtml(text) {
  return String(text)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

async function loadScenarios(status) {
  const grid = document.getElementById("scenario-grid");
  const card = document.getElementById("trigger-card");
  if (!status.triggersEnabled) {
    card.hidden = true;
    return;
  }

  const data = await fetchJson("/api/v1/spectator/scenarios");
  grid.innerHTML = (data.scenarios || [])
    .map(
      (s) => `<button type="button" class="scenario-btn" data-id="${escapeHtml(s.id)}" title="${escapeHtml(s.description)}">
        <span class="scenario-label">${escapeHtml(s.label)}</span>
        <span class="scenario-meta">→ ${escapeHtml(s.expectedDecision)}</span>
      </button>`
    )
    .join("");

  grid.querySelectorAll(".scenario-btn").forEach((btn) => {
    btn.onclick = () => triggerScenario(btn.dataset.id);
  });
}

async function triggerScenario(scenarioId) {
  const statusEl = document.getElementById("trigger-status");
  statusEl.textContent = "Sending event…";
  try {
    const result = await fetchJson("/api/v1/spectator/trigger", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ scenarioId }),
    });
    statusEl.textContent = `Sent ${result.eventName} — expect ${result.expectedDecision}.`;
    await refreshFeed();
  } catch (e) {
    statusEl.textContent = e.message;
  }
}

let lastDecisionCount = 0;

async function refreshFeed() {
  const data = await fetchJson("/api/v1/spectator/decisions?count=50");
  const decisions = data.decisions || [];
  renderDecisions(decisions);
  lastDecisionCount = decisions.length;
}

async function refresh() {
  try {
    const [status, demo] = await Promise.all([
      fetchJson("/api/v1/spectator/status"),
      fetch("/api/v1/demo/status").then((r) => r.json()).catch(() => ({})),
    ]);
    renderStats(status, demo);
    if (!window._scenariosLoaded) {
      await loadScenarios(status);
      window._scenariosLoaded = true;
    }
    await refreshFeed();
  } catch (e) {
    document.getElementById("stat-session").textContent = "Offline";
    document.getElementById("trigger-status").textContent = e.message;
  }
}

refresh();
setInterval(refresh, 2000);
