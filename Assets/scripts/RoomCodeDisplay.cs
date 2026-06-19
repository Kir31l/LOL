using UnityEngine;

/// <summary>
/// Attach to a BitmapText GameObject in the lobby HUD.
/// Displays the room code synced from RoomCodeSync.
/// </summary>
[RequireComponent(typeof(BitmapText))]
public class RoomCodeDisplay : MonoBehaviour
{
    private BitmapText bitmapText;
    private RoomCodeSync sync;

    private void Awake()
    {
        bitmapText = GetComponent<BitmapText>();
    }

    private void Start()
    {
        // Find the RoomCodeSync in the scene
        sync = Object.FindFirstObjectByType<RoomCodeSync>();

        if (sync != null)
        {
            // Show current value immediately
            UpdateDisplay(sync.RoomCode.Value);
            // Subscribe to changes
            sync.OnRoomCodeChanged += UpdateDisplay;
        }
        else
        {
            // Fallback: show the static value (only works if host loaded directly)
            UpdateDisplay(RoomCodeManager.CurrentCode);
        }
    }

    private void OnDestroy()
    {
        if (sync != null)
            sync.OnRoomCodeChanged -= UpdateDisplay;
    }

    private void UpdateDisplay(int code)
    {
        if (bitmapText != null)
        {
            bitmapText.SetText(code > 0 ? $"ROOM: {code:D4}" : "");
        }
    }
}
