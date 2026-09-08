-- NL_Kick — BeamMP server plugin
-- Reads kick requests written by NL_BeamNGBridge (GE) and calls MP.DropPlayer.
--
-- Install on the BeamMP dedicated/listen server:
--   copy this folder to <BeamMP Server>/Resources/Server/NL_Kick/
--
-- Queue file (same PC as NL Session Host / bridge by default):
--   %LOCALAPPDATA%\NL\beamng-kicks.ndjson
-- Override with env NL_BEAMNG_KICKS or edit QUEUE_PATH below.

local QUEUE_PATH = os.getenv("NL_BEAMNG_KICKS")
if not QUEUE_PATH or QUEUE_PATH == "" then
  local localApp = os.getenv("LOCALAPPDATA") or os.getenv("HOME") or "."
  local sep = package.config:sub(1, 1)
  QUEUE_PATH = localApp .. sep .. "NL" .. sep .. "beamng-kicks.ndjson"
end

local lastSize = 0
local pollAcc = 0
local POLL_INTERVAL = 0.5

local function trim(s)
  return (tostring(s or ""):gsub("^%s+", ""):gsub("%s+$", ""))
end

local function parseLine(line)
  line = trim(line)
  if line == "" or line:sub(1, 1) == "#" then
    return nil
  end
  local player = line:match('"player"%s*:%s*"(.-)"')
  local reason = line:match('"reason"%s*:%s*"(.-)"')
  if not player then
    return nil
  end
  return player, reason or "NLEvents Block"
end

local function findPlayerIdByName(name)
  if not name or name == "" then
    return nil
  end
  local want = string.lower(name)
  if not MP or not MP.GetPlayers then
    return nil
  end
  local players = MP.GetPlayers()
  if type(players) ~= "table" then
    return nil
  end
  for id, pname in pairs(players) do
    local n = pname
    if type(pname) == "table" then
      n = pname.name or pname.Name
    end
    if type(n) == "string" and string.lower(n) == want then
      return id
    end
  end
  -- Fallback: MP.GetPlayerName if available
  if MP.GetPlayerCount and MP.GetPlayerName then
    local count = MP.GetPlayerCount() or 0
    for id = 0, count + 32 do
      local ok, n = pcall(function() return MP.GetPlayerName(id) end)
      if ok and type(n) == "string" and string.lower(n) == want then
        return id
      end
    end
  end
  return nil
end

local function processNewLines()
  local f = io.open(QUEUE_PATH, "r")
  if not f then
    return
  end
  local content = f:read("*a") or ""
  f:close()
  local size = #content
  if size < lastSize then
    -- File truncated / rotated
    lastSize = 0
  end
  if size == lastSize then
    return
  end
  local chunk = content:sub(lastSize + 1)
  lastSize = size
  for line in chunk:gmatch("[^\r\n]+") do
    local player, reason = parseLine(line)
    if player then
      local id = findPlayerIdByName(player)
      if id ~= nil and MP and MP.DropPlayer then
        local ok, err = pcall(function()
          MP.DropPlayer(id, reason)
        end)
        if ok then
          print("[NL_Kick] Dropped " .. player .. " (" .. tostring(id) .. "): " .. tostring(reason))
        else
          print("[NL_Kick] DropPlayer failed: " .. tostring(err))
        end
      else
        print("[NL_Kick] No online player named '" .. player .. "' (queued kick ignored)")
      end
    end
  end
end

function onInit()
  print("[NL_Kick] Watching " .. QUEUE_PATH)
  local f = io.open(QUEUE_PATH, "r")
  if f then
    local content = f:read("*a") or ""
    f:close()
    lastSize = #content
  end
end

function onTick(elapsed)
  pollAcc = pollAcc + (elapsed or 0)
  if pollAcc < POLL_INTERVAL then
    return
  end
  pollAcc = 0
  processNewLines()
end
