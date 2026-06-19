# START LANDS — Online Multiplayer Exam Prototype

## Architecture

**Network Solution**: Netcode for GameObjects (NGO) 2.11.2 + Unity Relay + Unity Lobby

**Architecture Type**: Host-Authoritative Client-Server

- The host player acts as both server and client (`NetworkManager.StartHost()`)
- Unity Relay provides secure cloud relay (IP obfuscation, no port forwarding)
- Unity Lobby generates 4-digit numeric join codes for room discovery
- Clients join anonymously via Unity Authentication, query the lobby by code, read the Relay join code, and connect

## How Synchronization Works

| Data Type | Mechanism | Why |
|-----------|-----------|-----|
| **Position/Velocity** | `NetworkTransform` + `NetworkRigidbody2D` | Server-authoritative physics sync; interpolation enabled for smooth movement |
| **Score, Lives, Deaths** | `NetworkVariable<int>` | Simple value sync with `OnValueChanged` callbacks for HUD updates |
| **Username** | `NetworkVariable<FixedString32Bytes>` | Efficient fixed-string sync — set by server once on connect |
| **Character Index** | `NetworkVariable<int>` | Synced to apply correct sprite/animator per client |
| **Apple state** | `NetworkVariable<bool>` | Server-authoritative pickup/respawn; visual sync via callback |
| **Invulnerability** | `NetworkVariable<bool>` | Server controls invulnerability window |
| **Knockback** | `ClientRpc` | One-shot event sent from server to the hit client |
| **Score/State Updates** | `ClientRpc` | For targeted event sync (respawn, knockback, player visibility) |
| **Animation Parameters** | `NetworkAnimator` | Syncs Animator "state" integer parameter across all clients |

## Project Setup

### Requirements
- Unity 6000.3.10f1
- Unity Cloud project with **Authentication**, **Relay**, and **Lobby** enabled

### Setup Steps
1. Open the project in Unity
2. Open `Assets/Scenes/menu.unity`
3. In the **Unity Cloud** dashboard, link the project and enable:
   - Unity Authentication (anonymous)
   - Unity Relay
   - Unity Lobby
4. Add your Project ID to `Project Settings > Services`
5. Build the project (`File > Build Settings`)
   - Ensure `menu.unity` and `lobby.unity` are in the build order
   - Build output: `STARTLANDSOnlineExam.exe`

### How to Test
1. **Host**: Run the build or play in editor → click **CREATE**
   - A 4-digit lobby code appears in the HUD
2. **Client**: Run a second instance → enter the code → click **JOIN**
3. Both players appear with their selected character and username

## Gameplay Features
- **4 playable characters**: Virtual Guy, Pink Man, Ninja Frog, Mask Dude
- **Wall sliding/jumping**: Press toward wall in air to slide, jump to wall-jump away
- **Apple pickups**: 50 points, 5-second respawn
- **Saw hazards**: Moving saws deal damage with knockback
- **3 lives**: Invulnerability after hit, 5-second respawn on death
- **Scoreboard**: Tab/click to toggle ranked player list (score descending)
- **Custom BitmapText**: World-space sprite-based text (no Canvas)

## Scripts Overview

| Script | Purpose |
|--------|---------|
| `ConnectionManager` | Lobby + Relay connection flow (create/join/disconnect) |
| `NetworkPlayer` | NetworkBehaviour — syncs character, score, lives, username |
| `PlayerMove` | Local input, physics, wall slide/jump, animation |
| `LobbyPlayerSetup` | Applies character sprite + animator controller |
| `ApplePickup` | Server-authoritative pickup + respawn |
| `SawDamage` / `SawMovement` | Server-authoritative hazard logic |
| `ScoreManager` / `LivesManager` | Static state with change events |
| `Hud` / `LivesDisplay` / `ScoreboardDisplay` | In-game UI |
| `BitmapText` / `BitmapInputField` | Custom world-space text system |
| `CharacterSelectable` | Menu character selection |
| `NameTag` | Username display above player |
