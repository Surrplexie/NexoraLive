-- NL Game Integration Spec v1 — minimal Lua reference bridge.
-- Adapt socket calls to your engine (luasocket, BeamNG extensions, etc.).
--
-- Events: connect TCP to NL --source tcp://127.0.0.1:27021, send NDJSON lines.
-- Actions: listen on 27023 or read from paired WebSocket (see python reference).

local NL_VERSION = 1
local EVENT_HOST = "127.0.0.1"
local EVENT_PORT = 27021

local function jsonEscape(s)
  s = tostring(s or "")
  return s:gsub("\\", "\\\\"):gsub('"', '\\"'):gsub("\n", "\\n")
end

local function emitEvent(eventName, player, props)
  local parts = {
    string.format('"nl":%d', NL_VERSION),
    string.format('"event":"%s"', jsonEscape(eventName)),
    string.format('"player":"%s"', jsonEscape(player or "")),
    string.format('"ts":%d', math.floor(os.time() * 1000)),
  }
  if props and next(props) then
    local propParts = {}
    for k, v in pairs(props) do
      table.insert(propParts, string.format('"%s":%.6g', k, v))
    end
    table.insert(parts, '"props":{' .. table.concat(propParts, ",") .. "}")
  end
  local line = "{" .. table.concat(parts, ",") .. "}\n"
  -- TODO: replace with your engine's TCP send (e.g. socket:send(line))
  print("[nl bridge emit] " .. line)
  return line
end

local function handleActionLine(line)
  -- Parse {"nl":1,"type":"action","action":"warn",...}
  print("[nl bridge action] " .. tostring(line))
  -- TODO: map action verbs to in-game effects (toast, kick, recover, …)
end

-- Example bootstrap
emitEvent("sessionStart", "Driver", { ["map.id"] = 1 })
emitEvent("playerJoin", "Driver", { ["player.alive"] = 1 })
emitEvent("move", "Driver", { ["vehicle.speed"] = 42, ["player.x"] = 10 })

return {
  emitEvent = emitEvent,
  handleActionLine = handleActionLine,
}
