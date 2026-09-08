(function () {

  const statusEl = document.getElementById('status');

  const playerInput = document.getElementById('player-id');

  const linkBlock = document.getElementById('link-block');



  function setStatus(msg, err) {

    statusEl.textContent = msg;

    statusEl.className = 'status ' + (err ? 'error' : 'ok');

  }



  async function api(path, opts) {

    const res = await fetch(path, Object.assign({

      headers: Object.assign({ 'Content-Type': 'application/json' }, (opts && opts.headers) || {}),

    }, opts || {}));

    const body = await res.json().catch(function () { return {}; });

    if (!res.ok) throw new Error(body.error || res.statusText);

    return body;

  }



  function loadQueryParams() {

    var params = new URLSearchParams(window.location.search);

    if (params.get('linked') === 'twitch') {

      setStatus('Twitch linked: ' + (params.get('twitchLogin') || params.get('twitchUserId') || 'ok'));

      if (params.get('playerId')) {

        playerInput.value = params.get('playerId');

        localStorage.setItem('nlPlayerId', params.get('playerId'));

      }

      window.history.replaceState({}, '', '/social-link.html');

    }

    if (params.get('linked') === 'discord') {

      setStatus('Discord linked: ' + (params.get('discordUsername') || params.get('discordUserId') || 'ok'));

      if (params.get('playerId')) {

        playerInput.value = params.get('playerId');

        localStorage.setItem('nlPlayerId', params.get('playerId'));

      }

      window.history.replaceState({}, '', '/social-link.html');

    }

    if (params.get('linked') === 'youtube') {

      setStatus('YouTube linked: ' + (params.get('youtubeChannelTitle') || params.get('youtubeChannelId') || 'ok'));

      if (params.get('playerId')) {

        playerInput.value = params.get('playerId');

        localStorage.setItem('nlPlayerId', params.get('playerId'));

      }

      window.history.replaceState({}, '', '/social-link.html');

    }

    if (params.get('linked') === 'kick') {

      setStatus('Kick linked: ' + (params.get('kickUsername') || params.get('kickUserId') || 'ok'));

      if (params.get('playerId')) {

        playerInput.value = params.get('playerId');

        localStorage.setItem('nlPlayerId', params.get('playerId'));

      }

      window.history.replaceState({}, '', '/social-link.html');

    }

    if (params.get('error')) {

      setStatus(decodeURIComponent(params.get('error')), true);

      window.history.replaceState({}, '', '/social-link.html');

    }

  }



  async function loadSettings() {

    var s = await api('/api/v1/social/settings');

    var dl = document.getElementById('social-settings');

    dl.innerHTML = Object.entries(s).filter(function (e) { return typeof e[1] !== 'object'; })

      .map(function (e) { return '<dt>' + e[0] + '</dt><dd>' + e[1] + '</dd>'; }).join('');

  }



  async function refreshLinks() {

    var id = playerInput.value.trim() || localStorage.getItem('nlPlayerId');

    if (!id) {

      linkBlock.textContent = 'Enter a player id first.';

      return;

    }

    playerInput.value = id;

    localStorage.setItem('nlPlayerId', id);



    var links = await api('/api/v1/social/links/' + encodeURIComponent(id));

    var twitchOAuth = null;

    var discordOAuth = null;

    var youtubeOAuth = null;

    var kickOAuth = null;

    try { twitchOAuth = await api('/api/v1/social/twitch-oauth/' + encodeURIComponent(id)); } catch (e) { twitchOAuth = { linked: false }; }

    try { discordOAuth = await api('/api/v1/social/discord-oauth/' + encodeURIComponent(id)); } catch (e) { discordOAuth = { linked: false }; }

    try { youtubeOAuth = await api('/api/v1/social/youtube-oauth/' + encodeURIComponent(id)); } catch (e) { youtubeOAuth = { linked: false }; }

    try { kickOAuth = await api('/api/v1/social/kick-oauth/' + encodeURIComponent(id)); } catch (e) { kickOAuth = { linked: false }; }



    linkBlock.textContent = JSON.stringify({

      links: links,

      twitchOAuth: twitchOAuth,

      discordOAuth: discordOAuth,

      youtubeOAuth: youtubeOAuth,

      kickOAuth: kickOAuth,

    }, null, 2);

  }



  function startOAuth(path) {

    var playerId = playerInput.value.trim() || localStorage.getItem('nlPlayerId');

    if (!playerId) {

      setStatus('Enter a player id first.', true);

      return;

    }

    localStorage.setItem('nlPlayerId', playerId);

    var returnUrl = encodeURIComponent('/social-link.html');

    window.location.href = path + '?playerId=' + encodeURIComponent(playerId) + '&returnUrl=' + returnUrl;

  }



  document.getElementById('refresh-links').onclick = function () {

    refreshLinks().catch(function (e) { setStatus(e.message, true); });

  };



  document.getElementById('twitch-sign-in').onclick = function () {

    startOAuth('/api/v1/social/oauth/twitch/authorize');

  };



  document.getElementById('discord-sign-in').onclick = function () {

    startOAuth('/api/v1/social/oauth/discord/authorize');

  };



  document.getElementById('youtube-sign-in').onclick = function () {

    startOAuth('/api/v1/social/oauth/youtube/authorize');

  };



  document.getElementById('kick-sign-in').onclick = function () {

    startOAuth('/api/v1/social/oauth/kick/authorize');

  };



  document.getElementById('test-admit').onclick = async function () {

    try {

      var playerId = playerInput.value.trim() || localStorage.getItem('nlPlayerId');

      if (!playerId) {

        setStatus('Enter a player id first.', true);

        return;

      }

      var links = await api('/api/v1/social/links/' + encodeURIComponent(playerId));

      var result = await api('/api/v1/session/admit', {

        method: 'POST',

        body: JSON.stringify({

          playerId: playerId,

          displayName: playerId,

          twitchUserId: links.twitchUserId,

          youtubeChannelId: links.youtubeChannelId,

          kickUserId: links.kickUserId,

          discordUserId: links.discordUserId,

        }),

      });

      document.getElementById('admit-result').textContent = JSON.stringify(result, null, 2);

      setStatus(result.admit ? 'Admit allowed' : 'Admit denied: ' + (result.reason || ''), !result.admit);

    } catch (e) { setStatus(e.message, true); }

  };



  loadQueryParams();

  var saved = localStorage.getItem('nlPlayerId');

  if (saved && !playerInput.value) playerInput.value = saved;

  loadSettings().then(refreshLinks).catch(function (e) { setStatus(e.message, true); });

})();

