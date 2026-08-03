(function () {
  var statusEl = document.getElementById('status');

  function setStatus(msg, err) {
    statusEl.textContent = msg;
    statusEl.className = 'status ' + (err ? 'error' : 'ok');
  }

  async function api(path, opts) {
    var res = await fetch(path, Object.assign({
      headers: Object.assign({ 'Content-Type': 'application/json' }, window.NlAuth.authHeaders(), (opts && opts.headers) || {}),
    }, opts || {}));
    var body = await res.json().catch(function () { return {}; });
    if (!res.ok) throw new Error(body.error || res.statusText);
    return body;
  }

  document.getElementById('btn-setup').onclick = async function () {
    try {
      var data = await api('/api/v1/dogfood/setup', { method: 'POST' });
      document.getElementById('setup-result').textContent = JSON.stringify(data.profile, null, 2);
      setStatus('Dogfood profile loaded.');
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('btn-start').onclick = async function () {
    try {
      var data = await api('/api/v1/session/start', { method: 'POST', body: JSON.stringify({ replayOnce: false }) });
      document.getElementById('start-result').textContent = JSON.stringify(data, null, 2);
      var manifest = await api('/api/v1/session/manifest');
      setStatus('Session started. forkSessionId=' + (manifest.forkSessionId || 'none'));
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('btn-join').onclick = async function () {
    try {
      var result = await api('/api/v1/client/join-flow', {
        method: 'POST',
        body: JSON.stringify({
          playerId: 'sp-dogfood-1',
          streamerId: 'dogfood-streamer',
          platformUserId: '76561198000000001',
          platform: 'steam',
          gameId: 'hello-fork',
          majorVersion: '1.0',
          atOwnRiskAcknowledged: true,
          mode: 'Player',
        }),
      });
      document.getElementById('join-result').textContent = JSON.stringify(result, null, 2);
      setStatus(result.success ? 'Join complete' : result.message, !result.success);
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('btn-stop').onclick = async function () {
    try {
      var data = await api('/api/v1/session/stop', { method: 'POST' });
      document.getElementById('teardown-result').textContent = JSON.stringify(data, null, 2);
      setStatus('Session stopped — fork destroys after grace period.');
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('btn-refresh-status').onclick = async function () {
    try {
      var st = await api('/api/v1/dogfood/status');
      document.getElementById('teardown-result').textContent = JSON.stringify(st, null, 2);
      setStatus('Status refreshed.');
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('btn-open-operator').onclick = function () {
    window.open('/operator.html', '_blank');
  };
  document.getElementById('btn-open-client').onclick = function () {
    window.open('/nl-client.html', '_blank');
  };

  api('/api/v1/dogfood/status').then(function (st) {
    document.getElementById('teardown-result').textContent = JSON.stringify(st, null, 2);
  }).catch(function () {});
})();
