# Host Migration for Go Fish (Online Multiplayer)

## Goal
When the **host leaves** an online match, the match must NOT end. Instead:
1. A remaining human is promoted to **new host**.
2. The **departed host becomes an AI** (player count stays the same — no array resizing).
3. All remaining humans **reconnect** and the **exact game state is restored**.
4. Play continues seamlessly.

**Offline mode is completely untouched** — all changes are gated behind `GameModeManager.isOnlineMode`.

---

## Critical Reality (NGO + Relay limitation — MUST understand first)
Unity Netcode for GameObjects + Relay has **NO automatic host migration**. The host **is** the server and owns the Relay allocation. When the host leaves:
- The Relay allocation is destroyed → **every client disconnects**.
- There is no way to "promote player 2 to host on the same connection."

Therefore the *only* correct professional approach is **reconnection-based migration**:
```
Host leaves
  → everyone disconnects
  → deterministic new host elected
  → new host creates a NEW Relay allocation + updates Lobby
  → others reconnect via the new relay code
  → new host restores the saved snapshot
  → departed host's slot rebuilt as a server-controlled AI
  → game resumes
```
This is exactly the "Phase 1" your manager described (pause → choose host → reconnect → restore → host becomes AI).

---

## Current-State Findings (gaps that must be filled)
1. **No server-side AI.** `GameManager.AISelectRandomTarget()` early-returns in online mode (`NetworkGameManager.cs:532`). Online is currently all-human. A converted-to-AI host has **nothing** to drive its turns → **must build a server-authoritative AI controller**.
2. **Authoritative state lives only in the host's memory** — split across:
   - `NetworkDeckManager.deck` (List<int>)
   - `NetworkPlayer.hand / score / completedBooks`
   - `NetworkGameManager`: `serverAskedThisTurn`, `serverCompletedRanks`, `pendingDraw*`, `currentTurnPlayerId`, `requestedRank`, `targetPlayerId`
   None of it is serialized → **must build a serializable snapshot**.
3. **No disconnect handling.** No `OnClientDisconnectCallback` anywhere (grep confirmed). `NetworkPlayerManager.UnregisterPlayer` just removes the player and the game silently breaks.
4. **No snapshot transport.** Nothing writes game state to Lobby Data.
5. **Clients only hold partial truth** (own real hand + others' card counts). The full truth (all hands + deck) exists **only on the host** → the snapshot MUST originate from the host before it leaves.

---

## Architecture (MVC-aligned, no hardcode)

New/changed files (all `Assets/Scripts/`):

| File | Role | Layer |
|------|------|-------|
| `core/GameStateSnapshot.cs` | Pure data DTO (serializable). No Unity deps. | **Model** |
| `core/SnapshotSerializer.cs` | DTO ⇄ JSON. Pure C#. | **Model/Service** |
| `NetworkSnapshotSync.cs` | Host builds snapshot, writes to Lobby Data; clients read it. | **Network Service** |
| `NetworkAIController.cs` | Server-side AI driver for AI/AI-converted players. | **Controller** |
| `HostMigrationController.cs` | Detects host leave, elects new host, drives relay recreation + reconnect + restore. | **Controller** |
| `NetworkPlayerManager.cs` | (edit) deterministic ordering, AI-flag, departed-host retention. | **Model/Service** |
| `NetworkPlayer.cs` | (edit) add `NetworkVariable<bool> IsAI`. | **Model** |
| `NetworkGameManager.cs` | (edit) snapshot build/restore hooks, AI turn trigger, migration-safe turn advance. | **Controller** |
| `LobbyManager.cs` | (edit) snapshot read/write helpers, new-relay recreation for migration. | **Service** |
| `MigrationOverlayUI.cs` | "Migrating host…" panel. | **View** |

Offline path (`SetupGame`, local `GameManager` AI) is never modified — every new branch checks `isOnlineMode`.

---

## Implementation Phases

### Phase 0 — Foundations (Model layer, zero gameplay change)
1. **`core/GameStateSnapshot.cs`** — DTO capturing everything needed to rebuild a match:
   - `PlayerSnapshot[] players` → `{ clientId, name, avatarIndex, score, int[] hand, int[] completedBooks, bool isAI }`
   - `int[] deck`, `int[] completedRanks`
   - `ulong currentTurnClientId`, `int requestedRank`, `ulong targetPlayerId`
   - `bool hasPendingDraw`, `ulong pendingDrawPlayerId`, `int pendingDrawAskedRank`, `ulong pendingDrawTargetId`
   - `ulong departedHostClientId` (the player to convert to AI)
   - `int snapshotVersion`
2. **`core/SnapshotSerializer.cs`** — `ToJson()` / `FromJson()` using `JsonUtility`. Chunking helper because Lobby Data values have size limits (split large JSON across `state_0`, `state_1`, … keys).
3. **`NetworkPlayer.cs`** — add `NetworkVariable<bool> IsAI` so the departed-host slot is marked AI on all clients (drives UI "AI" badge + back-facing hand).

### Phase 1 — Server-side AI (required prerequisite)
A converted-to-AI host needs *something* to play its turns. Build `NetworkAIController.cs`:
- Watches `NetworkGameManager.currentTurnPlayerId`.
- When it is an **AI** player's turn (server-side), reuse the existing pure logic in `core/AIStrategy.cs` + `core/AIMemory.cs` (already MVC-clean, no Unity coupling) to choose `(rank, target)`, then invoke the **same** server ask path (`RequestCardRpc` body) and auto-draw path (`RequestDrawFromDeckRpc` body).
- Refactor the shared ask/draw core out of the two RPC methods into plain server methods (`ServerAsk(...)`, `ServerDraw(...)`) so both humans (via RPC) and AI (via direct call) use identical authoritative logic. No duplication.
- This also unlocks **mixed AI+human online games** for free (future feature), but stays optional for now.

### Phase 2 — Snapshot build & transport (host → lobby → clients)
4. **`NetworkSnapshotSync.cs`** (NetworkBehaviour, server-owned):
   - `BuildSnapshot()` reads from `NetworkDeckManager`, `NetworkPlayerManager`, `NetworkGameManager`.
   - Writes JSON (chunked) to **Lobby Data** key `gameState` on host, on:
     - every turn result,
     - every book formed,
     - every deck draw,
     - and a periodic timer (every ~3s) as a safety net.
   - Each client also caches the latest snapshot locally (for resilience if Lobby read fails).
5. Hook snapshot writes into `NetworkGameManager` after each `TurnResultClientRpc` (already the natural sync point) and after `CheckForBook`/`RefillHandIfEmpty`.

### Phase 3 — Disconnect detection & new-host election
6. **`HostMigrationController.cs`** registers `NetworkManager.OnClientDisconnectCallback` on every client.
   - On detecting the **host** clientId disconnect:
     - Block the normal `HandleLobbyClosed`/quit flow (do NOT return to menu).
     - Show `MigrationOverlayUI` ("Migrating host…").
7. **Deterministic election** (no coordination needed, every client computes the same answer):
   - New host = lowest remaining clientId (matches existing `players.Sort` by clientId in `NetworkPlayerManager`). 
   - Each client checks `NetworkManager.LocalClientId` == elected → it becomes host; else → client role.

### Phase 4 — Relay recreation & reconnect
8. **New host path** (in `HostMigrationController`, reusing `LobbyManager` primitives):
   - Read latest snapshot from Lobby Data (or local cache).
   - `RelayService.CreateAllocationAsync` → `GetJoinCodeAsync`.
   - `LobbyService.UpdateLobbyAsync` → overwrite `relayCode` with the new code.
   - `NetworkManager.StartHost()` on the new allocation.
   - Call `RestoreFromSnapshot()` (Phase 5).
9. **Remaining client path**:
   - Poll `LobbyService.GetLobbyAsync` until `relayCode` changes (timeout ~15s).
   - `JoinAllocationAsync(newCode)` → `SetRelayServerData` → `NetworkManager.StartClient()`.
   - Server pushes restored state via the existing `SendStateToSpecificClient` flow (already exists in `NetworkGameManager`).

### Phase 5 — State restoration (the core of "game continues")
10. **`NetworkGameManager.RestoreFromSnapshot(GameStateSnapshot snap)`** (server):
    - Rebuild `NetworkDeckManager.deck` from `snap.deck`.
    - For each `PlayerSnapshot`:
      - If the player is still connected → repopulate its `NetworkPlayer.hand/score/completedBooks`.
      - If it is `snap.departedHostClientId` → **spawn a server-owned dummy `NetworkPlayer`** (NetworkObject with server ownership), set `IsAI = true`, restore its hand/score/books, register in `NetworkPlayerManager`. This is the "host becomes AI" step — player count is unchanged, no array resize.
    - Restore `currentTurnPlayerId`, `requestedRank`, `targetPlayerId`, `serverAskedThisTurn`, `serverCompletedRanks`, `pendingDraw*`.
    - `deckRemainingCards.Value` updated; broadcast state to reconnecting clients.
    - If restored current turn belongs to the new AI → `NetworkAIController` takes over automatically.
11. **Client UI** rebuilds seats via the existing `InitializeMultiplayer`/`ApplyPublicState`/`ApplyPrivateHand` path — the departed host's seat now shows an **AI avatar/name** and face-down hand (its `IsAI` flag handles UI).

### Phase 6 — Edge cases & safety
- **2-player game**: host leaves → remaining human becomes host, departed host = AI. Game becomes 1 human + 1 AI — valid, continues. (If product wants "end game when only 1 human", that's a 1-line policy gate, not a structural change.)
- **Snapshot missing/stale**: fall back to graceful "match could not be restored → return to lobby/menu" (reuse `HandleLobbyClosed`). Never hardcode state.
- **During a Go-Fish pending draw**: snapshot includes `pendingDraw*` so the restored AI/human finishes the draw correctly.
- **Reconnect race**: new host waits until expected client count reached or timeout before unpausing.
- **Multiple players leave simultaneously**: election still deterministic; if only the host leaves the snapshot is valid; if a non-host human also left, that slot likewise converts to AI.

---

## What stays identical (no regression)
- Offline single-device play: `GameModeManager.isOnlineMode == false` branches are never touched.
- Lobby creation/join, matchmaking, profiles, PlayFab — unchanged.
- Existing ask/draw/book flow for **human online players** — only refactored (shared core extracted), behavior preserved.
- MVC purity: all game logic stays in `core/` (pure C#); Unity/Networkcode stays in `Network*` controllers; UI stays in `*UI` views.

---

## Build Order (smallest-risk-first)
1. Phase 0 (DTO + serializer + `IsAI` flag) — compiles, no behavior change.
2. Phase 1 (server AI controller + extract `ServerAsk/ServerDraw`) — testable standalone: spawn AI players in an online match and verify they play.
3. Phase 2 (snapshot build + Lobby write) — verify host writes valid JSON to Lobby Data.
4. Phase 3 + 4 (detect + elect + relay recreation + reconnect) — verify clients reconnect to new host on host quit.
5. Phase 5 (restore snapshot + spawn AI for departed host) — verify exact scores/hands/deck/turn survive a host quit.
6. Phase 6 (edge cases, timeouts, overlay polish).

## Out of scope (explicitly)
- Mid-match *spectating* / late-join of brand-new players.
- Persisting a match across app restarts (only in-session migration).
- Changing the offline AI difficulty or `AIStrategy` heuristics.
