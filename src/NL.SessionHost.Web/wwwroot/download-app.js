fetch('/api/v1/distribution/client-manifest')
  .then(function (r) { return r.json(); })
  .then(function (m) {
    document.getElementById('version-line').textContent = 'Version ' + m.version + ' · scheme ' + m.deepLinkScheme;
    document.getElementById('manifest-block').textContent = JSON.stringify(m, null, 2);
    var win = (m.releases || []).find(function (x) { return x.platform === 'win-x64'; });
    if (win && win.packageAvailable) {
      var a = document.getElementById('download-link');
      a.href = win.downloadUrl;
      a.textContent = 'Download NL Client (Windows x64)';
    } else {
      document.getElementById('download-link').textContent = 'Package building — run build-nl-client-package.ps1';
    }
  })
  .catch(function (err) {
    document.getElementById('version-line').textContent = 'Manifest unavailable: ' + err.message;
  });
