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

  async function loadStreamers(ownStreamerId) {
    var list = await api('/api/v1/client/streamers');
    var ul = document.getElementById('streamer-list');
    ul.innerHTML = '';
    (list || []).forEach(function (s) {
      var li = document.createElement('li');
      var label = s.streamerId + ' — ' + (s.isLive ? 'LIVE' : 'offline') +
        (s.title ? ': ' + s.title : '');
      if (ownStreamerId && s.streamerId === ownStreamerId) {
        label += ' (your streamer)';
      }
      li.innerHTML = '<button type="button" data-id="' + s.streamerId + '">' + label + '</button>';
      ul.appendChild(li);
    });
    ul.querySelectorAll('button[data-id]').forEach(function (btn) {
      btn.onclick = function () {
        document.getElementById('streamer-id').value = btn.dataset.id;
      };
    });
    if (ownStreamerId && mode() === 'Streamer') {
      document.getElementById('streamer-id').value = ownStreamerId;
    }
  }

  async function initSignedIn() {
    var me = await window.NlAuth.fetchMe();
    var panel = document.getElementById('signed-in-panel');
    if (!me || !me.account) {
      panel.hidden = true;
      await loadStreamers(null);
      return;
    }

    panel.hidden = false;
    document.getElementById('me-block').textContent = JSON.stringify(me.account, null, 2);
    document.getElementById('player-id').value = me.account.playerId || me.account.accountId;
    document.getElementById('nl-account-id').value = me.account.accountId;
    localStorage.setItem('nlAccountId', me.account.accountId);
    localStorage.setItem('nlPlayerId', me.account.playerId || me.account.accountId);
    await loadStreamers(me.account.streamerId || null);
  }

  document.getElementById('refresh-streamers').onclick = function () {
    window.NlAuth.fetchMe().then(function (me) {
      return loadStreamers(me && me.account ? me.account.streamerId : null);
    }).catch(function (e) { setStatus(e.message, true); });
  };

  document.querySelectorAll('input[name="mode"]').forEach(function (radio) {
    radio.onchange = function () {
      window.NlAuth.fetchMe().then(function (me) {
        if (me && me.account && me.account.streamerId && mode() === 'Streamer') {
          document.getElementById('streamer-id').value = me.account.streamerId;
        }
      }).catch(function () {});
    };
  });

  document.getElementById('run-join').onclick = async function () {
    try {
      var me = await window.NlAuth.fetchMe();
      var accountId = document.getElementById('nl-account-id').value.trim()
        || (me && me.account && me.account.accountId)
        || localStorage.getItem('nlAccountId');
      var playerId = document.getElementById('player-id').value.trim()
        || (me && me.account && me.account.playerId)
        || localStorage.getItem('nlPlayerId')
        || 'sp-demo-1';
      document.getElementById('player-id').value = playerId;
      if (accountId) document.getElementById('nl-account-id').value = accountId;

      var twoFactorCode = document.getElementById('join-2fa').value.trim() || undefined;
      var body = {
        playerId: playerId,
        streamerId: document.getElementById('streamer-id').value.trim(),
        platformUserId: document.getElementById('platform-user').value.trim(),
        nlAccountId: accountId,
        twoFactorCode: twoFactorCode,
        platform: 'steam',
        atOwnRiskAcknowledged: document.getElementById('at-own-risk-ack').checked,
        mode: mode(),
      };
      var result = await api('/api/v1/client/join-flow', { method: 'POST', body: JSON.stringify(body) });
      document.getElementById('join-result').textContent = JSON.stringify(result, null, 2);
      var hint = document.getElementById('native-connect-hint');
      var clip = result.launch && result.launch.nativeConnectClipboard;
      if (clip) {
        hint.hidden = false;
        hint.textContent = 'Paste this in RimWorld / Together (or Minecraft): ' + clip;
      } else {
        hint.hidden = true;
        hint.textContent = '';
      }
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

  initSignedIn().catch(function () { loadStreamers(null).catch(function () {}); });

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
