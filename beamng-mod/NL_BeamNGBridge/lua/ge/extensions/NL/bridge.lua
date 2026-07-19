-- NL BeamNG Bridge (game-engine extension)
-- Emits sparse NDJSON semantic events for NexoraLive (NL) and applies Block actions
-- from NL over localhost UDP. See docs/BEAMNG.md in the NL repo.
--
-- Install: copy/symlink NL_BeamNGBridge into <BeamNG user folder>/mods/unpacked/
-- then enable the mod and load a map. Events append to:
--   %LOCALAPPDATA%/NL/beamng-events.ndjson  (Windows)
-- Commands arrive on UDP 127.0.0.1:27022 with magic header "SCBN1".

local M = {}

local EVENT_PATH = nil
local CMD_PORT = 27022
local MAGIC = "SCBN1"

local MOVE_INTERVAL = 0.35          -- seconds between move events
local CRASH_DV_THRESHOLD = 8.0      -- m/s delta-v style severity proxy
local AIRTIME_THRESHOLD = 1.25      -- seconds wheels-ish airtime
local ROLLOVER_THRESHOLD = 1.5      -- seconds inverted
local BOUNDARY = {                  -- streamer-tunable axis-aligned box (world units)
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

local udpSock = nil

local function ensureDir(path)
  -- Best-effort; BeamNG/Windows usually already have LOCALAPPDATA\NL from Session Host.
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

local function resolveEventPath()
  local localApp = os.getenv("LOCALAPPDATA") or os.getenv("HOME") or "."
  local sep = package.config:sub(1, 1)
  local dir = localApp .. sep .. "NL"
  ensureDir(dir)
  return dir .. sep .. "beamng-events.ndjson"
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
  -- Recover active player vehicle when possible (API names vary by BeamNG version).
  local ok, err = pcall(function()
    if be and be.getPlayerVehicle then
      local veh = be:getPlayerVehicle(0)
      if veh and veh.queueLuaCommand then
        veh:queueLuaCommand("recovery.startRecovering()")
        return
      end
    end
    if core_vehicle_manager and core_vehicle_manager.enterVehicle then
      -- fallback no-op marker
    end
  end)
  if not ok then
    log("W", "NL", "recover failed: " .. tostring(err))
  end
end

local function despawnOrKick(kind, who, reason)
  toast(string.format("%s %s: %s", kind, who or "?", reason or ""))
  -- Solo: recover. BeamMP host tools can replace this with MP-specific kick later.
  if kind == "despawn" or kind == "kick" or kind == "recover" then
    recoverLocal()
  end
end

local function handleCommand(raw)
  -- SCBN1\naction|player|message
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
    return
  end
  local s = socket.udp()
  s:setsockname("127.0.0.1", CMD_PORT)
  s:settimeout(0)
  udpSock = s
  log("I", "NL", "Listening for NL commands on UDP 127.0.0.1:" .. tostring(CMD_PORT))
end

local function pollCommands()
  if not udpSock then
    return
  end
  while true do
    local data = udpSock:receive()
    if not data then
      break
    end
    handleCommand(data)
  end
end

local function getVehiclePose()
  -- Returns x,y,z,speed,inverted,airborne or nils
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
    -- Rough inverted / airborne proxies — tune during dogfood.
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
  -- Soft detect: presence of common BeamMP globals/hooks.
  beammpEnabled = (MPVehicleGE ~= nil) or (MPGameNetwork ~= nil) or (type(MP) == "table")
end

function M.onExtensionLoaded()
  EVENT_PATH = resolveEventPath()
  playerName = (getPlayerServerName and getPlayerServerName()) or (Steam and Steam.playerName) or "Driver"
  ensureUdp()
  detectBeamMP()
  log("I", "NL", "NL_BeamNGBridge loaded. Events → " .. tostring(EVENT_PATH) .. (beammpEnabled and " (BeamMP hints on)" or " (solo)"))
end

function M.onExtensionUnloaded()
  if sessionActive then
    appendEvent("sessionEnd", {})
    sessionActive = false
  end
  if udpSock then
    pcall(function() udpSock:close() end)
    udpSock = nil
  end
end

function M.onWorldReadyState(state)
  -- Fired when a level is ready in many BeamNG builds.
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

-- BeamMP-oriented hooks (called only if the MP stack invokes matching extension hooks).
function M.onPlayerConnected(playerId, name)
  if not name or name == "" then
    return
  end
  local prev = playerName
  playerName = tostring(name)
  appendEvent("playerJoin", { ["player.alive"] = 1, ["beammp"] = 1 })
  playerName = prev
end

function M.onPlayerDisconnected(playerId, name)
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

  -- Crash proxy: sudden speed loss.
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

-- Manual recover detection helper (call from vehicle side via queueGameEngineLua if desired)
function M.onSCRecover()
  appendEvent("recover", { ["player.alive"] = 1 })
  appendEvent("respawn", { ["player.alive"] = 1, ["player.health"] = 0 })
end

M.dependencies = {}

return M
