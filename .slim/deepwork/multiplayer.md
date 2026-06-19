# Multiplayer Conversion — Current State

## Scripts Written (ready to go)
- `NetworkPlayer.cs` — NetworkBehaviour, owner-gates movement, syncs character index via NetworkVariable
- `ConnectionManager.cs` — DontDestroyOnLoad, handles Create/Join/Disconnect, loads lobby via NetworkSceneManager
- `CameraFollower.cs` — simple LateUpdate follow on Main Camera
- `LoadSceneOnClick.cs` — Mode enum: LoadScene / StartHost / StartClient
- `LobbyPlayerSetup.cs` — ApplyCharacter() for NetworkPlayer, plus single-player fallback

## Manifest
- `com.unity.netcode.gameobjects` 2.11.2 already in manifest
- `com.unity.transport` auto-resolves as dependency

## Pending Editor Work
1. **Menu scene**: Create NetworkManager GO with NetworkManager + UnityTransport components
2. **Prefab**: Create NetworkPlayer prefab from lobby player setup
3. **NetworkManager**: Assign Player Prefab slot
4. **Menu**: Wire Create/Join buttons (LoadSceneOnClick mode → StartHost/StartClient)
5. **Lobby**: Remove static player, add NetworkStartPositions
6. Test: host + client connection, character sync, movement
