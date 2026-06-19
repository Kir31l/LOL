using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NetworkBehaviour that syncs the room code from server to all clients.
/// Place on a NetworkObject in the lobby scene (e.g. under NetworkManager).
/// </summary>
public class RoomCodeSync : NetworkBehaviour
{
    /// <summary>
    /// The room code, server-authoritative.
    /// </summary>
    public NetworkVariable<int> RoomCode = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>Fired when RoomCode changes (any client).</summary>
    public event System.Action<int> OnRoomCodeChanged;

    private void Awake()
    {
        RoomCode.OnValueChanged += (_, newVal) => OnRoomCodeChanged?.Invoke(newVal);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            RoomCode.Value = RoomCodeManager.CurrentCode;
            Debug.Log($"RoomCodeSync: Server set room code to {RoomCode.Value}");
        }
    }

    /// <summary>Convenience: get the current room code from any code that holds a reference.</summary>
    public int CurrentCode => RoomCode.Value;
}
