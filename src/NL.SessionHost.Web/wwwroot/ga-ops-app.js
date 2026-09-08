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

async function loadValidation() {
  const report = await api('/api/v1/ga/validation/run', { method: 'POST', body: '{}' });
  const summary = document.getElementById('validation-summary');
  summary.textContent = report.gaPassed ? 'GA VALIDATION PASSED' : 'GA VALIDATION FAILED';
  summary.className = report.gaPassed ? 'ok' : 'warn';
}

async function loadSla() {
  const sla = await api('/api/v1/ga/sla');
  document.getElementById('sla-tier').textContent = 'Tier: ' + sla.tier;
  const body = document.querySelector('#sla-table tbody');
  body.innerHTML = '';
  (sla.status || []).forEach(function (s) {
    const tr = document.createElement('tr');
    tr.innerHTML = '<td>' + s.name + '</td>'
      + '<td>' + s.target + ' ' + s.unit + '</td>'
      + '<td>' + Number(s.current).toFixed(2) + '</td>'
      + '<td>' + (s.met ? 'yes' : 'no') + '</td>';
    body.appendChild(tr);
  });
}

async function loadStreamers() {
  const rows = await api('/api/v1/ga/streamers');
  const body = document.querySelector('#streamers-table tbody');
  body.innerHTML = '';
  rows.forEach(function (entry) {
    const tr = document.createElement('tr');
    tr.innerHTML = '<td>' + entry.displayName + '</td>'
      + '<td>' + entry.contact + '</td>'
      + '<td>' + entry.streamerId + '</td>'
      + '<td>' + (entry.preferredGameId || '-') + '</td>'
      + '<td>' + entry.registeredAtUtc + '</td>';
    body.appendChild(tr);
  });
}

document.getElementById('run-validation').onclick = function () { loadValidation().catch(showErr); };
document.getElementById('refresh-sla').onclick = function () { loadSla().catch(showErr); };
document.getElementById('refresh-streamers').onclick = function () { loadStreamers().catch(showErr); };
document.getElementById('save-key').onclick = function () {
  window.NlAuth.setOperatorKey(document.getElementById('operator-key').value);
  document.getElementById('key-status').textContent = 'Saved.';
};

document.getElementById('operator-key').value = window.NlAuth.getOperatorKey();

function showErr(err) {
  alert(err.message || String(err));
}

loadSla().catch(showErr);
