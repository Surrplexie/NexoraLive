async function api(path, options) {
  const res = await fetch(path, options);
  const text = await res.text();
  var data = null;
  try { data = text ? JSON.parse(text) : null; } catch (e) { data = { raw: text }; }
  if (!res.ok) throw new Error((data && data.error) || res.statusText);
  return data;
}

async function refreshStatus() {
  const status = await api('/api/v1/ga/status');
  const el = document.getElementById('ga-status');
  if (!status.enabled) {
    el.textContent = 'General availability is not active on this host.';
    document.getElementById('signup-form').hidden = true;
    return;
  }
  el.textContent = 'Open signup ' + (status.openSignup ? 'enabled' : 'closed')
    + ' — ' + status.catalogGameCount + '/' + status.requiredCatalogGames + ' catalog games'
    + ' — SLA tier: ' + status.slaTier + '.';
  document.getElementById('signup-form').hidden = !status.openSignup;
}

async function refreshCatalog() {
  const catalog = await api('/api/v1/ga/catalog');
  const list = document.getElementById('catalog-list');
  list.innerHTML = '';
  if (!catalog.enabled || !catalog.games) return;
  catalog.games.forEach(function (g) {
    const li = document.createElement('li');
    li.textContent = g.displayName + ' (' + g.gameId + ') — ' + (g.majorVersion || '?') + ' [' + (g.tier || '') + ']';
    list.appendChild(li);
  });
}

document.getElementById('signup-form').onsubmit = async function (ev) {
  ev.preventDefault();
  const result = document.getElementById('signup-result');
  result.textContent = 'Submitting...';
  try {
    const entry = await api('/api/v1/ga/streamers/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        displayName: document.getElementById('display-name').value,
        contact: document.getElementById('contact').value,
        twitchHandle: document.getElementById('twitch').value || null,
        preferredGameId: document.getElementById('game').value || null,
      }),
    });
    result.textContent = 'Registered! Streamer ID: ' + entry.streamerId + ' — use this on /operator.html to go live.';
    await refreshStatus();
  } catch (err) {
    result.textContent = 'Error: ' + err.message;
  }
};

Promise.all([refreshStatus(), refreshCatalog()]).catch(function (err) {
  document.getElementById('ga-status').textContent = 'Could not load GA status: ' + err.message;
});
