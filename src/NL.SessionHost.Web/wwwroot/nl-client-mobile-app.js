(function () {
  const statusEl = document.getElementById('status');

  function setStatus(msg, err) {
    statusEl.textContent = msg;
    statusEl.className = 'status ' + (err ? 'error' : 'ok');
  }

  async function action(kind) {
    var body = {
      playerId: document.getElementById('target-player').value.trim(),
      streamerId: document.getElementById('streamer-id').value.trim(),
      action: kind,
      reason: document.getElementById('reason').value.trim(),
    };
    if (!body.playerId) {
      setStatus('Enter target player id', true);
      return;
    }

    var res = await fetch('/api/v1/client/mobile/action', {
      method: 'POST',
      headers: Object.assign({ 'Content-Type': 'application/json' }, window.NlAuth.authHeaders()),
      body: JSON.stringify(body),
    });
    var json = await res.json();
    document.getElementById('result').textContent = JSON.stringify(json, null, 2);
    if (!res.ok) throw new Error(json.error || res.statusText);
    setStatus(kind + ' issued');
  }

  document.getElementById('warn-btn').onclick = function () {
    action('warn').catch(function (e) { setStatus(e.message, true); });
  };
  document.getElementById('kick-btn').onclick = function () {
    action('kick').catch(function (e) { setStatus(e.message, true); });
  };
})();
