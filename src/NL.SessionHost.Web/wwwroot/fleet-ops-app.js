(function () {
  const statusEl = document.getElementById('status');
  const settingsEl = document.getElementById('fleet-settings');
  const sloBody = document.querySelector('#slo-table tbody');
  const regionsBody = document.querySelector('#regions-table tbody');
  const incidentsBody = document.querySelector('#incidents-table tbody');
  const obsEl = document.getElementById('observability');
  const autoscaleEl = document.getElementById('autoscale');
  const manifestEl = document.getElementById('manifest-preview');
  const validationSummary = document.getElementById('validation-summary');
  const validationBody = document.querySelector('#validation-table tbody');
  const runValidationBtn = document.getElementById('run-validation');

  function setStatus(msg, kind) {
    statusEl.textContent = msg;
    statusEl.className = 'status ' + (kind || 'muted');
  }

  async function api(path) {
    const res = await fetch(path, { headers: NlAuth.headers() });
    const text = await res.text();
    let body;
    try { body = JSON.parse(text); } catch { body = text; }
    if (!res.ok) throw new Error(body.error || res.statusText);
    return body;
  }

  function renderSettings(s) {
    settingsEl.innerHTML = Object.entries(s)
      .filter(function (e) { return typeof e[1] !== 'object'; })
      .map(function (e) { return '<dt>' + e[0] + '</dt><dd>' + e[1] + '</dd>'; })
      .join('');
  }

  function renderSlos(list) {
    sloBody.innerHTML = '';
    (list || []).forEach(function (s) {
      var tr = document.createElement('tr');
      tr.innerHTML =
        '<td>' + s.name + '</td>' +
        '<td>' + s.target + ' ' + s.unit + '</td>' +
        '<td>' + s.current + '</td>' +
        '<td>' + (s.met ? '✓' : '✗') + '</td>';
      sloBody.appendChild(tr);
    });
  }

  function renderRegions(list) {
    regionsBody.innerHTML = '';
    (list || []).forEach(function (r) {
      var tr = document.createElement('tr');
      tr.innerHTML = '<td>' + r.id + '</td><td>' + r.displayName + '</td><td>' + r.latencyBiasMs + '</td>';
      regionsBody.appendChild(tr);
    });
  }

  function renderIncidents(list) {
    incidentsBody.innerHTML = '';
    (list || []).forEach(function (i) {
      var tr = document.createElement('tr');
      tr.innerHTML =
        '<td>' + i.incidentId + '</td>' +
        '<td>' + i.kind + '</td>' +
        '<td>' + i.sessionId + '</td>' +
        '<td>' + i.streamerId + '</td>' +
        '<td>' + (i.autoRestartAttempted ? 'yes' : 'no') + '</td>' +
        '<td>' + i.detectedAtUtc + '</td>';
      incidentsBody.appendChild(tr);
    });
  }

  function renderValidation(report) {
    if (!report) return;
    validationSummary.textContent = (report.stagingPassed ? 'Staging: PASS' : 'Staging: FAIL')
      + ' | Production ready: ' + (report.productionReady ? 'yes' : 'no');
    validationSummary.className = 'status ' + (report.stagingPassed ? 'ok' : 'error');
    validationBody.innerHTML = '';
    (report.checks || []).forEach(function (c) {
      var tr = document.createElement('tr');
      tr.innerHTML =
        '<td>' + c.description + '</td>' +
        '<td>' + (c.passed ? '✓' : '✗') + '</td>' +
        '<td>' + (c.detail || '') + '</td>';
      validationBody.appendChild(tr);
    });
  }

  async function loadValidation() {
    var report = await api('/api/v1/fleet/validation');
    renderValidation(report);
  }

  async function refresh() {
    var settings = await api('/api/v1/fleet/settings');
    renderSettings(settings);
    var slos = await api('/api/v1/fleet/slos');
    renderSlos(slos);
    var obs = await api('/api/v1/fleet/observability');
    obsEl.textContent = JSON.stringify(obs, null, 2);
    var regions = await api('/api/v1/fleet/regions');
    renderRegions(regions);
    var autoscale = await api('/api/v1/fleet/autoscale');
    autoscaleEl.textContent = JSON.stringify(autoscale, null, 2);
    var incidents = await api('/api/v1/fleet/incidents?count=20');
    renderIncidents(incidents);
    var manifest = await api('/api/v1/session/manifest');
    manifestEl.textContent = JSON.stringify({
      forkConnectEndpoint: manifest.forkConnectEndpoint,
      fleetRegionId: manifest.fleetRegionId,
      fleetTurnUri: manifest.fleetTurnUri,
      forkSessionId: manifest.forkSessionId,
    }, null, 2);
    await loadValidation().catch(function () {});
  }

  runValidationBtn.addEventListener('click', function () {
    setStatus('Running validation…');
    fetch('/api/v1/fleet/validation/run', { method: 'POST', headers: NlAuth.headers() })
      .then(function (r) { return r.json(); })
      .then(function (report) {
        renderValidation(report);
        setStatus('Validation saved.');
      })
      .catch(function (e) { setStatus(e.message, 'error'); });
  });

  refresh().catch(function (e) { setStatus(e.message, 'error'); });
  setInterval(function () { refresh().catch(function () {}); }, 10000);
})();
