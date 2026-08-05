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

async function loadSettings() {
  document.getElementById('settings-block').textContent = JSON.stringify(await api('/api/v1/launch-ops/settings'), null, 2);
}

async function loadHealth() {
  document.getElementById('health-block').textContent = JSON.stringify(await api('/api/v1/launch-ops/health-summary'), null, 2);
}

async function loadValidation() {
  const body = {
    legalPagesVerified: document.getElementById('legal-verified').checked,
    hostBackupVerified: document.getElementById('backup-verified').checked,
    multiGame: {
      hostImagesVerified: true,
      verifiedGameIds: ['hello-fork', 'minecraft', 'beamng']
    }
  };
  const report = await api('/api/v1/launch-ops/validation/run', { method: 'POST', body: JSON.stringify(body) });
  const summary = document.getElementById('validation-summary');
  summary.textContent = report.launchOpsPassed ? 'LAUNCH OPS VALIDATION PASSED' : 'LAUNCH OPS VALIDATION FAILED';
  summary.className = report.launchOpsPassed ? 'ok' : 'warn';
  const lines = (report.checks || []).map(function (c) {
    return (c.passed ? '[PASS] ' : '[FAIL] ') + c.description + (c.detail ? ' — ' + c.detail : '');
  });
  document.getElementById('validation-checks').textContent = lines.join('\n');
}

document.getElementById('run-validation').onclick = function () { loadValidation().catch(showErr); };
document.getElementById('test-alert').onclick = function () {
  api('/api/v1/launch-ops/alert/test', { method: 'POST', body: '{}' }).then(function () {
    alert('Test alert sent (if webhook configured).');
  }).catch(showErr);
};
document.getElementById('save-key').onclick = function () {
  window.NlAuth.setOperatorKey(document.getElementById('operator-key').value);
};
document.getElementById('operator-key').value = window.NlAuth.getOperatorKey();

loadSettings().catch(showErr);
loadHealth().catch(showErr);

function showErr(err) {
  alert(err.message || String(err));
}
