async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: {
      "Content-Type": "application/json",
      ...window.NlAuth.authHeaders(),
      ...(options.headers || {}),
    },
    ...options,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || res.statusText);
  }
  return res.json();
}

function streamerId() {
  return document.getElementById("streamer").value.trim() || "default-streamer";
}

function setStatus(msg, isError = false) {
  const el = document.getElementById("status");
  el.textContent = msg;
  el.style.color = isError ? "#f88" : "";
}

async function loadRequirements() {
  const req = await api("/api/v1/social/join-requirements");
  document.getElementById("require-follow").checked = req.requireFollow ?? req.RequireFollow ?? false;
  document.getElementById("require-sub").checked = req.requireSubscription ?? req.RequireSubscription ?? false;
  document.getElementById("require-discord").checked = req.requireDiscordMember ?? req.RequireDiscordMember ?? false;
  document.getElementById("min-age").value = req.minAccountAgeDays ?? req.MinAccountAgeDays ?? 0;
  document.getElementById("max-offenses").value = req.maxActiveOffenses ?? req.MaxActiveOffenses ?? 999999;
  document.getElementById("graylist-hold").checked = req.allowGraylistWithHold ?? req.AllowGraylistWithHold ?? true;
}

async function loadStreamerConfig() {
  const cfg = await api(`/api/v1/social/streamer-config?streamer=${encodeURIComponent(streamerId())}`);
  document.getElementById("twitch-id").value = cfg.twitchBroadcasterId ?? cfg.TwitchBroadcasterId ?? "";
  document.getElementById("youtube-id").value = cfg.youTubeChannelId ?? cfg.YouTubeChannelId ?? "";
  document.getElementById("kick-slug").value = cfg.kickSlug ?? cfg.KickSlug ?? "";
  document.getElementById("discord-guild").value = cfg.discordGuildId ?? cfg.DiscordGuildId ?? "";
  document.getElementById("require-live").checked = cfg.requireLiveToStart ?? cfg.RequireLiveToStart ?? false;
  const platform = cfg.livePlatform ?? cfg.LivePlatform ?? "";
  document.getElementById("live-platform").value = platform ? String(platform).toLowerCase() : "";
}

async function loadSocialSettings() {
  const s = await api("/api/v1/social/settings");
  document.getElementById("social-settings").innerHTML = `
    <dt>Enabled</dt><dd>${s.enabled}</dd>
    <dt>Mode</dt><dd>${s.mode}</dd>
    <dt>Cache TTL</dt><dd>${s.cacheTtlSeconds}s</dd>
    <dt>Live check interval</dt><dd>${s.liveCheckIntervalSeconds}s</dd>
    <dt>Twitch configured</dt><dd>${s.twitchConfigured}</dd>
    <dt>Store</dt><dd>${s.storePath}</dd>`;
}

async function refreshLiveStatus() {
  const live = await api(`/api/v1/social/live-status?streamer=${encodeURIComponent(streamerId())}`);
  const isLive = live.isLive ?? live.IsLive;
  const title = live.title ?? live.Title ?? "";
  const platform = live.platform ?? live.Platform ?? "?";
  document.getElementById("live-status").textContent =
    isLive ? `LIVE on ${platform}${title ? `: ${title}` : ""}` : "Offline";
}

document.getElementById("save-requirements").onclick = async () => {
  const body = {
    requireFollow: document.getElementById("require-follow").checked,
    requireSubscription: document.getElementById("require-sub").checked,
    requireDiscordMember: document.getElementById("require-discord").checked,
    minAccountAgeDays: Number(document.getElementById("min-age").value) || 0,
    maxActiveOffenses: Number(document.getElementById("max-offenses").value) || 999999,
    allowGraylistWithHold: document.getElementById("graylist-hold").checked,
    requiredVerification: "None",
  };
  await api("/api/v1/social/join-requirements", { method: "PUT", body: JSON.stringify(body) });
  setStatus("Join requirements saved.");
};

document.getElementById("save-streamer").onclick = async () => {
  const platform = document.getElementById("live-platform").value.trim();
  const body = {
    streamerId: streamerId(),
    twitchBroadcasterId: document.getElementById("twitch-id").value.trim() || null,
    youTubeChannelId: document.getElementById("youtube-id").value.trim() || null,
    kickSlug: document.getElementById("kick-slug").value.trim() || null,
    discordGuildId: document.getElementById("discord-guild").value.trim() || null,
    requireLiveToStart: document.getElementById("require-live").checked,
    livePlatform: platform || null,
  };
  await api("/api/v1/social/streamer-config", { method: "PUT", body: JSON.stringify(body) });

  const profile = await api("/api/v1/session");
  const p = profile.profile ?? profile.Profile ?? {};
  p.requireLiveStream = document.getElementById("require-live").checked;
  p.socialGateEnabled = true;
  await api("/api/v1/session/profile", { method: "PUT", body: JSON.stringify(p) });

  setStatus("Streamer social config saved.");
  await refreshLiveStatus();
};

document.getElementById("refresh-live").onclick = refreshLiveStatus;
document.getElementById("streamer").onchange = async () => {
  await loadStreamerConfig();
  await refreshLiveStatus();
};

loadRequirements();
loadStreamerConfig();
loadSocialSettings();
refreshLiveStatus();
window.NlAuth.initOperatorAuthUi();
