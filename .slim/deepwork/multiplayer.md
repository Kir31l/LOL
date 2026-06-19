# Multiplayer Conversion

## Scripts Ready
- `NetworkPlayer.cs` — NetworkBehaviour, owner-gated movement, NetworkVariable<int> character sync
- `ConnectionManager.cs` — DontDestroyOnLoad, Create/Join/Disconnect, loads lobby via NetworkSceneManager
- `CameraFollower.cs` — simple LateUpdate follow on Main Camera
- `LoadSceneOnClick.cs` — Mode enum (LoadScene/StartHost/StartClient)
- `LobbyPlayerSetup.cs` — ApplyCharacter() for NetworkPlayer + single-player fallback

## Setup Method
Created `Assets/Editor/NetworkSceneSetup.cs` — Unity Editor tool.
Open menu: **Tools → Network → Setup All Scenes**

### What the tool does:
1. **Menu scene**: Creates NetworkManager GO with NetworkManager + UnityTransport + ConnectionManager. Wires CREATE button → StartHost. Wires JOIN button → StartClient. Both auto-save username.
2. **Lobby scene**: Adds NetworkObject, NetworkTransform, NetworkRigidbody2D, NetworkPlayer to the existing player. Creates `Assets/Prefabs/NetworkPlayer.prefab`. Removes old player. Creates NetworkManager + assigns Player Prefab. Adds 4 NetworkStartPositions.

### To test:
1. Open Unity, let packages resolve
2. Tools → Network → Setup All Scenes
3. Open menu.unity, press Play → click CREATE (hosts + loads lobby)
4. Build standalone exe → run → click JOIN (connects to editor host)
