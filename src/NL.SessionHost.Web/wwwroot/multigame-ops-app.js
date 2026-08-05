function authHeaders() {
  const key = window.NlAuth.getOperatorKey();
  return key ? { 'X-NL-Operator-Key': key } : {};
}

async function api(path, options) {
  options = options || {};
  options.headers = Object.assign({ 'Content-Type': 'application/json' }, options.headers || {}, authHeaders());
  const res = await fetch(path, options);
  const text = await res.text();
  var data = null;
  try { data = text ? JSON.parse(text) : null; } catch (e) { data = { raw: text }; }
  if (!res.ok) throw new Error((data && data.error) || res.statusText);
  return data;
}

async function loadStatus() {
  const status = await api('/api/v1/multigame/status');
  document.getElementById('status-block').textContent = JSON.stringify(status, null, 2);
}

async function loadCatalog() {
  const catalog = await api('/api/v1/multigame/catalog');
  document.getElementById('catalog-block').textContent = JSON.stringify(catalog, null, 2);
}

async function loadValidation() {
  const verified = document.getElementById('host-images-verified').checked;
  const body = verified
    ? { hostImagesVerified: true, verifiedGameIds: ['hello-fork', 'minecraft', 'beamng'] }
    : {};
  const report = await api('/api/v1/multigame/validation/run', {
    method: 'POST',
    body: JSON.stringify(body)
  });
  const summary = document.getElementById('validation-summary');
  summary.textContent = report.multiGamePassed ? 'MULTIGAME VALIDATION PASSED' : 'MULTIGAME VALIDATION FAILED';
  summary.className = report.multiGamePassed ? 'ok' : 'warn';
  const lines = (report.checks || []).map(function (c) {
    return (c.passed ? '[PASS] ' : '[FAIL] ') + c.description + (c.detail ? ' — ' + c.detail : '');
  });
  document.getElementById('validation-checks').textContent = lines.join('\n');
}

document.getElementById('run-validation').onclick = function () { loadValidation().catch(showErr); };
document.getElementById('save-key').onclick = function () {
  window.NlAuth.setOperatorKey(document.getElementById('operator-key').value);
  document.getElementById('key-status').textContent = 'Saved.';
};
document.getElementById('operator-key').value = window.NlAuth.getOperatorKey();

loadStatus().catch(showErr);
loadCatalog().catch(showErr);

function showErr(err) {
  alert(err.message || String(err));
}
