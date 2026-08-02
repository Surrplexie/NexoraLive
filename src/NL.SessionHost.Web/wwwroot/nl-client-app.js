(function () {
  const statusEl = document.getElementById('status');

  function setStatus(msg, err) {
    statusEl.textContent = msg;
    statusEl.className = 'status ' + (err ? 'error' : 'ok');
  }

  function mode() {
    var checked = document.querySelector('input[name="mode"]:checked');
    return checked ? checked.value : 'Player';
  }

  async function api(path, opts) {
    const res = await fetch(path, Object.assign({
      headers: Object.assign({ 'Content-Type': 'application/json' }, window.NlAuth.authHeaders(), (opts && opts.headers) || {}),
    }, opts || {}));
    const body = await res.json().catch(function () { return {}; });
    if (!res.ok) throw new Error(body.error || body.message || res.statusText);
    return body;
  }

  async function loadStreamers() {
    var list = await api('/api/v1/client/streamers');
    var ul = document.getElementById('streamer-list');
    ul.innerHTML = '';
    (list || []).forEach(function (s) {
      var li = document.createElement('li');
      li.innerHTML = '<button type="button" data-id="' + s.streamerId + '">' +
        s.streamerId + ' — ' + (s.isLive ? 'LIVE' : 'offline') +
        (s.title ? ': ' + s.title : '') + '</button>';
      ul.appendChild(li);
    });
    ul.querySelectorAll('button[data-id]').forEach(function (btn) {
      btn.onclick = function () {
        document.getElementById('streamer-id').value = btn.dataset.id;
      };
    });
  }

  document.getElementById('refresh-streamers').onclick = function () {
    loadStreamers().catch(function (e) { setStatus(e.message, true); });
  };

  document.getElementById('run-join').onclick = async function () {
    try {
      var body = {
        playerId: document.getElementById('player-id').value.trim(),
        streamerId: document.getElementById('streamer-id').value.trim(),
        platformUserId: document.getElementById('platform-user').value.trim(),
        nlAccountId: document.getElementById('nl-account-id').value.trim() || localStorage.getItem('nlAccountId'),
        platform: 'steam',
        atOwnRiskAcknowledged: document.getElementById('at-own-risk-ack').checked,
        mode: mode(),
      };
      var result = await api('/api/v1/client/join-flow', { method: 'POST', body: JSON.stringify(body) });
      document.getElementById('join-result').textContent = JSON.stringify(result, null, 2);
      if (result.step === 'RequiresAtOwnRiskAck') {
        setStatus('Acknowledge at-own-risk disclaimer, then retry with checkbox.', true);
      } else {
        setStatus(result.success ? 'Join complete' : result.message, !result.success);
      }
      await refreshOverlay();
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('block-invite-test').onclick = async function () {
    var manifest = await api('/api/v1/session/manifest');
    var fakeInvite = manifest.admitUrl || manifest.httpBaseUrl + '/api/v1/session/admit';
    var blocked = await api('/api/v1/client/block-invite', {
      method: 'POST',
      body: JSON.stringify({ inviteUrl: fakeInvite, expectedHost: manifest.httpBaseUrl }),
    });
    setStatus(blocked.blocked ? blocked.reason : 'Not blocked');
  };

  async function refreshOverlay() {
    var playerId = document.getElementById('player-id').value.trim();
    var streamerId = document.getElementById('streamer-id').value.trim();
    var overlay = await api('/api/v1/client/overlay/' + encodeURIComponent(playerId) + '?streamer=' + encodeURIComponent(streamerId));
    document.getElementById('overlay-state').textContent = JSON.stringify(overlay, null, 2);
  }

  document.getElementById('refresh-overlay').onclick = function () {
    refreshOverlay().catch(function (e) { setStatus(e.message, true); });
  };

  document.getElementById('clip-trigger').onclick = function () {
    setStatus('Clip trigger sent (stub — wire to hotkey daemon in production).');
  };

  loadStreamers().catch(function () {});

  var params = new URLSearchParams(window.location.search);
  if (params.get('linked') === 'steam') {
    if (params.get('accountId')) {
      document.getElementById('nl-account-id').value = params.get('accountId');
      localStorage.setItem('nlAccountId', params.get('accountId'));
    }
    if (params.get('steamId')) {
      document.getElementById('platform-user').value = params.get('steamId');
      localStorage.setItem('nlSteam64', params.get('steamId'));
    }
    setStatus('Steam linked for join flow.');
    window.history.replaceState({}, '', '/nl-client.html');
  }
  if (params.get('error')) {
    setStatus(decodeURIComponent(params.get('error')), true);
    window.history.replaceState({}, '', '/nl-client.html');
  }

  var savedAccount = localStorage.getItem('nlAccountId');
  var savedSteam = localStorage.getItem('nlSteam64');
  if (savedAccount) document.getElementById('nl-account-id').value = savedAccount;
  if (savedSteam) document.getElementById('platform-user').value = savedSteam;

  document.getElementById('steam-sign-in').onclick = function () {
    var accountId = document.getElementById('nl-account-id').value.trim() || localStorage.getItem('nlAccountId');
    if (!accountId) {
      window.location.href = '/identity-link.html';
      return;
    }
    window.location.href = '/api/v1/identity/oauth/steam/authorize?accountId='
      + encodeURIComponent(accountId) + '&returnUrl=' + encodeURIComponent('/nl-client.html');
  };
})();
