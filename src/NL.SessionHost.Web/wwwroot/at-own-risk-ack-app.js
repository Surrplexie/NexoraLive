(function () {
  const statusEl = document.getElementById('status');

  function setStatus(msg, err) {
    statusEl.textContent = msg;
    statusEl.className = 'status ' + (err ? 'error' : 'ok');
  }

  async function api(path, opts) {
    const res = await fetch(path, Object.assign({
      headers: Object.assign({ 'Content-Type': 'application/json' }, window.NlAuth.authHeaders(), (opts && opts.headers) || {}),
    }, opts || {}));
    const body = await res.json().catch(function () { return {}; });
    if (!res.ok) throw new Error(body.error || body.reason || res.statusText);
    return body;
  }

  function playerId() { return document.getElementById('player-id').value.trim(); }
  function gameId() { return document.getElementById('game-id').value.trim(); }

  document.getElementById('load-legal').onclick = async function () {
    try {
      var legal = await api('/api/v1/partnership/legal/' + encodeURIComponent(gameId()));
      document.getElementById('legal-copy').textContent = JSON.stringify(legal, null, 2);
      setStatus('Loaded legal copy for ' + gameId() + ' (tier: ' + legal.tier + ', requiresAck: ' + legal.requiresAcknowledgment + ')');
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('acknowledge').onclick = async function () {
    try {
      var ack = await api('/api/v1/partnership/acknowledge', {
        method: 'POST',
        body: JSON.stringify({ playerId: playerId(), gameId: gameId() }),
      });
      setStatus('Acknowledged at ' + ack.acknowledgedAtUtc);
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('test-admit').onclick = async function () {
    try {
      var without = await api('/api/v1/session/admit', {
        method: 'POST',
        body: JSON.stringify({ playerId: playerId(), gameId: gameId(), majorVersion: '1.0' }),
      });
      document.getElementById('admit-result').textContent = 'Without ack:\n' + JSON.stringify(without, null, 2);

      if (without.requiresAtOwnRiskAcknowledgment) {
        await api('/api/v1/partnership/acknowledge', {
          method: 'POST',
          body: JSON.stringify({ playerId: playerId(), gameId: gameId() }),
        });
        var withAck = await api('/api/v1/session/admit', {
          method: 'POST',
          body: JSON.stringify({
            playerId: playerId(),
            gameId: gameId(),
            majorVersion: '1.0',
            atOwnRiskAcknowledged: true,
          }),
        });
        document.getElementById('admit-result').textContent += '\n\nWith ack:\n' + JSON.stringify(withAck, null, 2);
        setStatus(withAck.admit ? 'Admit allowed after acknowledgment.' : 'Still denied: ' + withAck.reason, !withAck.admit);
      } else {
        setStatus('Partnered tier — no acknowledgment required.');
      }
    } catch (e) { setStatus(e.message, true); }
  };
})();
