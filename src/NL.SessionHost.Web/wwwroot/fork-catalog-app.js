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

let selectedEntry = null;

function setStatus(msg, isError = false) {
  const el = document.getElementById("status");
  el.textContent = msg;
  el.style.color = isError ? "#f88" : "";
}

function tierClass(tier) {
  const t = String(tier || "").toLowerCase();
  if (t.includes("official")) return "ok";
  if (t.includes("platform")) return "";
  return "warn";
}

function renderEntries(entries) {
  const tbody = document.querySelector("#catalog-table tbody");
  tbody.innerHTML = "";
  for (const e of entries) {
    const tr = document.createElement("tr");
    const tier = e.tier ?? e.Tier ?? "AtOwnRisk";
    const status = e.status ?? e.Status ?? "Active";
    tr.innerHTML = `
      <td>${e.displayName ?? e.DisplayName ?? e.gameId ?? e.GameId}</td>
      <td>${e.majorVersion ?? e.MajorVersion}</td>
      <td class="${tierClass(tier)}">${tier}</td>
      <td>${status}</td>
      <td>${(e.noProgressTransfer ?? e.NoProgressTransfer) ? "Ephemeral" : "—"}</td>
      <td><button type="button" data-game="${e.gameId ?? e.GameId}" data-major="${e.majorVersion ?? e.MajorVersion}">Select</button></td>`;
    tr.querySelector("button").onclick = () => selectEntry(e);
    tbody.appendChild(tr);
  }
}

function selectEntry(entry) {
  selectedEntry = entry;
  const tier = entry.tier ?? entry.Tier ?? "AtOwnRisk";
  document.getElementById("selection-info").innerHTML = `
    <dt>Game</dt><dd>${entry.gameId ?? entry.GameId} — ${entry.displayName ?? entry.DisplayName ?? ""}</dd>
    <dt>Major</dt><dd>${entry.majorVersion ?? entry.MajorVersion}</dd>
    <dt>Image</dt><dd><code>${(entry.imageDigest ?? entry.ImageDigest ?? "").slice(0, 24)}…</code></dd>
    <dt>Min client</dt><dd>${entry.minClientVersion ?? entry.MinClientVersion ?? "—"}</dd>
    <dt>Tier</dt><dd class="${tierClass(tier)}">${tier}</dd>`;
  document.getElementById("legal-notice").textContent =
    entry.effectiveLegalNotice ?? entry.EffectiveLegalNotice
    ?? entry.legalNotice ?? entry.LegalNotice
    ?? "Session data on NL forks does not transfer to publisher servers.";
  setStatus(`Selected ${entry.gameId ?? entry.GameId}@${entry.majorVersion ?? entry.MajorVersion}.`);
}

async function loadModHub() {
  const mods = await api("/api/v1/fork/catalog/mod-hub");
  const sel = document.getElementById("mod-slots");
  sel.innerHTML = "";
  for (const m of mods) {
    const opt = document.createElement("option");
    opt.value = m.id ?? m.Id;
    opt.textContent = `${m.id ?? m.Id} — ${m.description ?? m.Description ?? ""}`;
    sel.appendChild(opt);
  }
}

async function loadCatalog() {
  const [entries, settings] = await Promise.all([
    api("/api/v1/fork/catalog/entries"),
    api("/api/v1/fork/catalog/settings"),
  ]);
  renderEntries(entries);
  document.getElementById("catalog-settings").innerHTML = `
    <dt>Enabled</dt><dd>${settings.enabled}</dd>
    <dt>Max majors / game</dt><dd>${settings.maxMajorsPerGame}</dd>
    <dt>Manifest</dt><dd>${settings.manifestPath}</dd>`;
}

document.getElementById("apply-selection").onclick = async () => {
  if (!selectedEntry) {
    setStatus("Select a catalog entry first.", true);
    return;
  }
  const modIds = [...document.getElementById("mod-slots").selectedOptions].map(o => o.value);
  const body = {
    gameId: selectedEntry.gameId ?? selectedEntry.GameId,
    majorVersion: selectedEntry.majorVersion ?? selectedEntry.MajorVersion,
    modIds,
  };
  const result = await api("/api/v1/fork/catalog/select", { method: "POST", body: JSON.stringify(body) });
  setStatus(`Profile updated: ${body.gameId}@${body.majorVersion} (${modIds.length} mod(s)). NLE: ${result.nleTemplate ?? "—"}`);
};

loadCatalog();
loadModHub();
window.NlAuth.initOperatorAuthUi();
