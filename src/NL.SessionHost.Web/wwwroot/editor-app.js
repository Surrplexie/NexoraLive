const ST = { Allow: 0, Block: 1, Deny: 2, Warn: 3, If: 4 };

const state = {
  model: { hotkeys: [], events: [] },
  vocabulary: null,
  selectedEvent: 0,
  selectedStmt: -1,
  stmtEditMode: null,
  meta: {},
};

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

function stmtTypeName(type) {
  return ["allow", "block", "deny", "warn", "if"][type] || "allow";
}

function stmtTypeFromName(name) {
  return { allow: ST.Allow, block: ST.Block, deny: ST.Deny, warn: ST.Warn, if: ST.If }[name] ?? ST.Allow;
}

function actionStmt(type, warnMessage) {
  if (type === ST.Warn) {
    return { type: ST.Warn, warnMessage: warnMessage || "warning" };
  }
  return { type };
}

function currentEvent() {
  return state.model.events[state.selectedEvent] || null;
}

function updateMeta(text) {
  const el = document.getElementById("editor-meta");
  if (el) el.textContent = text;
}

function syncNlePreview() {
  const lines = [];
  if (state.model.hotkeys?.length) {
    lines.push("# ── Hotkey bindings ──");
    state.model.hotkeys.forEach((h) => lines.push(`hotkey "${h.combo}": ${h.action}`));
    lines.push("");
  }
  if (state.model.events?.length) {
    lines.push("# ── NLEvent rules ──");
    state.model.events.forEach((evt) => {
      lines.push(`event ${evt.name}:`);
      appendStatements(lines, evt.statements || [], 1);
      lines.push("");
    });
  }
  document.getElementById("nle-preview").value = lines.join("\n").trim() + (lines.length ? "\n" : "");
}

function appendStatements(lines, statements, depth) {
  const indent = "    ".repeat(depth);
  statements.forEach((s) => {
    const t = s.type ?? ST.Allow;
    if (t === ST.Allow) lines.push(`${indent}allow`);
    else if (t === ST.Block) lines.push(`${indent}block`);
    else if (t === ST.Deny) lines.push(`${indent}deny`);
    else if (t === ST.Warn) lines.push(`${indent}warn "${(s.warnMessage || "").replace(/"/g, "'")}"`);
    else if (t === ST.If) {
      const cond = formatCondition(s.condition);
      lines.push(`${indent}if ${cond}:`);
      appendStatements(lines, s.thenBody?.length ? s.thenBody : [{ type: ST.Allow }], depth + 1);
      if (s.elseBody?.length) {
        lines.push(`${indent}else:`);
        appendStatements(lines, s.elseBody, depth + 1);
      }
    }
  });
}

function formatCondition(cond) {
  if (!cond?.parts?.length) return "true == true";
  const bits = [];
  cond.parts.forEach((p, i) => {
    if (i > 0 && cond.joins?.[i - 1]) bits.push(cond.joins[i - 1]);
    bits.push(`${p.left} ${p.op} ${p.right}`);
  });
  return bits.join(" ");
}

function renderEventList() {
  const ul = document.getElementById("event-list");
  ul.innerHTML = "";
  state.model.events.forEach((evt, i) => {
    const li = document.createElement("li");
    li.className = i === state.selectedEvent ? "selected" : "";
    li.textContent = evt.name;
    li.onclick = () => {
      state.selectedEvent = i;
      state.selectedStmt = -1;
      renderAll();
    };
    ul.appendChild(li);
  });
}

function renderStmtList() {
  const ul = document.getElementById("stmt-list");
  ul.innerHTML = "";
  const evt = currentEvent();
  if (!evt) return;
  (evt.statements || []).forEach((s, i) => {
    const li = document.createElement("li");
    li.className = i === state.selectedStmt ? "selected" : "";
    li.textContent = displayStmt(s);
    li.onclick = () => {
      state.selectedStmt = i;
      renderStmtList();
    };
    ul.appendChild(li);
  });
}

function displayStmt(s) {
  const t = s.type ?? ST.Allow;
  if (t === ST.If) {
    const cond = formatCondition(s.condition);
    const thenA = s.thenBody?.[0] ? stmtTypeName(s.thenBody[0].type) : "allow";
    const elseA = s.elseBody?.[0] ? ` else ${stmtTypeName(s.elseBody[0].type)}` : "";
    return `if ${cond}: ${thenA}${elseA}`;
  }
  if (t === ST.Warn) return `warn "${s.warnMessage || ""}"`;
  return stmtTypeName(t);
}

function renderEvalEvents() {
  const sel = document.getElementById("eval-event");
  const names = state.vocabulary?.events || state.model.events.map((e) => e.name);
  const current = sel.value;
  sel.innerHTML = "";
  names.forEach((n) => {
    const opt = document.createElement("option");
    opt.value = n;
    opt.textContent = n;
    sel.appendChild(opt);
  });
  if (current && [...sel.options].some((o) => o.value === current)) sel.value = current;
}

function renderAll() {
  renderEventList();
  renderStmtList();
  syncNlePreview();
  renderEvalEvents();
  const evt = currentEvent();
  document.getElementById("remove-event").disabled = !evt;
  document.getElementById("add-stmt").disabled = !evt;
  document.getElementById("edit-stmt").disabled = state.selectedStmt < 0;
  document.getElementById("remove-stmt").disabled = state.selectedStmt < 0;
  document.getElementById("move-stmt-up").disabled = state.selectedStmt <= 0;
  document.getElementById("move-stmt-down").disabled =
    !evt || state.selectedStmt < 0 || state.selectedStmt >= (evt.statements?.length || 0) - 1;
}

function showStmtEditor(mode, stmt) {
  state.stmtEditMode = mode;
  const panel = document.getElementById("stmt-editor");
  panel.hidden = false;
  document.getElementById("stmt-editor-title").textContent =
    mode === "add" ? "Add statement" : "Edit statement";

  const type = stmt ? stmtTypeName(stmt.type ?? ST.Allow) : "allow";
  document.getElementById("stmt-type").value = type === "if" ? "if" : type;
  document.getElementById("stmt-warn-msg").value = stmt?.warnMessage || "";
  toggleStmtFields(type);

  if (type === "if" && stmt) {
    renderCondParts(stmt.condition);
    fillBranchSelects(stmt);
  } else if (type === "if") {
    renderCondParts({ parts: [{ left: "player.health", op: ">", right: "0" }], joins: [] });
    document.getElementById("then-action").value = "allow";
    document.getElementById("else-action").value = "block";
  }
}

function hideStmtEditor() {
  document.getElementById("stmt-editor").hidden = true;
  state.stmtEditMode = null;
}

function toggleStmtFields(typeName) {
  document.getElementById("warn-wrap").hidden = typeName !== "warn";
  document.getElementById("if-wrap").hidden = typeName !== "if";
}

function renderCondParts(cond) {
  const wrap = document.getElementById("cond-parts");
  wrap.innerHTML = "";
  const parts = cond?.parts?.length ? cond.parts : [{ left: "player.health", op: ">", right: "0" }];
  const joins = cond?.joins || [];
  parts.forEach((p, i) => {
    const row = document.createElement("div");
    row.className = "cond-row";
    const joinHtml =
      i > 0
        ? `<select class="cond-join"><option value="and"${joins[i - 1] === "or" ? "" : " selected"}>and</option><option value="or"${joins[i - 1] === "or" ? " selected" : ""}>or</option></select>`
        : "";
    row.innerHTML = `${joinHtml}
      <input class="cond-left" value="${escapeAttr(p.left)}" list="prop-list" />
      <select class="cond-op">${[">", "<", ">=", "<=", "==", "!="].map((o) => `<option${o === p.op ? " selected" : ""}>${o}</option>`).join("")}</select>
      <input class="cond-right" value="${escapeAttr(p.right)}" />
      <button type="button" class="remove-cond" title="Remove">×</button>`;
    wrap.appendChild(row);
    row.querySelector(".remove-cond").onclick = () => {
      row.remove();
    };
  });

  if (!document.getElementById("prop-list")) {
    const dl = document.createElement("datalist");
    dl.id = "prop-list";
    (state.vocabulary?.properties || []).forEach((pr) => {
      const opt = document.createElement("option");
      opt.value = pr.name;
      dl.appendChild(opt);
    });
    document.body.appendChild(dl);
  }
}

function escapeAttr(s) {
  return String(s ?? "").replace(/"/g, "&quot;");
}

function fillBranchSelects(stmt) {
  const then0 = stmt.thenBody?.[0];
  const else0 = stmt.elseBody?.[0];
  document.getElementById("then-action").value = then0 ? stmtTypeName(then0.type) : "allow";
  document.getElementById("then-warn-msg").value = then0?.warnMessage || "";
  document.getElementById("else-action").value = else0 ? stmtTypeName(else0.type) : "none";
  document.getElementById("else-warn-msg").value = else0?.warnMessage || "";
  toggleBranchWarn();
}

function toggleBranchWarn() {
  document.getElementById("then-warn-wrap").hidden = document.getElementById("then-action").value !== "warn";
  document.getElementById("else-warn-wrap").hidden = document.getElementById("else-action").value !== "warn";
}

function readCondFromDom() {
  const rows = [...document.querySelectorAll("#cond-parts .cond-row")];
  const parts = rows.map((r) => ({
    left: r.querySelector(".cond-left").value.trim(),
    op: r.querySelector(".cond-op").value,
    right: r.querySelector(".cond-right").value.trim(),
  }));
  const joins = rows.slice(1).map((r) => r.querySelector(".cond-join")?.value || "and");
  return { parts, joins };
}

function readBranch(actionSel, warnInput) {
  const name = actionSel.value;
  if (name === "none") return null;
  const type = stmtTypeFromName(name);
  if (type === ST.Warn) return actionStmt(type, warnInput.value.trim());
  return actionStmt(type);
}

function buildStmtFromForm() {
  const typeName = document.getElementById("stmt-type").value;
  if (typeName === "warn") {
    return actionStmt(ST.Warn, document.getElementById("stmt-warn-msg").value.trim());
  }
  if (typeName === "if") {
    const thenBody = [readBranch(document.getElementById("then-action"), document.getElementById("then-warn-msg"))];
    const elseStmt = readBranch(document.getElementById("else-action"), document.getElementById("else-warn-msg"));
    return {
      type: ST.If,
      condition: readCondFromDom(),
      thenBody,
      elseBody: elseStmt ? [elseStmt] : [],
    };
  }
  return actionStmt(stmtTypeFromName(typeName));
}

function parseProps(text) {
  const props = {};
  text.split("\n").forEach((line) => {
    const trimmed = line.trim();
    if (!trimmed) return;
    const eq = trimmed.indexOf("=");
    if (eq < 0) return;
    const key = trimmed.slice(0, eq).trim();
    const val = parseFloat(trimmed.slice(eq + 1).trim());
    if (!Number.isNaN(val)) props[key] = val;
  });
  return props;
}

async function loadConfig() {
  const [config, vocab] = await Promise.all([
    api("/api/v1/editor/config"),
    api("/api/v1/editor/vocabulary"),
  ]);
  state.model = config.model || { hotkeys: [], events: [] };
  state.vocabulary = vocab;
  state.meta = config;
  state.selectedEvent = Math.min(state.selectedEvent, Math.max(0, state.model.events.length - 1));
  state.selectedStmt = -1;

  const sandboxNote = config.isSandbox ? "sandbox file" : "template / profile";
  const liveNote = config.sessionUsesSandbox ? " · live session uses sandbox" : "";
  updateMeta(
    `Source: ${sandboxNote}${liveNote} · session ${config.sessionRunning ? "running" : "stopped"}`
  );
  renderAll();
}

async function saveConfig() {
  const data = await api("/api/v1/editor/config", {
    method: "PUT",
    body: JSON.stringify(state.model),
  });
  document.getElementById("sandbox-status").textContent = `Saved to ${data.sourcePath}`;
  await loadConfig();
}

async function applyConfig() {
  await saveConfig();
  await api("/api/v1/editor/apply", {
    method: "POST",
    body: JSON.stringify({ restartSession: true }),
  });
  document.getElementById("sandbox-status").textContent = "Applied sandbox and restarted session.";
  await loadConfig();
}

async function resetConfig() {
  if (!confirm("Reset sandbox rules to the demo template?")) return;
  const data = await api("/api/v1/editor/reset", { method: "POST", body: "{}" });
  state.model = data.model;
  state.selectedEvent = 0;
  state.selectedStmt = -1;
  document.getElementById("sandbox-status").textContent = `Reset from ${data.template}`;
  renderAll();
}

async function runEvaluate() {
  const eventName = document.getElementById("eval-event").value;
  const props = parseProps(document.getElementById("eval-props").value);
  const resultEl = document.getElementById("eval-result");
  try {
    const result = await api("/api/v1/editor/evaluate", {
      method: "POST",
      body: JSON.stringify({ eventName, properties: props, model: state.model }),
    });
    const allow = result.allow;
    resultEl.className = "eval-result " + (allow ? "eval-allow" : "eval-block");
    resultEl.textContent = `${allow ? "✓ ALLOW" : "✗ BLOCK"}${result.message ? `\n${result.message}` : ""}`;
  } catch (e) {
    resultEl.className = "eval-result eval-block";
    resultEl.textContent = e.message;
  }
}

function wireUi() {
  document.getElementById("stmt-type").onchange = (e) => toggleStmtFields(e.target.value);
  document.getElementById("then-action").onchange = toggleBranchWarn;
  document.getElementById("else-action").onchange = toggleBranchWarn;

  document.getElementById("add-event").onclick = () => {
    const name = prompt("Event name:", "newEvent");
    if (!name?.trim()) return;
    state.model.events.push({ name: name.trim(), statements: [{ type: ST.Block }] });
    state.selectedEvent = state.model.events.length - 1;
    state.selectedStmt = 0;
    renderAll();
  };

  document.getElementById("remove-event").onclick = () => {
    const evt = currentEvent();
    if (!evt || !confirm(`Remove event "${evt.name}"?`)) return;
    state.model.events.splice(state.selectedEvent, 1);
    state.selectedEvent = Math.max(0, state.selectedEvent - 1);
    state.selectedStmt = -1;
    renderAll();
  };

  document.getElementById("add-stmt").onclick = () => {
    showStmtEditor("add", null);
  };

  document.getElementById("edit-stmt").onclick = () => {
    const evt = currentEvent();
    if (!evt || state.selectedStmt < 0) return;
    showStmtEditor("edit", evt.statements[state.selectedStmt]);
  };

  document.getElementById("remove-stmt").onclick = () => {
    const evt = currentEvent();
    if (!evt || state.selectedStmt < 0) return;
    evt.statements.splice(state.selectedStmt, 1);
    state.selectedStmt = -1;
    renderAll();
  };

  document.getElementById("move-stmt-up").onclick = () => {
    const evt = currentEvent();
    const i = state.selectedStmt;
    if (!evt || i <= 0) return;
    [evt.statements[i - 1], evt.statements[i]] = [evt.statements[i], evt.statements[i - 1]];
    state.selectedStmt = i - 1;
    renderAll();
  };

  document.getElementById("move-stmt-down").onclick = () => {
    const evt = currentEvent();
    const i = state.selectedStmt;
    if (!evt || i < 0 || i >= evt.statements.length - 1) return;
    [evt.statements[i + 1], evt.statements[i]] = [evt.statements[i], evt.statements[i + 1]];
    state.selectedStmt = i + 1;
    renderAll();
  };

  document.getElementById("save-stmt").onclick = () => {
    const evt = currentEvent();
    if (!evt) return;
    const stmt = buildStmtFromForm();
    if (state.stmtEditMode === "add") {
      evt.statements = evt.statements || [];
      evt.statements.push(stmt);
      state.selectedStmt = evt.statements.length - 1;
    } else if (state.selectedStmt >= 0) {
      evt.statements[state.selectedStmt] = stmt;
    }
    hideStmtEditor();
    renderAll();
  };

  document.getElementById("cancel-stmt").onclick = hideStmtEditor;

  document.getElementById("add-cond-part").onclick = () => {
    const cond = readCondFromDom();
    cond.parts.push({ left: "player.health", op: ">", right: "0" });
    cond.joins.push("and");
    renderCondParts(cond);
  };

  document.getElementById("save-config").onclick = () => saveConfig().catch(showError);
  document.getElementById("apply-config").onclick = () => applyConfig().catch(showError);
  document.getElementById("reset-config").onclick = () => resetConfig().catch(showError);
  document.getElementById("reload-config").onclick = () => loadConfig().catch(showError);
  document.getElementById("run-evaluate").onclick = () => runEvaluate().catch(showError);
}

function showError(err) {
  document.getElementById("sandbox-status").textContent = err.message || String(err);
}

async function init() {
  wireUi();
  await window.NlAuth.initOperatorAuthUi();
  await loadConfig();
}

init().catch((e) => {
  updateMeta("Failed to load editor.");
  showError(e);
});
