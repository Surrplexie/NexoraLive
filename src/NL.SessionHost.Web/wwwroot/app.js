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
    configPath: document.getElementById("config").value.trim(),
    sourcePath: document.getElementById("source").value.trim(),
    rconEndpoint: document.getElementById("rcon").value.trim() || null,
    nlActionEndpoint: document.getElementById("nl-action").value.trim() || null,
    useSessionBus: document.getElementById("use-bus").checked,
    antiCheat: document.getElementById("anti-cheat").checked,
    joinGate: document.getElementById("join-gate").checked,
    anomalyAutoMod: document.getElementById("anomaly-auto-mod").checked,
    useDefaultDataPaths: true,
  };
}

function applyProfile(p) {
  if (!p) return;
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
    <dt>Moderation</dt><dd>${m.moderationUrl}</dd>`;
}

function updateOperatorPanels(isAuthorized, authRequired) {
  const locked = document.getElementById("operator-locked");
  const panels = document.querySelectorAll(".operator-panel");
  const showPanels = !authRequired || isAuthorized;
  if (locked) locked.hidden = showPanels;
  panels.forEach((p) => { p.hidden = !showPanels; });
}

function renderStatus(data) {
  document.getElementById("status").textContent = `State: ${data.state} · Decisions: ${data.decisions}`;
  document.getElementById("decisions").textContent = `(decisions: ${data.decisions})`;
  document.getElementById("log").textContent = (data.log || []).join("\n");
  if (data.profile?.configPath !== undefined) {
    applyProfile(data.profile);
  } else if (data.profile) {
    document.getElementById("streamer").value = data.profile.streamerId || "";
    document.getElementById("game").value = data.profile.game || "generic";
    document.getElementById("join-gate").checked = !!data.profile.joinGate;
    document.getElementById("anti-cheat").checked = data.profile.antiCheat !== false;
    document.getElementById("anomaly-auto-mod").checked = !!data.profile.anomalyAutoMod;
    document.getElementById("use-bus").checked = data.profile.useSessionBus !== false;
  }
  if (data.bus) renderBus(data.bus);
  if (data.manifest) renderManifest(data.manifest);
}

async function refresh() {
  try {
    const data = await api("/api/v1/session");
    renderStatus(data);
    const info = await window.NlAuth.fetchSecurityInfo().catch(() => ({ operatorAuthRequired: false }));
    const isAuthorized = !info.operatorAuthRequired || !!window.NlAuth.getOperatorKey();
    updateOperatorPanels(isAuthorized, info.operatorAuthRequired);
  } catch (e) {
    document.getElementById("status").textContent = e.message;
  }
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
  const data = await api("/api/v1/session/profile", {
    method: "PUT",
    body: JSON.stringify(profileFromForm()),
  });
  renderStatus(data);
});

document.getElementById("bus-defaults")?.addEventListener("click", async () => {
  const data = await api("/api/v1/session/bus-defaults", { method: "POST" });
  renderStatus(data);
});

document.getElementById("start")?.addEventListener("click", async () => {
  await api("/api/v1/session/profile", { method: "PUT", body: JSON.stringify(profileFromForm()) });
  const replay = document.getElementById("replay-once").checked;
  await api("/api/v1/session/start", { method: "POST", body: JSON.stringify({ replayOnce: replay }) });
  await refresh();
});

document.getElementById("stop")?.addEventListener("click", async () => {
  await api("/api/v1/session/stop", { method: "POST" });
  await refresh();
});

refresh();
setInterval(refresh, 2000);
window.NlAuth.initOperatorAuthUi().then(refresh);
