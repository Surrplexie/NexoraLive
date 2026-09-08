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
  const report = await api('/api/v1/beta/validation/run', { method: 'POST', body: '{}' });
  const summary = document.getElementById('validation-summary');
  summary.textContent = report.betaPassed ? 'BETA VALIDATION PASSED' : 'BETA VALIDATION FAILED';
  summary.className = report.betaPassed ? 'ok' : 'warn';
}

async function loadWaitlist() {
  const rows = await api('/api/v1/beta/waitlist');
  const body = document.querySelector('#waitlist-table tbody');
  body.innerHTML = '';
  rows.forEach(function (entry) {
    const tr = document.createElement('tr');
    tr.innerHTML = '<td>' + entry.displayName + '</td>'
      + '<td>' + entry.contact + '</td>'
      + '<td>' + entry.status + '</td>'
      + '<td>' + (entry.approvedStreamerId || '-') + '</td>'
      + '<td></td>';
    const actions = tr.lastElementChild;
    if (entry.status === 'Pending') {
      const approve = document.createElement('button');
      approve.textContent = 'Approve';
      approve.onclick = async function () {
        await api('/api/v1/beta/waitlist/' + entry.id + '/approve', { method: 'POST', body: '{}' });
        await loadWaitlist();
      };
      actions.appendChild(approve);
    }
    body.appendChild(tr);
  });
}

document.getElementById('run-validation').onclick = function () { loadValidation().catch(showErr); };
document.getElementById('refresh-waitlist').onclick = function () { loadWaitlist().catch(showErr); };
document.getElementById('save-key').onclick = function () {
  window.NlAuth.setOperatorKey(document.getElementById('operator-key').value);
  document.getElementById('key-status').textContent = 'Saved.';
};

document.getElementById('operator-key').value = window.NlAuth.getOperatorKey();

function showErr(err) {
  alert(err.message || String(err));
}
