-- Optional vehicle-side helper for NL_BeamNGBridge.
-- Relays recover/reset moments to the GE extension as recover/respawn NDJSON events.

local M = {}

local function notifyGe(fn)
  if not obj or not obj.queueGameEngineLua then
    return
  end
  obj:queueGameEngineLua(string.format("if extensions and extensions.NL_bridge then extensions.NL_bridge.%s() end", fn))
end

function M.onExtensionLoaded()
  -- no-op
end

function M.onRecover()
  notifyGe("onSCRecover")
end

-- Some BeamNG builds expose reset differently; bind common names.
function M.onVehicleReset()
  notifyGe("onSCRecover")
end

return M
