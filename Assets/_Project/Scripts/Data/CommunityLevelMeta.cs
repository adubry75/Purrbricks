using System;
using System.Collections.Generic;

/// <summary>Metadata for a community-published level (no json_data — use CommunityLevelService.FetchLevel for that).</summary>
[Serializable]
public class CommunityLevelMeta
{
    public int    id;
    public string steamId;
    public string steamName;
    public string title;
    public string description;
    public int    brickCount;
    public int    playCount;
    public int    ratingCount;
    public float  averageRating;
    public string publishedAt;
}

/// <summary>Paginated response from list.php.</summary>
[Serializable]
public class CommunityLevelPage
{
    public List<CommunityLevelMeta> levels;
    public int total;
    public int page;
    public int perPage;
}
