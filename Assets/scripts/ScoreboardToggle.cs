using UnityEngine;

/// <summary>
/// Click the button to toggle the scoreboard panel on/off.
/// </summary>
public class ScoreboardToggle : MonoBehaviour
{
    [SerializeField] private GameObject scoreboardPanel;

    void Start()
    {
        if (scoreboardPanel != null)
            scoreboardPanel.SetActive(false);
    }

    void OnMouseDown()
    {
        if (scoreboardPanel != null)
            scoreboardPanel.SetActive(!scoreboardPanel.activeSelf);
    }
}
