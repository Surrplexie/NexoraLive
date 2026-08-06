(function () {
  const statusEl = document.getElementById('status');
  const accountInput = document.getElementById('account-id');
  const linkList = document.getElementById('link-list');

  function setStatus(msg, err) {
    statusEl.textContent = msg;
    statusEl.className = 'status ' + (err ? 'error' : 'ok');
  }

  function authHeaders() {
    return window.NlAuth && window.NlAuth.headers ? window.NlAuth.headers() : {};
  }

  async function api(path, opts) {
    const res = await fetch(path, Object.assign({
      headers: Object.assign({ 'Content-Type': 'application/json' }, authHeaders(), (opts && opts.headers) || {}),
    }, opts || {}));
    const body = await res.json().catch(function () { return {}; });
    if (!res.ok) throw new Error(body.error || res.statusText);
    return body;
  }

  function loadQueryParams() {
    var params = new URLSearchParams(window.location.search);
    var linked = params.get('linked');
    if (linked) {
      setStatus(linked + ' linked: ' + (params.get('displayName') || params.get('platformUserId') || params.get('steamId') || 'ok'));
      if (params.get('accountId')) {
        accountInput.value = params.get('accountId');
        localStorage.setItem('nlAccountId', params.get('accountId'));
      }
      if (params.get('platformUserId')) {
        document.getElementById('verify-platform-user').value = params.get('platformUserId');
      }
      if (params.get('steamId')) {
        document.getElementById('verify-platform-user').value = params.get('steamId');
        document.getElementById('verify-platform').value = 'steam';
      }
      window.history.replaceState({}, '', '/identity-link.html');
    }
    if (params.get('error')) {
      setStatus(decodeURIComponent(params.get('error')), true);
      window.history.replaceState({}, '', '/identity-link.html');
    }
  }

  async function loadSettings() {
    var s = await api('/api/v1/identity/settings');
    var dl = document.getElementById('identity-settings');
    dl.innerHTML = Object.entries(s).filter(function (e) { return typeof e[1] !== 'object'; })
      .map(function (e) { return '<dt>' + e[0] + '</dt><dd>' + e[1] + '</dd>'; }).join('');
  }

  async function refreshAccount() {
    var id = accountInput.value.trim() || localStorage.getItem('nlAccountId');
    if (!id) return;
    accountInput.value = id;
    var acct = await api('/api/v1/identity/accounts/' + encodeURIComponent(id));
    linkList.innerHTML = '';
    (acct.links || []).forEach(function (l) {
      var li = document.createElement('li');
      li.textContent = l.platform + ': ' + l.externalUserId;
      linkList.appendChild(li);
      if (l.platform.toLowerCase() === 'steam') {
        document.getElementById('verify-platform').value = 'steam';
        document.getElementById('verify-platform-user').value = l.externalUserId;
      }
    });
  }

  function startOAuth(path) {
    var accountId = accountInput.value.trim() || localStorage.getItem('nlAccountId');
    if (!accountId) {
      setStatus('Create an NL account first.', true);
      return;
    }
    var returnUrl = encodeURIComponent('/identity-link.html');
    window.location.href = path + '?accountId=' + encodeURIComponent(accountId) + '&returnUrl=' + returnUrl;
  }

  document.getElementById('create-account').onclick = async function () {
    try {
      var name = document.getElementById('display-name').value.trim();
      var acct = await api('/api/v1/identity/accounts', {
        method: 'POST',
        body: JSON.stringify({ displayName: name }),
      });
      accountInput.value = acct.accountId;
      localStorage.setItem('nlAccountId', acct.accountId);
      setStatus('Account created: ' + acct.accountId);
      await refreshAccount();
    } catch (e) { setStatus(e.message, true); }
  };

  document.getElementById('refresh-account').onclick = function () {
    refreshAccount().catch(function (e) { setStatus(e.message, true); });
  };

  document.getElementById('steam-sign-in').onclick = function () {
    startOAuth('/api/v1/identity/oauth/steam/authorize');
  };
  document.getElementById('epic-sign-in').onclick = function () {
    startOAuth('/api/v1/identity/oauth/epic/authorize');
  };
  document.getElementById('xbox-sign-in').onclick = function () {
    startOAuth('/api/v1/identity/oauth/xbox/authorize');
  };
  document.getElementById('playstation-sign-in').onclick = function () {
    startOAuth('/api/v1/identity/oauth/playstation/authorize');
  };

  document.getElementById('test-admit').onclick = async function () {
    try {
      var platform = document.getElementById('verify-platform').value;
      var platformUserId = document.getElementById('verify-platform-user').value.trim();
      var appId = document.getElementById('verify-appid').value.trim();
      var accountId = accountInput.value.trim() || localStorage.getItem('nlAccountId');
      var result = await api('/api/v1/session/admit', {
        method: 'POST',
        body: JSON.stringify({
          playerId: 'identity-test',
          displayName: 'Identity Test',
          platform: platform,
          platformUserId: platformUserId,
          appId: appId,
          nlAccountId: accountId,
        }),
      });
      document.getElementById('admit-result').textContent = JSON.stringify(result, null, 2);
      setStatus(result.admit ? 'Admit allowed' : 'Admit denied: ' + (result.reason || result.ownershipStatus), !result.admit);
    } catch (e) { setStatus(e.message, true); }
  };

  loadQueryParams();
  var saved = localStorage.getItem('nlAccountId');
  if (saved && !accountInput.value) accountInput.value = saved;
  loadSettings().then(refreshAccount).catch(function (e) { setStatus(e.message, true); });
})();
