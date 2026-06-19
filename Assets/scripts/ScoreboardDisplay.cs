using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads all NetworkPlayers, ranks them by score (desc) / deaths (asc),
/// and renders the list via BitmapText.
/// Place on a child GameObject of the scoreboard area.
/// </summary>
public class ScoreboardDisplay : MonoBehaviour
{
    [SerializeField] private BitmapText rankText;

    private List<NetworkPlayer> players = new();
    private float refreshTimer;

    private void OnEnable()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        // Refresh periodically to catch stat changes
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.5f;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (rankText == null) return;

        // Gather all active NetworkPlayers
        players.Clear();
        players.AddRange(FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None));

        if (players.Count == 0)
        {
            rankText.SetText("");
            return;
        }

        // Sort by score desc, then deaths asc (tie-breaker)
        players.Sort((a, b) =>
        {
            int cmp = b.Score.Value.CompareTo(a.Score.Value);
            if (cmp == 0)
                cmp = a.Deaths.Value.CompareTo(b.Deaths.Value);
            return cmp;
        });

        // Build ranked text
        string[] lines = new string[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            string name = p.UsernameNV.Value.ToString();
            if (name.Length > 8)
                name = name[..8];
            lines[i] = $"{i + 1}. {name}  {p.Score.Value:D3}  {p.Deaths.Value}";
        }

        rankText.SetText(string.Join("\n", lines));
    }
}
