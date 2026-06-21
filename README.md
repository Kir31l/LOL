# START LANDS — Online Multiplayer Exam Prototype

## Architecture

**Network Solution**: Photon Fusion 2.0.12

**Architecture Type**: Shared-Authoritative (Host Mode)

- The host player runs as both server and client via `NetworkRunner.StartGame()` with `GameMode.Host`
- Clients connect by entering the host's 4-digit Fusion session name (displayed in the HUD)
- Fusion handles state synchronization, RPCs, and room management — no Unity Relay / Lobby needed
- Physics driven by `Fusion.Addons.Physics` with `RunnerSimulatePhysics2D` and `ClientPhysicsSimulation.SimulateForward`
- Input System 1.18.0 (activeInputHandler=2, no legacy input)

## How Synchronization Works

| Data Type | Mechanism | Why |
|-----------|-----------|-----|
| **Position/Velocity** | `NetworkTransform` + `Rigidbody2D` (via Fusion) | Server-authoritative physics sync; interpolation enabled |
| **Score, Lives** | `[Networked]` properties on `NetworkPlayer` | Automatic state sync with Fusion's networked properties |
| **Username** | `[Networked]` `NetworkString<_16>` | Fixed-string sync per player, assigned on spawn |
| **Character Index** | `[Networked]` property | Synced to apply correct sprite/animator per client |
| **AnimState** | `[Networked]` `int` | Server-sets in `FixedUpdateNetwork`, clients apply in `Render()` |
| **Apple State** | `NetworkObject` state + `ServerRpc` | Server-authoritative pickup/respawn; photon visibility |
| **Damage / Knockback** | `ServerRpc` → server authorizes → `Rpc` (state sync) | One-shot events with server validation |
| **Invulnerability** | `[Networked]` property on `NetworkPlayer` | Server controls invulnerability window |
| **Lives** | `[Networked]` property with `OnChanged` | Server-authoritative lives decrement and respawn |

## Project Setup

### Requirements
- Unity 6000.3.10f1
- Photon Fusion 2.0.12 SDK (App ID configured in Fusion settings)

### Setup Steps
1. Open the project in Unity
2. Open `Assets/Scenes/menu.unity`
3. Configure your Photon Fusion App ID in `Project Settings > Fusion > App Settings`
4. Build the project (`File > Build Settings`)
   - Ensure `menu.unity` and `lobby.unity` are in the build order

### How to Test
1. **Host**: Play in editor → click **CREATE**
   - A 4-digit room code appears in the HUD
2. **Client**: Run a second instance (Standalone build) → enter the code → click **JOIN**
3. Both players appear with their selected character and username

> **Note**: Client physics requires `RunnerSimulatePhysics2D` and `ClientPhysicsSimulation.SimulateForward`. Fusion 2 disables Unity auto-physics (`Physics2D.simulationMode = Script`); clients will not move or collide without this setup.

## Gameplay Features
- **4 playable characters**: Virtual Guy, Pink Man, Ninja Frog, Mask Dude
- **Wall sliding/jumping**: Press toward wall in air to slide, jump to wall-jump away
- **Apple pickups**: +50 score, 5-second respawn
- **Saw hazards**: Moving saws deal damage with knockback
- **3 lives**: Invulnerability after hit, 5-second respawn on death
- **Scoreboard**: Tab or click to toggle ranked player list (score descending)
- **Custom BitmapText**: World-space sprite-based text (no Canvas)

## Scripts Overview

| Script | Purpose |
|--------|---------|
| `ConnectionManager` | Fusion connection flow (create/join/disconnect), `INetworkRunnerCallbacks`, buffered input, `RunnerSimulatePhysics2D` setup |
| `NetworkPlayer` | `NetworkBehaviour` — syncs character, score, lives, username, anim state |
| `PlayerMove` | Local input consumption, physics, wall slide/jump, death flag, animation sync |
| `NetworkInputData` | `INetworkInput` struct for buffered movement/jump inputs |
| `LobbyPlayerSetup` | Applies character sprite + animator controller on spawn |
| `NameTag` | Username display above player via `[Networked] UsernameNV` |
| `PlayerSession` | Static local username storage |
| `ApplePickup` | Server-authoritative pickup + respawn |
| `SawDamage` / `SawMovement` | Server-authoritative hazard logic |
| `ScoreManager` / `LivesManager` | Static state with change events |
| `Hud` / `LivesDisplay` / `ScoreboardDisplay` / `ScoreboardToggle` | In-game UI |
| `BitmapText` / `BitmapInputField` | Custom world-space text system |
| `CharacterSelectable` | Menu character selection |
| `CreateGameOnClick` / `JoinGameOnClick` / `LoadSceneOnClick` | Menu button bindings |
| `BackgroundScroller` | Menu background parallax |

## Known Issues & Fixes

1. **Client physics**: Fusion 2 disables Unity auto-physics. Fixed by using `Fusion.Addons.Physics` with `RunnerSimulatePhysics2D` and `ClientPhysicsSimulation.SimulateForward` — without this, client-side physics never runs.
2. **Input System**: Project uses New Input System only (`activeInputHandler: 2`). Legacy `Input.GetAxisRaw`/`GetButton` always return 0. Fixed: uses `Keyboard.current`/`Gamepad.current` with buffered input in `Update()` served to `OnInput()`.
3. **FastKeyboard backend**: Individual key `isPressed` returns false despite events arriving. Fixed: uses `ReadValue() > 0.1f` threshold with raw event state tracking via `ReadUnprocessedValueFromEvent`.
4. **SawMovement transform**: Setting `transform.position` directly bypasses `Rigidbody2D` and may not trigger `NetworkTransform` detection. Fixed: uses `rb.MovePosition()`.
5. **Saw NetworkObject**: Missing `NetworkTransform` in `NetworkedBehaviours` list prevented position sync. Fixed: added fileID to the list.
6. **Animation sync**: `GetInput` returns false on proxy players so `UpdateAnimation` was never called. Fixed: `[Networked] AnimState` set on server in `FixedUpdateNetwork`, applied in `Render()` for non-local players.
7. **NameTag**: Used static `PlayerSession.Username` for all players, showing local username on everyone. Fixed: removed from `Start()`, name set only via `[Networked] UsernameNV`.
8. **Death deadlock**: Self-disable via `enabled = false` prevents `FixedUpdateNetwork` from running, so the player can never re-enable. Fixed: `bool isDead` flag instead; server always initializes `CurrentLives = 3` for remote players in `Spawned()`.
9. **Scoreboard**: Removed unused `[Networked] Deaths` property. Adjusted panel/scoreboard positions for on-screen visibility.

## Chat System

The project includes imported chat UI art assets (`Assets/Free/Menu/chat/gui/`) with sprites (`chatbox.png`, `chatbar.png`) and a JSON layout config, but no C# scripts — no chat system is implemented yet.
