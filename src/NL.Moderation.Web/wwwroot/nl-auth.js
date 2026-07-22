const NL_AUTH_STORAGE_KEY = "nl-operator-key";

function getOperatorKey() {
  return sessionStorage.getItem(NL_AUTH_STORAGE_KEY) || "";
}

function setOperatorKey(key) {
  const trimmed = (key || "").trim();
  if (trimmed) {
    sessionStorage.setItem(NL_AUTH_STORAGE_KEY, trimmed);
  } else {
    sessionStorage.removeItem(NL_AUTH_STORAGE_KEY);
  }
}

function authHeaders() {
  const key = getOperatorKey();
  return key ? { "X-NL-Operator-Key": key } : {};
}

async function fetchSecurityInfo() {
  const res = await fetch("/api/v1/security");
  if (!res.ok) {
    throw new Error("Could not load security info.");
  }
  return res.json();
}

async function initOperatorAuthUi() {
  const panel = document.getElementById("operator-auth");
  if (!panel) return;

  try {
    const info = await fetchSecurityInfo();
    if (!info.operatorAuthRequired) {
      panel.hidden = true;
      return;
    }

    panel.hidden = false;
    const input = document.getElementById("operator-key");
    const status = document.getElementById("operator-auth-status");
    input.value = getOperatorKey();

    document.getElementById("save-operator-key").onclick = () => {
      setOperatorKey(input.value);
      status.textContent = getOperatorKey()
        ? "Operator key saved for this browser session."
        : "Operator key cleared.";
    };
  } catch {
    panel.hidden = true;
  }
}

window.NlAuth = {
  getOperatorKey,
  setOperatorKey,
  authHeaders,
  fetchSecurityInfo,
  initOperatorAuthUi,
};
