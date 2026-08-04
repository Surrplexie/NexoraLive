async function api(path, options) {
  const res = await fetch(path, options);
  const text = await res.text();
  var data = null;
  try { data = text ? JSON.parse(text) : null; } catch (e) { data = { raw: text }; }
  if (!res.ok) throw new Error((data && data.error) || res.statusText);
  return data;
}

async function refreshStatus() {
  const status = await api('/api/v1/beta/status');
  const el = document.getElementById('beta-status');
  if (!status.enabled) {
    el.textContent = 'Beta program is not active on this host.';
    document.getElementById('signup-form').hidden = true;
    return;
  }
  el.textContent = 'Waitlist ' + (status.waitlistOpen ? 'open' : 'closed')
    + ' — ' + status.approvedCount + '/' + status.maxApprovedStreamers + ' streamer slots filled'
    + ' (' + status.remainingSlots + ' remaining).';
  document.getElementById('signup-form').hidden = !status.waitlistOpen;
}

document.getElementById('signup-form').onsubmit = async function (ev) {
  ev.preventDefault();
  const result = document.getElementById('signup-result');
  result.textContent = 'Submitting...';
  try {
    const entry = await api('/api/v1/beta/waitlist', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        displayName: document.getElementById('display-name').value,
        contact: document.getElementById('contact').value,
        twitchHandle: document.getElementById('twitch').value || null,
        requestedGameId: document.getElementById('game').value || null,
      }),
    });
    result.textContent = 'Thanks! Entry ' + entry.id + ' is ' + entry.status + '.';
    await refreshStatus();
  } catch (err) {
    result.textContent = 'Error: ' + err.message;
  }
};

refreshStatus().catch(function (err) {
  document.getElementById('beta-status').textContent = 'Could not load beta status: ' + err.message;
});
