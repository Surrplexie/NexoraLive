-- Loaded when NL_BeamNGBridge is enabled in the Mod Manager.
-- Without this file BeamNG lists the mod but never runs lua/ge/extensions/NL/bridge.lua.
load("NL_bridge")
setExtensionUnloadMode("NL_bridge", "manual")
