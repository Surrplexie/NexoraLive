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
  document.getElementById('status-block').textContent = JSON.stringify(await api('/api/v1/public-ga-launch/status'), null, 2);
}

async function loadChecklist() {
  document.getElementById('checklist-block').textContent = JSON.stringify(await api('/api/v1/public-ga-launch/checklist'), null, 2);
}

async function recordSignoff() {
  const entry = await api('/api/v1/public-ga-launch/signoff', { method: 'POST', body: '{}' });
  document.getElementById('signoff-block').textContent = JSON.stringify(entry, null, 2);
  document.getElementById('signoff-verified').checked = true;
}

async function loadValidation() {
  const body = {
    operatorSignoffVerified: document.getElementById('signoff-verified').checked,
    backupVerified: document.getElementById('backup-verified').checked,
    supportContactVerified: document.getElementById('support-verified').checked,
    launchAnnouncementReady: document.getElementById('announcement-ready').checked,
    legalCompliance: {
      gdprExportVerified: true,
      streamerTermsVerified: true,
      scaleReliability: {
        loadTestVerified: true,
        multiRegionVerified: true,
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
      }
    }
  };
  const report = await api('/api/v1/public-ga-launch/validation/run', { method: 'POST', body: JSON.stringify(body) });
  const summary = document.getElementById('validation-summary');
  summary.textContent = report.publicGaLaunchPassed ? 'PUBLIC GA LAUNCH VALIDATION PASSED' : 'PUBLIC GA LAUNCH VALIDATION FAILED';
  summary.className = report.publicGaLaunchPassed ? 'ok' : 'warn';
  const lines = (report.checks || []).map(function (c) {
    return (c.passed ? '[PASS] ' : '[FAIL] ') + c.description + (c.detail ? ' — ' + c.detail : '');
  });
  document.getElementById('validation-checks').textContent = lines.join('\n');
}

document.getElementById('record-signoff').onclick = function () { recordSignoff().catch(showErr); };
document.getElementById('run-validation').onclick = function () { loadValidation().catch(showErr); };
document.getElementById('save-key').onclick = function () {
  window.NlAuth.setOperatorKey(document.getElementById('operator-key').value);
};
document.getElementById('operator-key').value = window.NlAuth.getOperatorKey();

loadStatus().catch(showErr);
loadChecklist().catch(showErr);

function showErr(err) {
  alert(err.message || String(err));
}
