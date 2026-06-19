using UnityEngine;

public static class LivesManager
{
    public static int MaxLives = 3;
    public static int Lives { get; private set; } = 3;

    public static event System.Action<int> OnLivesChanged;

    /// <summary>Set lives directly (used by NetworkPlayer sync).</summary>
    public static void SetLives(int value)
    {
        if (Lives == value) return;
        Lives = Mathf.Clamp(value, 0, MaxLives);
        OnLivesChanged?.Invoke(Lives);
    }

    /// <summary>Removes one life. Returns true if at zero.</summary>
    public static bool RemoveLife()
    {
        Lives = Mathf.Max(0, Lives - 1);
        OnLivesChanged?.Invoke(Lives);
        return Lives <= 0;
    }

    public static void Reset()
    {
        Lives = MaxLives;
        OnLivesChanged?.Invoke(Lives);
    }
}
