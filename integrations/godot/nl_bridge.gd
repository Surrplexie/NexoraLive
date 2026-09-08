extends Node
## NL Game Integration Spec v1 — Godot 4 reference bridge (WebSocket).
##
## Attach to an autoload or scene root. Set `bridge_url` from the Session Host dashboard
## (includes ?token= when using the hosted session bus).
##
##   bridge_url = "ws://127.0.0.1:27021/nl/v1?token=YOUR_TOKEN"

@export var bridge_url: String = "ws://127.0.0.1:27021/nl/v1"
@export var auto_reconnect_sec: float = 2.0
@export var run_sample_on_connect: bool = false

var _socket: WebSocketPeer = WebSocketPeer.new()
var _connected := false

func _ready() -> void:
	_connect()

func _process(_delta: float) -> void:
	_socket.poll()
	var state := _socket.get_ready_state()
	if state == WebSocketPeer.STATE_OPEN and not _connected:
		_connected = true
		print("[nl bridge] connected ", bridge_url)
		if run_sample_on_connect:
			_run_sample()
	elif state == WebSocketPeer.STATE_CLOSED and _connected:
		_connected = false
		print("[nl bridge] disconnected; reconnecting…")
		await get_tree().create_timer(auto_reconnect_sec).timeout
		_connect()
	while _socket.get_available_packet_count() > 0:
		var raw := _socket.get_packet().get_string_from_utf8()
		for line in raw.split("\n", false):
			var trimmed := line.strip_edges()
			if trimmed:
				print("[nl action] ", trimmed)

func emit_event(event: String, player: String, props: Dictionary = {}) -> void:
	if _socket.get_ready_state() != WebSocketPeer.STATE_OPEN:
		return
	var payload := {
		"nl": 1,
		"event": event,
		"player": player,
		"ts": Time.get_unix_time_from_system() * 1000,
	}
	if not props.is_empty():
		payload["props"] = props
	_socket.send_text(JSON.stringify(payload))

func _connect() -> void:
	var err := _socket.connect_to_url(bridge_url)
	if err != OK:
		push_warning("[nl bridge] connect failed: %s" % err)

func _run_sample() -> void:
	emit_event("sessionStart", "Alice", {"map.id": 1})
	emit_event("playerJoin", "Alice", {"player.alive": 1})
	emit_event("shoot", "Alice", {"weapon.damage": 12})
	emit_event("shoot", "Bob", {"weapon.damage": 50})
