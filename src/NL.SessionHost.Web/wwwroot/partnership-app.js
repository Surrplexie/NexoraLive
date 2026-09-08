(function () {
  const statusEl = document.getElementById('status');

  function setStatus(msg, err) {
    statusEl.textContent = msg;
    statusEl.className = 'status ' + (err ? 'error' : 'muted');
  }

  async function api(path, opts) {
    const res = await fetch(path, Object.assign({ headers: window.NlAuth.authHeaders() }, opts || {}));
    const body = await res.json().catch(function () { return {}; });
    if (!res.ok) throw new Error(body.error || res.statusText);
    return body;
  }

  async function refresh() {
    const settings = await api('/api/v1/partnership/settings');
    document.getElementById('partnership-settings').innerHTML = Object.entries(settings)
      .map(function (e) { return '<dt>' + e[0] + '</dt><dd>' + e[1] + '</dd>'; }).join('');

    const pubs = await api('/api/v1/partnership/publishers');
    var tbody = document.querySelector('#publishers-table tbody');
    tbody.innerHTML = '';
    (pubs || []).forEach(function (p) {
      var tr = document.createElement('tr');
      var titles = (p.titles || p.Titles || []).map(function (t) {
        return (t.gameId || t.GameId) + ' (' + (t.tier || t.Tier) + ')';
      }).join(', ');
      tr.innerHTML = '<td>' + (p.publisherId || p.PublisherId) + '</td><td>' + (p.displayName || p.DisplayName) + '</td><td>' + titles + '</td><td><button data-id="' + (p.publisherId || p.PublisherId) + '">Dashboard</button></td>';
      tbody.appendChild(tr);
    });
    tbody.querySelectorAll('button[data-id]').forEach(function (btn) {
      btn.onclick = async function () {
        var dash = await api('/api/v1/partnership/dashboard/' + btn.dataset.id);
        setStatus('Join count: ' + dash.sessionJoinCount + ', bans: ' + dash.activeBanCount);
      };
    });

    var optin = await api('/api/v1/partnership/platform-opt-in');
    var otbody = document.querySelector('#optin-table tbody');
    otbody.innerHTML = '';
    (optin || []).forEach(function (e) {
      var tr = document.createElement('tr');
      tr.innerHTML = '<td>' + e.platform + '</td><td>' + e.appId + '</td><td>' + e.gameId + '</td><td>' + e.tier + '</td>';
      otbody.appendChild(tr);
    });

    var spec = await api('/api/v1/partnership/sdk/spec');
    document.getElementById('sdk-spec').textContent = JSON.stringify(spec, null, 2);
  }

  refresh().catch(function (e) { setStatus(e.message, true); });
})();
