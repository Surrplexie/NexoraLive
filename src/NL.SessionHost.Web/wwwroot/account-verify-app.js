(function () {
  var messageEl = document.getElementById('message');
  var accountInput = document.getElementById('account-id');
  var statusBlock = document.getElementById('status-block');
  var enrollBlock = document.getElementById('enroll-block');
  var playerInput = document.getElementById('player-id');

  function setMessage(msg, err) {
    messageEl.textContent = msg;
    messageEl.className = 'status ' + (err ? 'error' : 'ok');
  }

  function accountId() {
    return accountInput.value.trim() || localStorage.getItem('nlAccountId') || '';
  }

  async function api(path, opts) {
    var res = await fetch(path, Object.assign({
      headers: Object.assign({ 'Content-Type': 'application/json' }, (opts && opts.headers) || {}),
    }, opts || {}));
    var body = await res.json().catch(function () { return {}; });
    if (!res.ok) throw new Error(body.error || res.statusText);
    return body;
  }

  function loadQueryParams() {
    var params = new URLSearchParams(window.location.search);
    if (params.get('accountId')) {
      accountInput.value = params.get('accountId');
      localStorage.setItem('nlAccountId', params.get('accountId'));
      window.history.replaceState({}, '', '/account-verify.html');
    }
  }

  async function refreshStatus() {
    var id = accountId();
    if (!id) {
      statusBlock.textContent = 'Enter an account id first.';
      return;
    }
    accountInput.value = id;
    localStorage.setItem('nlAccountId', id);
    var status = await api('/api/v1/identity/verification/' + encodeURIComponent(id));
    statusBlock.textContent = JSON.stringify(status, null, 2);
  }

  document.getElementById('refresh-status').onclick = function () {
    refreshStatus().catch(function (e) { setMessage(e.message, true); });
  };

  document.getElementById('send-email-code').onclick = async function () {
    try {
      var id = accountId();
      var email = document.getElementById('email').value.trim();
      if (!id || !email) {
        setMessage('Account id and email required.', true);
        return;
      }
      var result = await api('/api/v1/identity/verification/email/request', {
        method: 'POST',
        body: JSON.stringify({ accountId: id, email: email }),
      });
      if (result.devCode) {
        document.getElementById('email-code').value = result.devCode;
        setMessage('Code sent (dev mode: auto-filled).');
      } else {
        setMessage('Verification code sent.');
      }
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('confirm-email').onclick = async function () {
    try {
      var id = accountId();
      var code = document.getElementById('email-code').value.trim();
      var result = await api('/api/v1/identity/verification/email/confirm', {
        method: 'POST',
        body: JSON.stringify({ accountId: id, code: code }),
      });
      setMessage('Email verified: ' + result.verification);
      await refreshStatus();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('start-2fa').onclick = async function () {
    try {
      var id = accountId();
      var result = await api('/api/v1/identity/verification/2fa/enroll/start', {
        method: 'POST',
        body: JSON.stringify({ accountId: id }),
      });
      enrollBlock.textContent = JSON.stringify({
        secretBase32: result.secretBase32,
        otpAuthUri: result.otpAuthUri,
        hint: 'Add otpAuthUri to your authenticator app, then enter the 6-digit code below.',
      }, null, 2);
      setMessage('2FA enrollment started — scan/add the otpauth URI.');
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('confirm-2fa').onclick = async function () {
    try {
      var id = accountId();
      var code = document.getElementById('totp-code').value.trim();
      var result = await api('/api/v1/identity/verification/2fa/enroll/confirm', {
        method: 'POST',
        body: JSON.stringify({ accountId: id, code: code }),
      });
      setMessage('2FA enabled: ' + result.verification);
      await refreshStatus();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('verify-2fa').onclick = async function () {
    try {
      var id = accountId();
      var code = document.getElementById('totp-code').value.trim();
      await api('/api/v1/identity/verification/2fa/verify', {
        method: 'POST',
        body: JSON.stringify({ accountId: id, code: code }),
      });
      setMessage('2FA code accepted.');
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('disable-2fa').onclick = async function () {
    try {
      var id = accountId();
      var code = document.getElementById('totp-code').value.trim();
      var result = await api('/api/v1/identity/verification/2fa', {
        method: 'DELETE',
        body: JSON.stringify({ accountId: id, code: code }),
      });
      setMessage('2FA disabled: ' + result.verification);
      enrollBlock.textContent = '';
      await refreshStatus();
    } catch (e) { setMessage(e.message, true); }
  };

  document.getElementById('test-admit').onclick = async function () {
    try {
      var id = accountId();
      var playerId = playerInput.value.trim() || id;
      var twoFactorCode = document.getElementById('admit-2fa').value.trim() || undefined;
      var result = await api('/api/v1/session/admit', {
        method: 'POST',
        body: JSON.stringify({
          playerId: playerId,
          displayName: playerId,
          nlAccountId: id,
          twoFactorCode: twoFactorCode,
        }),
      });
      document.getElementById('admit-result').textContent = JSON.stringify(result, null, 2);
      setMessage(result.admit ? 'Admit allowed' : 'Admit denied: ' + (result.reason || ''), !result.admit);
    } catch (e) { setMessage(e.message, true); }
  };

  loadQueryParams();
  var saved = localStorage.getItem('nlAccountId');
  if (saved && !accountInput.value) accountInput.value = saved;
  if (saved && !playerInput.value) playerInput.value = saved;
  refreshStatus().catch(function () { });
})();
