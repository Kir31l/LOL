public static class ScoreManager
{
    public static int Score { get; private set; }

    public static event System.Action<int> OnScoreChanged;

    public static void AddPoints(int points)
    {
        Score += points;
        OnScoreChanged?.Invoke(Score);
    }

    public static void Reset()
    {
        Score = 0;
        OnScoreChanged?.Invoke(Score);
    }
}
