using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Reads all NetworkPlayers, ranks them by score (desc)
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

        // Gather all active NetworkPlayers (skip ones not yet spawned)
        players.Clear();
        var allFound = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var p in allFound)
        {
            if (p.Runner == null) continue; // not yet linked to a runner
            players.Add(p);
        }

        if (players.Count == 0)
        {
            rankText.SetText("");
            return;
        }

        // Sort by score desc
        // Fusion [Networked] properties are accessed directly (no .Value wrapper)
        players.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Build ranked text
        string[] lines = new string[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            string name = p.UsernameNV.ToString();
            if (name.Length > 8)
                name = name[..8];
            lines[i] = $"{i + 1}. {name}  {p.Score:D3}";
        }

        rankText.SetText(string.Join("\n", lines));
    }
}
