(function () {
  const statusEl = document.getElementById('status');
  const settingsEl = document.getElementById('orch-settings');
  const tbody = document.querySelector('#sessions-table tbody');
  const manifestEl = document.getElementById('manifest-preview');

  function setStatus(msg, kind) {
    statusEl.textContent = msg;
    statusEl.className = 'status ' + (kind || 'muted');
  }

  async function api(path, opts) {
    const res = await fetch(path, Object.assign({ headers: NlAuth.headers() }, opts || {}));
    const text = await res.text();
    let body;
    try { body = JSON.parse(text); } catch { body = text; }
    if (!res.ok) throw new Error(body.error || res.statusText);
    return body;
  }

  function renderSettings(s) {
    settingsEl.innerHTML = Object.entries(s)
      .map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`)
      .join('');
  }

  function renderSessions(list) {
    tbody.innerHTML = '';
    (list || []).forEach(function (s) {
      const tr = document.createElement('tr');
      tr.innerHTML =
        '<td>' + s.sessionId + '</td>' +
        '<td>' + s.streamerId + '</td>' +
        '<td>' + s.gameId + '@' + s.majorVersion + '</td>' +
        '<td>' + s.state + '</td>' +
        '<td>' + s.provisioner + '</td>' +
        '<td><code>' + (s.forkConnectEndpoint || '—') + '</code></td>' +
        '<td><button type="button" data-id="' + s.sessionId + '">Destroy</button></td>';
      tbody.appendChild(tr);
    });
    tbody.querySelectorAll('button[data-id]').forEach(function (btn) {
      btn.addEventListener('click', async function () {
        try {
          await api('/api/v1/fork/orchestrator/destroy/' + btn.dataset.id, { method: 'POST' });
          setStatus('Destroyed ' + btn.dataset.id, 'ok');
          await refresh();
        } catch (e) {
          setStatus(e.message, 'error');
        }
      });
    });
  }

  async function refresh() {
    const settings = await api('/api/v1/fork/orchestrator/settings');
    renderSettings(settings);
    const sessions = await api('/api/v1/fork/orchestrator/sessions');
    renderSessions(sessions);
    const manifest = await api('/api/v1/session/manifest');
    manifestEl.textContent = JSON.stringify({
      forkOrchestratorEnabled: manifest.forkOrchestratorEnabled,
      forkSessionId: manifest.forkSessionId,
      forkConnectEndpoint: manifest.forkConnectEndpoint,
      forkProvisioner: manifest.forkProvisioner,
      reservedPrivilegedSlots: manifest.reservedPrivilegedSlots,
      bridgeConnectUrl: manifest.bridgeConnectUrl,
    }, null, 2);
  }

  refresh().catch(function (e) { setStatus(e.message, 'error'); });
  setInterval(function () { refresh().catch(function () {}); }, 8000);
})();
