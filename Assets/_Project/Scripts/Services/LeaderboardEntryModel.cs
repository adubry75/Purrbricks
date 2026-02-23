using Steamworks;

public class LeaderboardEntryModel
{
    public int Rank { get; }
    public int Score { get; }
    public CSteamID SteamId { get; }
    public string DisplayName { get; }

    public LeaderboardEntryModel(int rank, int score, CSteamID steamId, string displayName)
    {
        Rank = rank;
        Score = score;
        SteamId = steamId;
        DisplayName = displayName;
    }
}
