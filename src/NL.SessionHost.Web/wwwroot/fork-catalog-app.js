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

let versionPolicy = null;



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



function entryMajor(e) {

  return e.majorVersion ?? e.MajorVersion;

}



function entryGameId(e) {

  return e.gameId ?? e.GameId;

}



function isLatestStable(entry) {

  if (!versionPolicy || !versionPolicy.latestStableByGame) return true;

  const latest = versionPolicy.latestStableByGame[entryGameId(entry)];

  return !latest || latest === entryMajor(entry);

}



function renderEntries(entries) {

  const stableBody = document.querySelector("#catalog-table tbody");

  const betaBody = document.querySelector("#beta-catalog-table tbody");

  stableBody.innerHTML = "";

  betaBody.innerHTML = "";



  const seenStable = new Set();

  for (const e of entries) {

    const gameId = entryGameId(e);

    const major = entryMajor(e);

    const latest = isLatestStable(e);

    const tr = document.createElement("tr");

    const tier = e.tier ?? e.Tier ?? "AtOwnRisk";

    const status = e.status ?? e.Status ?? "Active";



    if (latest) {

      if (seenStable.has(gameId)) continue;

      seenStable.add(gameId);

      tr.innerHTML = `

        <td>${e.displayName ?? e.DisplayName ?? gameId}</td>

        <td>${major} <span class="ok">stable</span></td>

        <td class="${tierClass(tier)}">${tier}</td>

        <td>${status}</td>

        <td>${(e.noProgressTransfer ?? e.NoProgressTransfer) ? "Ephemeral" : "—"}</td>

        <td><button type="button" class="primary">Use latest</button></td>`;

      tr.querySelector("button").onclick = () => selectEntry(e, true);

      stableBody.appendChild(tr);

      continue;

    }



    const entitled = versionPolicy && versionPolicy.allowCustomMajorForStreamer;

    tr.innerHTML = `

      <td>${e.displayName ?? e.DisplayName ?? gameId}</td>

      <td>${major} <span class="warn">beta</span></td>

      <td class="${tierClass(tier)}">${tier}</td>

      <td>${status}</td>

      <td>${entitled ? "Entitled" : "Paid beta"}</td>

      <td><button type="button" ${entitled ? "" : "disabled"}>${entitled ? "Pin major" : "Locked"}</button></td>`;

    if (entitled) {

      tr.querySelector("button").onclick = () => selectEntry(e, false);

    }

    betaBody.appendChild(tr);

  }



  const betaSection = document.getElementById("beta-section");

  betaSection.hidden = betaBody.children.length === 0;

}



function selectEntry(entry, isStablePick) {

  selectedEntry = entry;

  const tier = entry.tier ?? entry.Tier ?? "AtOwnRisk";

  const major = entryMajor(entry);

  const gameId = entryGameId(entry);

  const latest = versionPolicy?.latestStableByGame?.[gameId] ?? major;

  document.getElementById("selection-info").innerHTML = `

    <dt>Game</dt><dd>${gameId} — ${entry.displayName ?? entry.DisplayName ?? ""}</dd>

    <dt>Major</dt><dd>${isStablePick ? latest + " (latest stable)" : major + " (custom beta)"}</dd>

    <dt>Image</dt><dd><code>${(entry.imageDigest ?? entry.ImageDigest ?? "").slice(0, 24)}…</code></dd>

    <dt>Min client</dt><dd>${entry.minClientVersion ?? entry.MinClientVersion ?? "—"}</dd>

    <dt>Tier</dt><dd class="${tierClass(tier)}">${tier}</dd>`;

  document.getElementById("legal-notice").textContent =

    entry.effectiveLegalNotice ?? entry.EffectiveLegalNotice

    ?? entry.legalNotice ?? entry.LegalNotice

    ?? "Session data on NL forks does not transfer to publisher servers.";

  setStatus(isStablePick

    ? `Ready to apply ${gameId}@${latest} (latest stable).`

    : `Ready to pin beta major ${gameId}@${major}.`);

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

  const [entries, settings, policy] = await Promise.all([

    api("/api/v1/fork/catalog/entries"),

    api("/api/v1/fork/catalog/settings"),

    api("/api/v1/fork/catalog/version-policy"),

  ]);

  versionPolicy = policy;

  renderEntries(entries);

  document.getElementById("catalog-settings").innerHTML = `

    <dt>Enabled</dt><dd>${settings.enabled}</dd>

    <dt>Default to latest stable</dt><dd>${settings.defaultToLatestStable}</dd>

    <dt>Custom major beta</dt><dd>${settings.customMajorVersionBetaEnabled ? "enabled" : "off"}</dd>

    <dt>Your custom-major entitlement</dt><dd>${policy.allowCustomMajorForStreamer ? "yes" : "no (latest stable only)"}</dd>

    <dt>Max majors / game</dt><dd>${settings.maxMajorsPerGame}</dd>

    <dt>Manifest</dt><dd>${settings.manifestPath}</dd>`;

}



document.getElementById("apply-selection").onclick = async () => {

  if (!selectedEntry) {

    setStatus("Select a catalog entry first.", true);

    return;

  }

  const modIds = [...document.getElementById("mod-slots").selectedOptions].map(o => o.value);

  const gameId = entryGameId(selectedEntry);

  const latest = versionPolicy?.latestStableByGame?.[gameId];

  const isStablePick = latest && latest === entryMajor(selectedEntry);

  const body = {

    gameId,

    modIds,

  };

  if (!isStablePick) {

    body.majorVersion = entryMajor(selectedEntry);

  }

  const result = await api("/api/v1/fork/catalog/select", { method: "POST", body: JSON.stringify(body) });

  const resolvedMajor = result.resolvedMajorVersion ?? result.profile?.gameMajorVersion ?? latest;

  setStatus(`Profile updated: ${gameId}@${resolvedMajor} (${modIds.length} mod(s)). NLE: ${result.nleTemplate ?? "—"}`);

};



loadCatalog();

loadModHub();

window.NlAuth.initOperatorAuthUi();

