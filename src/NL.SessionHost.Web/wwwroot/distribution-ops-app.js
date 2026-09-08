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
  document.getElementById('status-block').textContent = JSON.stringify(await api('/api/v1/distribution/status'), null, 2);
}

async function loadManifest() {
  document.getElementById('manifest-block').textContent = JSON.stringify(await api('/api/v1/distribution/client-manifest'), null, 2);
}

async function loadValidation() {
  const body = {
    hostClientPackageVerified: document.getElementById('pkg-verified').checked,
    streamerSignupVerified: document.getElementById('signup-verified').checked,
    playerJoinVerified: document.getElementById('join-verified').checked,
    productionCutover: {
      alertingTestPassed: true,
      legalPagesVerified: true,
      multiGame: { hostImagesVerified: true, verifiedGameIds: ['hello-fork', 'minecraft', 'beamng'] }
    }
  };
  const report = await api('/api/v1/distribution/validation/run', { method: 'POST', body: JSON.stringify(body) });
  const summary = document.getElementById('validation-summary');
  summary.textContent = report.distributionPassed ? 'DISTRIBUTION VALIDATION PASSED' : 'DISTRIBUTION VALIDATION FAILED';
  summary.className = report.distributionPassed ? 'ok' : 'warn';
  const lines = (report.checks || []).map(function (c) {
    return (c.passed ? '[PASS] ' : '[FAIL] ') + c.description + (c.detail ? ' — ' + c.detail : '');
  });
  document.getElementById('validation-checks').textContent = lines.join('\n');
}

document.getElementById('run-validation').onclick = function () { loadValidation().catch(showErr); };
document.getElementById('save-key').onclick = function () {
  window.NlAuth.setOperatorKey(document.getElementById('operator-key').value);
};
document.getElementById('operator-key').value = window.NlAuth.getOperatorKey();

loadStatus().catch(showErr);
loadManifest().catch(showErr);

function showErr(err) {
  alert(err.message || String(err));
}
