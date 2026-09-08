(function () {
  var storageKey = 'nl-cookie-consent-v1';
  var bannerId = 'nl-cookie-consent';

  function ensureBanner() {
    if (localStorage.getItem(storageKey) === 'accepted') {
      return;
    }

    if (document.getElementById(bannerId)) {
      return;
    }

    var bar = document.createElement('div');
    bar.id = bannerId;
    bar.setAttribute('role', 'dialog');
    bar.setAttribute('aria-label', 'Cookie consent');
    bar.style.cssText = 'position:fixed;bottom:0;left:0;right:0;padding:1rem 1.25rem;background:#1a1a2e;color:#eee;border-top:1px solid #333;z-index:9999;display:flex;flex-wrap:wrap;gap:1rem;align-items:center;justify-content:space-between;font-size:0.95rem;';

    var text = document.createElement('span');
    text.innerHTML = 'We use essential cookies and local storage for session continuity. See our <a href="/cookie-policy.html" style="color:#8cf">Cookie Policy</a> and <a href="/privacy.html" style="color:#8cf">Privacy Policy</a>.';

    var btn = document.createElement('button');
    btn.type = 'button';
    btn.textContent = 'Accept';
    btn.style.cssText = 'padding:0.5rem 1rem;cursor:pointer;border:none;border-radius:4px;background:#4a9eff;color:#fff;font-weight:600;';
    btn.onclick = function () {
      localStorage.setItem(storageKey, 'accepted');
      bar.remove();
    };

    bar.appendChild(text);
    bar.appendChild(btn);
    document.body.appendChild(bar);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ensureBanner);
  } else {
    ensureBanner();
  }
})();
