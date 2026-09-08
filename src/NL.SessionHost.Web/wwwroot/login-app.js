(function () {
  var messageEl = document.getElementById('message');
  var authPanel = document.getElementById('auth-panel');
  var loggedInPanel = document.getElementById('logged-in-panel');
  var meBlock = document.getElementById('me-block');

  function setMessage(msg, err) {
    messageEl.textContent = msg;
    messageEl.className = 'status ' + (err ? 'error' : 'ok');
  }

  async function api(path, opts) {
    var res = await fetch(path, Object.assign({
      headers: Object.assign({ 'Content-Type': 'application/json' }, window.NlAuth.authHeaders(), (opts && opts.headers) || {}),
    }, opts || {}));
    var body = await res.json().catch(function () { return {}; });
    if (!res.ok) throw new Error(body.error || res.statusText);
    return body;
  }

  async function refreshMe() {
    var me = await window.NlAuth.fetchMe();
    if (!me) {
      authPanel.hidden = false;
      loggedInPanel.hidden = true;
      return;
    }

    authPanel.hidden = true;
    loggedInPanel.hidden = false;
    meBlock.textContent = JSON.stringify(me, null, 2);
    if (me.account && me.account.accountId) {
      localStorage.setItem('nlAccountId', me.account.accountId);
      localStorage.setItem('nlPlayerId', me.account.playerId);
    }
  }

  document.getElementById('register').onclick = async function () {
    try {
      var result = await api('/api/v1/auth/register', {
        method: 'POST',
        body: JSON.stringify({
          displayName: document.getElementById('reg-name').value.trim(),
          email: document.getElementById('reg-email').value.trim(),
          password: document.getElementById('reg-password').value,
        }),
      });
      window.NlAuth.setSessionToken(result.sessionToken);
      setMessage('Account created and signed in.');
      await refreshMe();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('login').onclick = async function () {
    try {
      var result = await api('/api/v1/auth/login', {
        method: 'POST',
        body: JSON.stringify({
          email: document.getElementById('login-email').value.trim(),
          password: document.getElementById('login-password').value,
          twoFactorCode: document.getElementById('login-2fa').value.trim() || null,
        }),
      });
      window.NlAuth.setSessionToken(result.sessionToken);
      setMessage('Signed in.');
      await refreshMe();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('logout').onclick = async function () {
    try {
      await api('/api/v1/auth/logout', { method: 'POST', body: '{}' });
    } catch (e) { /* ignore */ }
    window.NlAuth.clearSession();
    authPanel.hidden = false;
    loggedInPanel.hidden = true;
    setMessage('Signed out.');
  };

  document.getElementById('goto-client').onclick = function () {
    window.location.href = '/nl-client.html';
  };

  document.getElementById('enable-streamer').onclick = async function () {
    try {
      var slug = prompt('Streamer slug (optional, letters/numbers/hyphen):', '');
      var result = await api('/api/v1/auth/streamer/enable', {
        method: 'POST',
        body: JSON.stringify({ streamerSlug: slug || null }),
      });
      setMessage('Streamer enabled: ' + result.account.streamerId);
      await refreshMe();
    } catch (e) { setMessage(e.message, true); }
  };

  if (window.NlAuth.getSessionToken()) {
    refreshMe().catch(function () {
      authPanel.hidden = false;
      loggedInPanel.hidden = true;
    });
  }
})();
