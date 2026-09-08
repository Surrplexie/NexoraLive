// NL Game Integration Spec v1 — Unreal C++ stub (header-only sketch).

#pragma once

#include "CoreMinimal.h"

/** Connect a mod/module to NL.Server over WebSocket (Spec v1). */
class NEXORALIVE_API FNlBridge
{
public:
    void Connect(const FString& Url = TEXT("ws://127.0.0.1:27021/nl/v1"));
    void EmitEvent(const FString& EventName, const FString& Player, const TMap<FString, double>& Props);
    void PollActions(); // read inbound action NDJSON lines

private:
    TSharedPtr<class IWebSocket> Socket;
    void HandleActionLine(const FString& Line);
};

// Implementation notes:
// - Use IWebSocket from WebSockets module (or third-party) for ws:// transport.
// - Emit one NDJSON line per event with "nl":1 and Unix-ms "ts".
// - On action JSON, map "action" field: warn → on-screen message, kick → disconnect, recover → reset pawn.
