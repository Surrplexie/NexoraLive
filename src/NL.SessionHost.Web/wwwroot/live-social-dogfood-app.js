(function () {
  var messageEl = document.getElementById('message');

  function setMessage(msg, err) {
    messageEl.textContent = msg;
    messageEl.className = 'status ' + (err ? 'error' : 'ok');
  }

  function socialMode() {
    var checked = document.querySelector('input[name="social-mode"]:checked');
    return checked ? checked.value : 'mock';
  }

  async function api(path, opts) {
    var res = await fetch(path, Object.assign({
      headers: Object.assign({ 'Content-Type': 'application/json' }, window.NlAuth.authHeaders(), (opts && opts.headers) || {}),
    }, opts || {}));
    var body = await res.json().catch(function () { return {}; });
    if (!res.ok) throw new Error(body.error || body.message || res.statusText);
    return body;
  }

  async function refreshStatus() {
    var status = await api('/api/v1/dogfood/status');
    document.getElementById('status-block').textContent = JSON.stringify(status, null, 2);

    var social = await api('/api/v1/social/settings');
    document.getElementById('oauth-block').textContent = JSON.stringify({
      mode: social.mode,
      enabled: social.enabled,
      twitchOAuthConfigured: social.twitchOAuthConfigured,
      discordOAuthConfigured: social.discordOAuthConfigured,
      youtubeOAuthConfigured: social.youtubeOAuthConfigured,
      kickOAuthConfigured: social.kickOAuthConfigured,
      oauth: social.oauth,
      liveSocialDogfoodPath: social.liveSocialDogfoodPath,
    }, null, 2);
  }

  document.getElementById('btn-social-setup').onclick = async function () {
    try {
      var result = await api('/api/v1/dogfood/social/setup', {
        method: 'POST',
        body: JSON.stringify({ socialMode: socialMode() }),
      });
      document.getElementById('social-setup-result').textContent = JSON.stringify(result, null, 2);
      setMessage('Social fixtures installed.');
      await refreshStatus();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('btn-full-setup').onclick = async function () {
    try {
      var result = await api('/api/v1/dogfood/setup', {
        method: 'POST',
        body: JSON.stringify({ gameId: 'hello-fork', socialMode: socialMode() }),
      });
      document.getElementById('session-result').textContent = JSON.stringify(result, null, 2);
      setMessage('Dogfood profile loaded with social gate.');
      await refreshStatus();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('btn-start').onclick = async function () {
    try {
      var result = await api('/api/v1/session/start', {
        method: 'POST',
        body: JSON.stringify({ replayOnce: false }),
      });
      document.getElementById('session-result').textContent = JSON.stringify(result, null, 2);
      setMessage('Session started.');
      await refreshStatus();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('btn-stop').onclick = async function () {
    try {
      var result = await api('/api/v1/session/stop', { method: 'POST' });
      document.getElementById('session-result').textContent = JSON.stringify(result, null, 2);
      setMessage('Session stopped.');
      await refreshStatus();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('btn-follower-join').onclick = async function () {
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
      setMessage(result.success ? 'Follower join OK' : result.message, !result.success);
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('btn-stranger-admit').onclick = async function () {
    try {
      var result = await api('/api/v1/session/admit', {
        method: 'POST',
        body: JSON.stringify({
          playerId: 'sp-dogfood-stranger',
          streamerId: 'dogfood-streamer',
          displayName: 'Stranger',
          platformUserId: '76561198000000001',
          platform: 'steam',
          gameId: 'hello-fork',
          majorVersion: '1.0',
          atOwnRiskAcknowledged: true,
        }),
      });
      document.getElementById('join-result').textContent = JSON.stringify(result, null, 2);
      setMessage(result.admit ? 'Unexpected: stranger was admitted' : 'Stranger denied (expected)', result.admit);
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('btn-open-client').onclick = function () {
    window.open('/nl-client.html', '_blank');
  };

  document.getElementById('btn-refresh').onclick = function () {
    refreshStatus().catch(function (e) { setMessage(e.message, true); });
  };

  refreshStatus().catch(function () {});
})();
