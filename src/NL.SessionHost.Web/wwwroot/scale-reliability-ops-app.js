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
  document.getElementById('status-block').textContent = JSON.stringify(await api('/api/v1/scale-reliability/status'), null, 2);
}

async function loadRegions() {
  document.getElementById('regions-block').textContent = JSON.stringify(await api('/api/v1/scale-reliability/regions'), null, 2);
}

async function loadSlos() {
  document.getElementById('slos-block').textContent = JSON.stringify(await api('/api/v1/scale-reliability/production-slos'), null, 2);
}

async function loadValidation() {
  const body = {
    loadTestVerified: document.getElementById('load-verified').checked,
    multiRegionVerified: document.getElementById('region-verified').checked,
    verifiedRegionIds: ['us-east', 'us-west', 'eu-west'],
    distribution: {
      hostClientPackageVerified: true,
      streamerSignupVerified: true,
      playerJoinVerified: true,
      productionCutover: {
        alertingTestPassed: true,
        legalPagesVerified: true,
        multiGame: { hostImagesVerified: true, verifiedGameIds: ['hello-fork', 'minecraft', 'beamng'] }
      }
    }
  };
  const report = await api('/api/v1/scale-reliability/validation/run', { method: 'POST', body: JSON.stringify(body) });
  const summary = document.getElementById('validation-summary');
  summary.textContent = report.scaleReliabilityPassed ? 'SCALE RELIABILITY VALIDATION PASSED' : 'SCALE RELIABILITY VALIDATION FAILED';
  summary.className = report.scaleReliabilityPassed ? 'ok' : 'warn';
  const lines = (report.checks || []).map(function (c) {
    return (c.passed ? '[PASS] ' : '[FAIL] ') + c.description + (c.detail ? ' — ' + c.detail : '');
  });
  document.getElementById('validation-checks').textContent = lines.join('\n');
  if (report.productionSlos) {
    document.getElementById('slos-block').textContent = JSON.stringify(report.productionSlos, null, 2);
  }
}

document.getElementById('run-validation').onclick = function () { loadValidation().catch(showErr); };
document.getElementById('save-key').onclick = function () {
  window.NlAuth.setOperatorKey(document.getElementById('operator-key').value);
};
document.getElementById('operator-key').value = window.NlAuth.getOperatorKey();

loadStatus().catch(showErr);
loadRegions().catch(showErr);
loadSlos().catch(showErr);

function showErr(err) {
  alert(err.message || String(err));
}
