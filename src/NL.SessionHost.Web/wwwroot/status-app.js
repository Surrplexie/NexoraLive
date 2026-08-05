async function loadStatus() {
  const res = await fetch('/api/v1/launch-ops/status');
  if (!res.ok) throw new Error(res.statusText);
  const data = await res.json();
  const overall = document.getElementById('overall-status');
  overall.textContent = 'Overall: ' + (data.overallStatus || 'unknown');
  overall.className = data.overallStatus === 'operational' ? 'ok' : 'warn';
  document.getElementById('updated-at').textContent = 'Updated ' + (data.updatedAtUtc || '');

  const container = document.getElementById('components');
  container.innerHTML = '';
  (data.components || []).forEach(function (c) {
    const div = document.createElement('div');
    div.className = 'stat';
    div.innerHTML = '<strong>' + c.name + '</strong><br/><span class="' + (c.status === 'operational' ? 'ok' : 'warn') + '">' + c.status + '</span>' +
      (c.detail ? '<br/><small>' + c.detail + '</small>' : '');
    container.appendChild(div);
  });
}

loadStatus().catch(function (err) {
  document.getElementById('overall-status').textContent = 'Status unavailable: ' + (err.message || err);
});
