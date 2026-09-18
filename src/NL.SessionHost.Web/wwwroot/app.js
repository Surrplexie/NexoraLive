async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: {
      "Content-Type": "application/json",
      ...window.NlAuth.authHeaders(),
      ...(options.headers || {}),
    },
    ...options,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || res.statusText);
  }
  return res.json();
}

function profileFromForm() {
  return {
    streamerId: document.getElementById("streamer").value.trim() || "default-streamer",
    game: document.getElementById("game").value,
    gameId: document.getElementById("game-id").value.trim() || null,
    configPath: document.getElementById("config").value.trim(),
    sourcePath: document.getElementById("source").value.trim(),
    rconEndpoint: document.getElementById("rcon").value.trim() || null,
    nlActionEndpoint: document.getElementById("nl-action").value.trim() || null,
    useSessionBus: document.getElementById("use-bus").checked,
    antiCheat: document.getElementById("anti-cheat").checked,
    joinGate: document.getElementById("join-gate").checked,
    anomalyAutoMod: document.getElementById("anomaly-auto-mod").checked,
    forkOrchestratorEnabled: document.getElementById("fork-orchestrator").checked,
    useDefaultDataPaths: true,
  };
}

let _formDirty = false;
let _applyingProfile = false;
/** When true, status polls must never rewrite Profile inputs. */
let _lockForm = false;

function markFormDirty() {
  if (!_applyingProfile) {
    _formDirty = true;
    _lockForm = true;
  }
}

function bindFormDirtyTracking() {
  const root = document.getElementById("profile") || document.querySelector(".operator-panel") || document.body;
  root.addEventListener("input", markFormDirty, true);
  root.addEventListener("change", markFormDirty, true);
}

function applyProfile(p) {
  if (!p) return;
  _applyingProfile = true;
  try {
    document.getElementById("streamer").value = p.streamerId || "";
    document.getElementById("game").value = p.game || "generic";
    document.getElementById("config").value = p.configPath || "";
    document.getElementById("source").value = p.sourcePath || "";
    document.getElementById("rcon").value = p.rconEndpoint || "";
    document.getElementById("nl-action").value = p.nlActionEndpoint || "auto";
    document.getElementById("use-bus").checked = p.useSessionBus !== false;
    document.getElementById("anti-cheat").checked = p.antiCheat !== false;
    document.getElementById("join-gate").checked = !!p.joinGate;
    document.getElementById("anomaly-auto-mod").checked = !!p.anomalyAutoMod;
    document.getElementById("fork-orchestrator").checked = !!p.forkOrchestratorEnabled;
    document.getElementById("game-id").value = p.gameId || "";
    _formDirty = false;
  } finally {
    _applyingProfile = false;
  }
}

function renderBus(bus) {
  const el = document.getElementById("bus-info");
  if (!el) return;
  el.innerHTML = `
    <dt>HTTP</dt><dd>${bus.httpBaseUrl}</dd>
    <dt>WebSocket</dt><dd>${bus.webSocketUrl}</dd>
    <dt>Bridge URL</dt><dd id="bridge-url">${bus.bridgeConnectUrl}</dd>
    <dt>Token</dt><dd>${bus.token}</dd>
    <dt>Session</dt><dd>${bus.sessionId}</dd>`;
}

function renderManifest(m) {
  if (!m) return;
  window._lastManifest = m;
  const el = document.getElementById("manifest-info");
  if (!el) return;
  el.innerHTML = `
    <dt>Streamer</dt><dd>${m.streamerId}</dd>
    <dt>Bridge URL</dt><dd id="bridge-url">${m.bridgeConnectUrl}</dd>
    <dt>Admit URL</dt><dd>${m.admitUrl}</dd>
    <dt>Join gate</dt><dd>${m.joinGateEnabled ? "ON" : "off"}</dd>
    <dt>Session running</dt><dd>${m.sessionRunning ? "yes" : "no"}</dd>
    <dt>Fork orchestrator</dt><dd>${m.forkOrchestratorEnabled ? "ON" : "off"}</dd>
    <dt>Fork session</dt><dd>${m.forkSessionId || "—"}</dd>
    <dt>Fork connect</dt><dd>${m.forkConnectEndpoint || "—"}</dd>
    <dt>Moderation</dt><dd>${m.moderationUrl}</dd>`;
}

function updateOperatorPanels(isAuthorized, authRequired) {
  const locked = document.getElementById("operator-locked");
  const panels = document.querySelectorAll(".operator-panel");
  const showPanels = !authRequired || isAuthorized;
  if (locked) locked.hidden = showPanels;
  panels.forEach((p) => { p.hidden = !showPanels; });
}

/** Redacted public status omits paths — never hydrate the form from that. */
function profileIsOperatorComplete(p) {
  return !!p && (Object.prototype.hasOwnProperty.call(p, "configPath")
    || Object.prototype.hasOwnProperty.call(p, "sourcePath")
    || Object.prototype.hasOwnProperty.call(p, "nlActionEndpoint")
    || Object.prototype.hasOwnProperty.call(p, "forkOrchestratorEnabled"));
}

/**
 * @param {{ applyProfile?: boolean, forceProfile?: boolean }} [opts]
 * Polling / start / stop must NOT stomp the form.
 * Only explicit dogfood / bus-defaults / first hydrate may rewrite inputs.
 */
function renderStatus(data, opts = {}) {
  if (!data) return;
  const status = data.status && typeof data.status === "object" ? data.status : data;
  if (status.state != null) {
    document.getElementById("status").textContent = `State: ${status.state} · Decisions: ${status.decisions}`;
  }
  if (status.decisions != null) {
    document.getElementById("decisions").textContent = `(decisions: ${status.decisions})`;
  }
  if (status.log) {
    document.getElementById("log").textContent = (status.log || []).join("\n");
  }
  const allowForm = opts.forceProfile === true
    || (opts.applyProfile && !_lockForm && !_formDirty && profileIsOperatorComplete(status.profile));
  if (allowForm && status.profile) {
    applyProfile(status.profile);
  }
  if (status.bus) renderBus(status.bus);
  if (status.manifest) renderManifest(status.manifest);
}

let _profileHydrated = false;

async function refresh(opts = {}) {
  try {
    const info = await window.NlAuth.fetchSecurityInfo().catch(() => ({ operatorAuthRequired: false }));
    const isAuthorized = !info.operatorAuthRequired || !!window.NlAuth.getOperatorKey();
    updateOperatorPanels(isAuthorized, info.operatorAuthRequired);

    const data = await api("/api/v1/session");
    const status = data.status && typeof data.status === "object" ? data.status : data;
    // Start/stop/poll: never rewrite the Profile form.
    const apply = opts.forceProfile === true
      || (opts.applyProfile === true && !_lockForm && !_formDirty)
      || (!_profileHydrated && isAuthorized && !_lockForm && !_formDirty);
    renderStatus(data, { applyProfile: apply, forceProfile: opts.forceProfile === true });
    if (apply && (opts.forceProfile === true || profileIsOperatorComplete(status.profile))) {
      _profileHydrated = true;
    }
  } catch (e) {
    document.getElementById("status").textContent = e.message;
  }
}

function showActionError(e) {
  const msg = e?.message || String(e);
  document.getElementById("status").textContent = msg;
  console.error(e);
}

document.getElementById("copy-bridge-url")?.addEventListener("click", () => {
  const url = document.getElementById("bridge-url")?.textContent
    || window._lastManifest?.bridgeConnectUrl;
  if (url) navigator.clipboard.writeText(url);
});

document.getElementById("copy-manifest")?.addEventListener("click", () => {
  if (window._lastManifest) {
    navigator.clipboard.writeText(JSON.stringify(window._lastManifest, null, 2));
  }
});

document.getElementById("save-profile")?.addEventListener("click", async () => {
  const snap = profileFromForm();
  _lockForm = true;
  try {
    const data = await api("/api/v1/session/profile", {
      method: "PUT",
      body: JSON.stringify(snap),
    });
    // Keep the user's form values — do not re-bind from server.
    renderStatus(data, { applyProfile: false });
    applyProfile(snap);
    _lockForm = true;
    document.getElementById("status").textContent =
      `State: ${data.state} · Decisions: ${data.decisions} · Profile saved.`;
  } catch (e) {
    showActionError(e);
    applyProfile(snap);
  }
});

document.getElementById("load-dogfood-profile")?.addEventListener("click", async () => {
  try {
    const data = await api("/api/v1/dogfood/setup", { method: "POST" });
    _lockForm = false;
    _formDirty = false;
    renderStatus(data, { applyProfile: true, forceProfile: true });
    _lockForm = true;
    document.getElementById("status").textContent = "Dogfood profile loaded — click Start session.";
  } catch (e) {
    showActionError(e);
  }
});

document.getElementById("bus-defaults")?.addEventListener("click", async () => {
  try {
    const data = await api("/api/v1/session/bus-defaults", { method: "POST" });
    _lockForm = false;
    _formDirty = false;
    renderStatus(data, { applyProfile: true, forceProfile: true });
    _lockForm = true;
  } catch (e) {
    showActionError(e);
  }
});

document.getElementById("start")?.addEventListener("click", async () => {
  const snap = profileFromForm();
  _lockForm = true;
  try {
    await api("/api/v1/session/profile", { method: "PUT", body: JSON.stringify(snap) });
    const replay = document.getElementById("replay-once").checked;
    await api("/api/v1/session/start", { method: "POST", body: JSON.stringify({ replayOnce: replay }) });
    await refresh({ applyProfile: false });
    // Start may rewrite bus source server-side — never let that bounce into the form.
    applyProfile(snap);
    _lockForm = true;
  } catch (e) {
    showActionError(e);
    applyProfile(snap);
    _lockForm = true;
  }
});

document.getElementById("stop")?.addEventListener("click", async () => {
  const snap = profileFromForm();
  _lockForm = true;
  try {
    await api("/api/v1/session/stop", { method: "POST" });
    await refresh({ applyProfile: false });
    applyProfile(snap);
    _lockForm = true;
  } catch (e) {
    showActionError(e);
    applyProfile(snap);
    _lockForm = true;
  }
});

bindFormDirtyTracking();
refresh({ applyProfile: false });
setInterval(() => refresh({ applyProfile: false }), 2000);
window.NlAuth.initOperatorAuthUi().then(() => {
  if (!_lockForm && !_formDirty) {
    refresh({ applyProfile: true });
  } else {
    refresh({ applyProfile: false });
  }
});
