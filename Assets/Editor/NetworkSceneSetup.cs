#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;

/// <summary>
/// One-click tool to set up networked scenes.
/// Tools → Network → Setup All Scenes
/// </summary>
public static class NetworkSceneSetup
{
    [MenuItem("Tools/Network/Setup All Scenes")]
    public static void SetupAll()
    {
        EditorSceneManager.SaveOpenScenes();

        // Step 1: Menu scene — create NetworkManager, wire buttons
        SetupMenuScene();

        // Step 2: Lobby scene — create prefab, remove old player, add NM + spawn points
        SetupLobbyScene();

        Debug.Log("=== Network setup complete! Open menu scene and click Play to test as host. ===");
    }

    [MenuItem("Tools/Network/Step 1: Setup Menu Scene")]
    private static void SetupMenuScene()
    {
        var menuScene = EditorSceneManager.OpenScene("Assets/Scenes/menu.unity", OpenSceneMode.Single);

        // --- Create NetworkManager with ConnectionManager ---
        CreateNetworkManager();

        // --- Wire CREATE button (StartHost) ---
        WireButton("CREATE", 1);

        // --- Wire JOIN button (StartClient) ---
        WireButton("JOIN", 2);

        EditorSceneManager.SaveScene(menuScene);
        Debug.Log("Menu: NetworkManager + buttons wired.");
    }

    [MenuItem("Tools/Network/Step 2: Setup Lobby Scene")]
    private static void SetupLobbyScene()
    {
        var lobbyScene = EditorSceneManager.OpenScene("Assets/Scenes/lobby.unity", OpenSceneMode.Single);

        // --- Capture old player data then delete it ---
        var player = GameObject.Find("player");
        if (player == null)
        {
            Debug.LogError("No 'player' GameObject found in lobby scene.");
            return;
        }

        // Capture references before deletion
        var playerMove = player.GetComponent<PlayerMove>();
        var lobbySetup = player.GetComponent<LobbyPlayerSetup>();
        var sr = player.GetComponent<SpriteRenderer>();
        var rb = player.GetComponent<Rigidbody2D>();
        var capsule = player.GetComponent<CapsuleCollider2D>();
        var anim = player.GetComponent<Animator>();
        var nameTagChild = player.transform.Find("NameTag");

        // --- Create NetworkPlayer prefab ---
        CreateNetworkPlayerPrefab(player, playerMove, lobbySetup, nameTagChild);

        // --- Delete old player ---
        Object.DestroyImmediate(player);

        // --- Create NetworkManager with ConnectionManager + Player Prefab ---
        CreateNetworkManager();

        // --- Assign player prefab ---
        string prefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            var nm = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>();
            if (nm != null)
            {
                SerializedObject so = new SerializedObject(nm);
                var pp = so.FindProperty("m_NetworkConfig.m_PlayerPrefab");
                if (pp != null)
                {
                    pp.objectReferenceValue = prefab;
                    so.ApplyModifiedProperties();
                    Debug.Log("NetworkManager: Player Prefab assigned.");
                }
            }
        }

        // --- Add spawn points ---
        AddSpawnPoints();

        // --- Add room code sync + display ---
        AddRoomCodeSync();
        AddRoomCodeDisplay();

        EditorSceneManager.SaveScene(lobbyScene);
        Debug.Log("Lobby: player removed, NetworkManager configured, spawn points added.");
    }

    private static void CreateNetworkManager()
    {
        // Don't duplicate if one exists
        var existing = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>();
        if (existing != null)
        {
            if (existing.GetComponent<ConnectionManager>() == null)
                existing.gameObject.AddComponent<ConnectionManager>();
            return;
        }

        var go = new GameObject("NetworkManager");
        go.AddComponent<Unity.Netcode.NetworkManager>();
        go.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        go.AddComponent<ConnectionManager>();
        Debug.Log("Created NetworkManager GameObject.");
    }

    private static void WireButton(string name, int modeIndex)
    {
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        var targets = allGOs.Where(go => go.name == name).ToArray();

        if (targets.Length == 0)
        {
            Debug.LogWarning($"No GameObject named '{name}' found.");
            return;
        }

        // Find username input field
        var inputField = FindUsernameInput();

        foreach (var btn in targets)
        {
            var lsoc = btn.GetComponent<LoadSceneOnClick>();
            if (lsoc == null)
                lsoc = btn.AddComponent<LoadSceneOnClick>();

            SerializedObject so = new SerializedObject(lsoc);
            so.FindProperty("mode").enumValueIndex = modeIndex;
            so.FindProperty("sceneName").stringValue = "lobby";
            if (inputField != null)
                so.FindProperty("inputField").objectReferenceValue = inputField;
            so.ApplyModifiedProperties();
        }

        Debug.Log($"Wired '{name}' buttons → mode {modeIndex}");
    }

    private static BitmapInputField FindUsernameInput()
    {
        var allInputs = Object.FindObjectsByType<BitmapInputField>(FindObjectsSortMode.None);
        // Prefer object named "UsernameInput"
        foreach (var inp in allInputs)
            if (inp.name == "UsernameInput" || inp.gameObject.name == "UsernameInput")
                return inp;
        // Fallback to first
        return allInputs.FirstOrDefault();
    }

    private static void CreateNetworkPlayerPrefab(
        GameObject player,
        PlayerMove playerMove,
        LobbyPlayerSetup lobbySetup,
        Transform nameTagChild)
    {
        // Ensure Prefabs directory
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // Add networking components to the player (will become the prefab)
        var netObj = player.GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj == null) netObj = player.AddComponent<Unity.Netcode.NetworkObject>();

        var netTransform = player.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform == null)
        {
            netTransform = player.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            // Enable interpolation via public API if available
            var so = new SerializedObject(netTransform);
            var interp = so.FindProperty("m_Interpolate");
            if (interp != null) interp.boolValue = true;
            var syncScaleX = so.FindProperty("m_SyncScaleX");
            if (syncScaleX != null) syncScaleX.boolValue = true;
            var syncScaleY = so.FindProperty("m_SyncScaleY");
            if (syncScaleY != null) syncScaleY.boolValue = true;
            so.ApplyModifiedProperties();
        }

        var netRb = player.GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
        if (netRb == null)
        {
            netRb = player.AddComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
            var so = new SerializedObject(netRb);
            var auth = so.FindProperty("m_ClientAuthority");
            if (auth != null) auth.boolValue = true;
            so.ApplyModifiedProperties();
        }

        var netPlayer = player.GetComponent<NetworkPlayer>();
        if (netPlayer == null) netPlayer = player.AddComponent<NetworkPlayer>();
        var npSo = new SerializedObject(netPlayer);
        npSo.FindProperty("playerMove").objectReferenceValue = playerMove;
        npSo.FindProperty("lobbySetup").objectReferenceValue = lobbySetup;
        npSo.ApplyModifiedProperties();

        // Save as prefab
        string prefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
            AssetDatabase.DeleteAsset(prefabPath);

        PrefabUtility.SaveAsPrefabAsset(player, prefabPath);
        Debug.Log($"NetworkPlayer prefab created at {prefabPath}");
    }

    private static void AddSpawnPoints()
    {
        // Find the NetworkManager GO to parent under it
        var nm = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>();
        GameObject parent = nm != null ? nm.gameObject : new GameObject("SpawnPoints");

        var spawnParent = parent.transform.Find("SpawnPoints");
        if (spawnParent == null)
        {
            spawnParent = new GameObject("SpawnPoints").transform;
            spawnParent.SetParent(parent.transform);
            spawnParent.localPosition = Vector3.zero;
        }

        // Clear existing
        while (spawnParent.childCount > 0)
            Object.DestroyImmediate(spawnParent.GetChild(0).gameObject);

        var positions = new Vector3[] {
            new Vector3(-3, -1, 0),
            new Vector3(0, -1, 0),
            new Vector3(3, -1, 0),
            new Vector3(6, -1, 0),
        };

        // Ensure SpawnPoint tag exists
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        bool hasTag = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == "SpawnPoint") { hasTag = true; break; }
        }
        if (!hasTag)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = "SpawnPoint";
            tagManager.ApplyModifiedProperties();
        }

        foreach (var pos in positions)
        {
            var spGo = new GameObject("SpawnPosition");
            spGo.transform.SetParent(spawnParent);
            spGo.transform.localPosition = pos;
            spGo.tag = "SpawnPoint";
        }

        Debug.Log($"Added {positions.Length} spawn positions (NetworkPlayer will use them via OnNetworkSpawn).");
    }

    private static void AddRoomCodeSync()
    {
        var nm = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>();
        if (nm == null) return;

        // Don't duplicate
        if (nm.GetComponentInChildren<RoomCodeSync>() != null) return;

        var syncGo = new GameObject("RoomSync");
        syncGo.transform.SetParent(nm.transform);
        syncGo.transform.localPosition = Vector3.zero;
        syncGo.AddComponent<Unity.Netcode.NetworkObject>();
        syncGo.AddComponent<RoomCodeSync>();
        Debug.Log("Added RoomCodeSync under NetworkManager.");
    }

    private static void AddRoomCodeDisplay()
    {
        var hud = GameObject.Find("HUD");
        if (hud == null) return;

        // Don't duplicate
        if (hud.transform.Find("roomCode") != null) return;

        // Find an existing BitmapText to copy settings from (e.g. score text)
        var scoreChild = hud.transform.Find("score");
        BitmapText template = scoreChild != null
            ? scoreChild.GetComponent<BitmapText>()
            : null;

        var rcGo = new GameObject("roomCode");
        rcGo.transform.SetParent(hud.transform);
        rcGo.transform.localPosition = new Vector3(0, -2.5f, 0); // below score

        // Copy BitmapText settings from the score template
        if (template != null)
        {
            var dst = rcGo.AddComponent<BitmapText>();
            EditorUtility.CopySerialized(template, dst);
            // Set text via serialized property
            SerializedObject so = new SerializedObject(dst);
            var textProp = so.FindProperty("text");
            if (textProp != null) textProp.stringValue = "ROOM: ----";
            so.ApplyModifiedProperties();
            // Rebuild so child sprites are created in the scene
            dst.Rebuild();
        }

        rcGo.AddComponent<RoomCodeDisplay>();
        Debug.Log("Added roomCode display to HUD.");
    }
}
#endif
