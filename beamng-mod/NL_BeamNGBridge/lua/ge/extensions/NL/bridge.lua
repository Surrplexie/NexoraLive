-- NL BeamNG Bridge (game-engine extension)
-- Emits sparse NDJSON semantic events for NexoraLive (NL) and applies Block actions
-- from NL over localhost UDP. See docs/BEAMNG.md in the NL repo.
--
-- Install: copy/symlink NL_BeamNGBridge into <BeamNG user folder>/mods/unpacked/
-- then enable the mod and load a map. Events append to:
--   %LOCALAPPDATA%/NL/beamng-events.ndjson  (Windows)
-- Commands arrive on UDP 127.0.0.1:<cmdPort> with magic header "SCBN1".
-- Optional overrides: bridge.json next to this mod (cmdPort + emit thresholds).

local M = {}

local EVENT_PATH = nil
local KICK_QUEUE_PATH = nil
local CMD_PORT = 27022
local MAGIC = "SCBN1"

local MOVE_INTERVAL = 0.35
local CRASH_DV_THRESHOLD = 10.0
local AIRTIME_THRESHOLD = 1.5
local ROLLOVER_THRESHOLD = 1.75
local BOUNDARY = {
  minX = -5000, maxX = 5000,
  minY = -5000, maxY = 5000,
  minZ = -200,  maxZ = 2000,
}

local playerName = "Driver"
local sessionActive = false
local lastMoveT = 0
local lastSpeed = 0
local airtimeAcc = 0
local rolloverAcc = 0
local outsideBoundary = false
local beammpEnabled = false
local beammpPlayers = {} -- name(lower) -> playerId

local udpSock = nil
local udpReady = false

local function ensureDir(path)
  local sep = package.config:sub(1, 1)
  local acc = ""
  for part in string.gmatch(path, "[^/\\" .. sep .. "]+") do
    if acc == "" and part:match("^%a:$") then
      acc = part
    else
      acc = (acc == "" and part) or (acc .. sep .. part)
      FS and FS.directoryCreate and FS.directoryCreate(acc)
    end
  end
end

local function nlDir()
  local localApp = os.getenv("LOCALAPPDATA") or os.getenv("HOME") or "."
  local sep = package.config:sub(1, 1)
  local dir = localApp .. sep .. "NL"
  ensureDir(dir)
  return dir, sep
end

local function resolveEventPath()
  local dir, sep = nlDir()
  return dir .. sep .. "beamng-events.ndjson"
end

local function resolveKickQueuePath()
  local dir, sep = nlDir()
  return dir .. sep .. "beamng-kicks.ndjson"
end

local function modRoot()
  -- BeamNG often exposes this extension path; fall back to relative unpack layout.
  if M.__extensionPath then
    return M.__extensionPath
  end
  return nil
end

local function loadBridgeJson()
  local candidates = {}
  local root = modRoot()
  if root then
    table.insert(candidates, root .. "/bridge.json")
    table.insert(candidates, root .. "\\bridge.json")
  end
  -- Unpacked mod typical layout: .../mods/unpacked/NL_BeamNGBridge/bridge.json
  local localApp = os.getenv("LOCALAPPDATA")
  if localApp then
    -- BeamNG 0.38+ user data
    table.insert(candidates, localApp .. "\\BeamNG\\BeamNG.drive\\current\\mods\\unpacked\\NL_BeamNGBridge\\bridge.json")
    for _, ver in ipairs({ "", "0.38", "0.37", "0.36", "0.35", "0.34", "0.33" }) do
      local base = localApp .. "\\BeamNG.drive"
      if ver ~= "" then
        base = base .. "\\" .. ver
      end
      table.insert(candidates, base .. "\\mods\\unpacked\\NL_BeamNGBridge\\bridge.json")
    end
  end
  table.insert(candidates, "bridge.json")

  for _, path in ipairs(candidates) do
    local f = io.open(path, "r")
    if f then
      local raw = f:read("*a")
      f:close()
      if raw and #raw > 0 then
        local ok, cfg = pcall(function()
          if json and json.decode then
            return json.decode(raw)
          end
          if Json and JsonDecode then
            return JsonDecode(raw)
          end
          return nil
        end)
        if ok and type(cfg) == "table" then
          if type(cfg.cmdPort) == "number" then
            CMD_PORT = math.floor(cfg.cmdPort)
          end
          if type(cfg.moveInterval) == "number" then
            MOVE_INTERVAL = cfg.moveInterval
          end
          if type(cfg.crashDvThreshold) == "number" then
            CRASH_DV_THRESHOLD = cfg.crashDvThreshold
          end
          if type(cfg.airtimeThreshold) == "number" then
            AIRTIME_THRESHOLD = cfg.airtimeThreshold
          end
          if type(cfg.rolloverThreshold) == "number" then
            ROLLOVER_THRESHOLD = cfg.rolloverThreshold
          end
          if type(cfg.boundary) == "table" then
            BOUNDARY.minX = cfg.boundary.minX or BOUNDARY.minX
            BOUNDARY.maxX = cfg.boundary.maxX or BOUNDARY.maxX
            BOUNDARY.minY = cfg.boundary.minY or BOUNDARY.minY
            BOUNDARY.maxY = cfg.boundary.maxY or BOUNDARY.maxY
            BOUNDARY.minZ = cfg.boundary.minZ or BOUNDARY.minZ
            BOUNDARY.maxZ = cfg.boundary.maxZ or BOUNDARY.maxZ
          end
          log("I", "NL", "Loaded bridge.json from " .. path)
          return
        end
      end
    end
  end
end

local function nowMs()
  return math.floor((os.time() * 1000) + ((os.clock() % 1) * 1000))
end

local function jsonEscape(s)
  s = tostring(s or "")
  s = s:gsub("\\", "\\\\"):gsub('"', '\\"'):gsub("\n", "\\n"):gsub("\r", "\\r")
  return s
end

local function appendEvent(eventName, props)
  if not EVENT_PATH then
    EVENT_PATH = resolveEventPath()
  end
  local parts = {
    string.format('"event":"%s"', jsonEscape(eventName)),
    string.format('"player":"%s"', jsonEscape(playerName)),
    string.format('"ts":%d', nowMs()),
  }
  if props and next(props) then
    local propParts = {}
    for k, v in pairs(props) do
      if type(v) == "number" then
        table.insert(propParts, string.format('"%s":%.6g', k, v))
      elseif type(v) == "boolean" then
        table.insert(propParts, string.format('"%s":%s', k, v and "true" or "false"))
      end
    end
    table.insert(parts, '"props":{' .. table.concat(propParts, ",") .. "}")
  end
  local line = "{" .. table.concat(parts, ",") .. "}\n"
  local f = io.open(EVENT_PATH, "a")
  if f then
    f:write(line)
    f:close()
  else
    log("W", "NL", "Failed to append NDJSON to " .. tostring(EVENT_PATH))
  end
end

local function toast(msg)
  if guihooks and guihooks.trigger then
    guihooks.trigger("toastrMsg", { type = "warning", title = "NL", msg = tostring(msg), config = { timeOut = 5000 } })
  end
  log("I", "NL", tostring(msg))
end

local function recoverLocal()
  -- Try several BeamNG recover entry points; APIs differ by version.
  local attempts = {
    function()
      if be and be.getPlayerVehicle then
        local veh = be:getPlayerVehicle(0)
        if veh and veh.queueLuaCommand then
          veh:queueLuaCommand("recovery.startRecovering()")
          return "vehicle.recovery.startRecovering"
        end
      end
    end,
    function()
      if be and be.getPlayerVehicle then
        local veh = be:getPlayerVehicle(0)
        if veh and veh.queueLuaCommand then
          veh:queueLuaCommand("obj:requestReset(RESET_PHYSICS)")
          return "vehicle.requestReset"
        end
      end
    end,
    function()
      if recovery and recovery.startRecovering then
        recovery.startRecovering()
        return "recovery.startRecovering"
      end
    end,
    function()
      if core_recovery and core_recovery.recover then
        core_recovery.recover()
        return "core_recovery.recover"
      end
    end,
  }

  for _, attempt in ipairs(attempts) do
    local ok, result = pcall(attempt)
    if ok and type(result) == "string" then
      log("I", "NL", "recover via " .. result)
      return true
    end
  end
  log("W", "NL", "recover failed — no known API succeeded")
  return false
end

local function enqueueBeamMpKick(who, reason)
  if not KICK_QUEUE_PATH then
    KICK_QUEUE_PATH = resolveKickQueuePath()
  end
  local line = string.format(
    '{"player":"%s","reason":"%s","ts":%d}\n',
    jsonEscape(who or ""),
    jsonEscape(reason or "NLEvents Block"),
    nowMs()
  )
  local f = io.open(KICK_QUEUE_PATH, "a")
  if f then
    f:write(line)
    f:close()
    log("I", "NL", "Queued BeamMP kick for " .. tostring(who) .. " → " .. KICK_QUEUE_PATH)
    return true
  end
  log("W", "NL", "Failed to write kick queue " .. tostring(KICK_QUEUE_PATH))
  return false
end

local function resolveBeamMpPlayerId(who)
  if not who or who == "" then
    return nil
  end
  local key = string.lower(tostring(who))
  if beammpPlayers[key] then
    return beammpPlayers[key]
  end
  -- Client-side BeamMP helpers when present
  if MPVehicleGE and MPVehicleGE.getPlayerByName then
    local ok, player, id = pcall(function()
      return MPVehicleGE.getPlayerByName(who)
    end)
    if ok and id then
      return id
    end
  end
  if MP and MP.GetPlayers then
    local ok, players = pcall(function() return MP.GetPlayers() end)
    if ok and type(players) == "table" then
      for id, name in pairs(players) do
        if type(name) == "string" and string.lower(name) == key then
          return id
        elseif type(name) == "table" and name.name and string.lower(tostring(name.name)) == key then
          return id
        end
      end
    end
  end
  return nil
end

local function tryBeamMpKick(who, reason)
  local id = resolveBeamMpPlayerId(who)
  local kicked = false

  if id ~= nil then
    if MP and MP.DropPlayer then
      local ok = pcall(function() MP.DropPlayer(id, reason or "NLEvents Block") end)
      if ok then
        log("I", "NL", "MP.DropPlayer(" .. tostring(id) .. ")")
        kicked = true
      end
    end
    if not kicked and DropPlayer then
      local ok = pcall(function() DropPlayer(id) end)
      if ok then
        log("I", "NL", "DropPlayer(" .. tostring(id) .. ")")
        kicked = true
      end
    end
  end

  -- Always enqueue for the companion BeamMP server plugin (authoritative kick path).
  local queued = enqueueBeamMpKick(who, reason)
  return kicked or queued
end

local function despawnOrKick(kind, who, reason)
  toast(string.format("%s %s: %s", kind, who or "?", reason or ""))
  if (kind == "kick" or kind == "despawn") and (beammpEnabled or next(beammpPlayers)) then
    if tryBeamMpKick(who, reason) then
      return
    end
    log("W", "NL", "kick fallback — BeamMP APIs unavailable; recovering local vehicle")
  end
  if kind == "despawn" or kind == "kick" or kind == "recover" then
    recoverLocal()
  end
end

local function handleCommand(raw)
  if type(raw) ~= "string" or #raw < 6 then
    return
  end
  if raw:sub(1, 5) ~= MAGIC then
    return
  end
  local body = raw:gsub("^SCBN1[\r\n]*", "")
  local action, who, msg = body:match("^([^|]+)|([^|]*)|(.*)$")
  if not action then
    action = body:match("^(%S+)")
  end
  action = (action or ""):lower()
  who = who or playerName
  msg = msg or ""
  if action == "warn" then
    toast(msg ~= "" and msg or "NLEvents Block")
  elseif action == "recover" then
    toast(msg ~= "" and msg or "NL recover")
    recoverLocal()
  elseif action == "despawn" or action == "kick" then
    despawnOrKick(action, who, msg)
  else
    log("W", "NL", "Unknown command: " .. tostring(action))
  end
end

local function ensureUdp()
  if udpSock then
    return
  end
  local ok, socket = pcall(require, "socket")
  if not ok or not socket then
    log("W", "NL", "LuaSocket not available — command channel disabled (events still emit).")
    toast("NL: LuaSocket missing — UDP commands disabled")
    return
  end
  local s = socket.udp()
  local bindOk, bindErr = pcall(function()
    s:setsockname("127.0.0.1", CMD_PORT)
  end)
  if not bindOk then
    log("E", "NL", "UDP bind failed on 127.0.0.1:" .. tostring(CMD_PORT) .. " — " .. tostring(bindErr))
    toast("NL: UDP port " .. tostring(CMD_PORT) .. " busy — set bridge.json cmdPort / Session Host BeamNG UDP")
    pcall(function() s:close() end)
    return
  end
  -- Some LuaSocket builds report bind errors via getpeername/getsockname only; probe receive.
  s:settimeout(0)
  udpSock = s
  udpReady = true
  log("I", "NL", "Listening for NL commands on UDP 127.0.0.1:" .. tostring(CMD_PORT))
end

local function pollCommands()
  if not udpSock then
    return
  end
  while true do
    local data, err = udpSock:receive()
    if not data then
      if err and err ~= "timeout" and err ~= "closed" then
        log("W", "NL", "UDP receive: " .. tostring(err))
      end
      break
    end
    handleCommand(data)
  end
end

local function getVehiclePose()
  if not be or not be.getPlayerVehicle then
    return nil
  end
  local veh = be:getPlayerVehicle(0)
  if not veh then
    return nil
  end
  local x, y, z = 0, 0, 0
  local speed = 0
  local inverted = false
  local airborne = false
  pcall(function()
    local pos = veh:getPosition()
    if pos then
      x, y, z = pos.x or pos[1] or 0, pos.y or pos[2] or 0, pos.z or pos[3] or 0
    end
    local vel = veh:getVelocity()
    if vel then
      local vx, vy, vz = vel.x or vel[1] or 0, vel.y or vel[2] or 0, vel.z or vel[3] or 0
      speed = math.sqrt(vx * vx + vy * vy + vz * vz)
    end
    if veh.getDirectionVectorUp then
      local up = veh:getDirectionVectorUp()
      if up and (up.z or up[3] or 1) < 0.15 then
        inverted = true
      end
    end
    if veh.isAirborne then
      airborne = veh:isAirborne()
    elseif speed > 2 and z > 5 then
      airborne = true
    end
  end)
  return x, y, z, speed, inverted, airborne
end

local function detectBeamMP()
  beammpEnabled = (MPVehicleGE ~= nil) or (MPGameNetwork ~= nil) or (type(MP) == "table")
end

function M.onExtensionLoaded()
  loadBridgeJson()
  EVENT_PATH = resolveEventPath()
  KICK_QUEUE_PATH = resolveKickQueuePath()
  playerName = (getPlayerServerName and getPlayerServerName()) or (Steam and Steam.playerName) or "Driver"
  ensureUdp()
  detectBeamMP()
  log("I", "NL", "NL_BeamNGBridge loaded. Events → " .. tostring(EVENT_PATH)
    .. " crashDv=" .. tostring(CRASH_DV_THRESHOLD)
    .. " air=" .. tostring(AIRTIME_THRESHOLD)
    .. " roll=" .. tostring(ROLLOVER_THRESHOLD)
    .. (beammpEnabled and " (BeamMP hints on)" or " (solo)")
    .. (udpReady and "" or " [UDP offline]"))
end

function M.onExtensionUnloaded()
  if sessionActive then
    appendEvent("sessionEnd", {})
    sessionActive = false
  end
  if udpSock then
    pcall(function() udpSock:close() end)
    udpSock = nil
    udpReady = false
  end
end

function M.onWorldReadyState(state)
  if state == 2 or state == true or state == "ready" then
    if not sessionActive then
      sessionActive = true
      appendEvent("sessionStart", { ["map.id"] = 1 })
      appendEvent("playerJoin", {
        ["player.alive"] = 1,
        ["beammp"] = beammpEnabled and 1 or 0,
      })
    end
  end
end

function M.onClientStartMission()
  if not sessionActive then
    sessionActive = true
    appendEvent("sessionStart", { ["map.id"] = 1 })
    appendEvent("playerJoin", { ["player.alive"] = 1 })
  end
end

function M.onClientEndMission()
  if sessionActive then
    appendEvent("playerLeave", { ["player.alive"] = 0 })
    appendEvent("sessionEnd", {})
    sessionActive = false
  end
end

function M.onPlayerConnected(playerId, name)
  if not name or name == "" then
    return
  end
  beammpPlayers[string.lower(tostring(name))] = playerId
  local prev = playerName
  playerName = tostring(name)
  appendEvent("playerJoin", { ["player.alive"] = 1, ["beammp"] = 1 })
  playerName = prev
end

function M.onPlayerDisconnected(playerId, name)
  if name and name ~= "" then
    beammpPlayers[string.lower(tostring(name))] = nil
  end
  local prev = playerName
  if name and name ~= "" then
    playerName = tostring(name)
  end
  appendEvent("playerLeave", { ["player.alive"] = 0, ["beammp"] = 1 })
  playerName = prev
end

function M.onUpdate(dt)
  pollCommands()
  if not sessionActive then
    return
  end
  dt = dt or 0.016
  local x, y, z, speed, inverted, airborne = getVehiclePose()
  if not speed then
    return
  end

  local dv = lastSpeed - speed
  if dv > CRASH_DV_THRESHOLD and lastSpeed > 5 then
    appendEvent("crash", {
      ["crash.severity"] = dv,
      ["vehicle.speed"] = speed,
      ["vehicle.damage"] = math.min(100, dv * 4),
      ["player.alive"] = 1,
    })
  end
  lastSpeed = speed

  if airborne then
    airtimeAcc = airtimeAcc + dt
    if airtimeAcc >= AIRTIME_THRESHOLD then
      appendEvent("airtime", { ["airtime.seconds"] = airtimeAcc, ["vehicle.speed"] = speed })
      airtimeAcc = 0
    end
  else
    airtimeAcc = 0
  end

  if inverted then
    rolloverAcc = rolloverAcc + dt
    if rolloverAcc >= ROLLOVER_THRESHOLD then
      appendEvent("rollover", { ["rollover.seconds"] = rolloverAcc, ["vehicle.speed"] = speed })
      rolloverAcc = 0
    end
  else
    rolloverAcc = 0
  end

  local outside = x < BOUNDARY.minX or x > BOUNDARY.maxX
      or y < BOUNDARY.minY or y > BOUNDARY.maxY
      or z < BOUNDARY.minZ or z > BOUNDARY.maxZ
  if outside and not outsideBoundary then
    outsideBoundary = true
    appendEvent("leaveBoundary", { ["boundary.id"] = 1, ["player.x"] = x, ["player.y"] = y, ["player.z"] = z })
  elseif not outside then
    outsideBoundary = false
  end

  lastMoveT = lastMoveT + dt
  if lastMoveT >= MOVE_INTERVAL then
    lastMoveT = 0
    appendEvent("move", {
      ["player.x"] = x,
      ["player.y"] = y,
      ["player.z"] = z,
      ["vehicle.speed"] = speed,
      ["player.alive"] = 1,
    })
  end
end

function M.onSCRecover()
  appendEvent("recover", { ["player.alive"] = 1 })
  appendEvent("respawn", { ["player.alive"] = 1, ["player.health"] = 0 })
end

M.dependencies = {}

return M
