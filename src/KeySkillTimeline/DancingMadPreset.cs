namespace KeySkillTimeline;

public static class DancingMadPreset
{
    public const uint TerritoryId = 1363;
    public const uint WhiteMageJobId = 24;
    public const float DurationSeconds = 1114.3f;
    public const string Source = "FF Logs BfwHNxhPJRKy76cV / fight 5";

    public static List<SkillSetting> CreateSkills() =>
    [
        new() { Key = "temperance", Name = "节制", Color = 0xFFFFB770, IconId = 2645 },
        new() { Key = "caress", Name = "神爱抚", Color = 0xFFE8A8FF, IconId = 2128 },
        new() { Key = "aquaveil", Name = "水流幕", Color = 0xFF57C8FF, IconId = 2648 },
        new() { Key = "benison", Name = "神祝祷", Color = 0xFF8AF0B7, IconId = 2638 },
        new() { Key = "asylum", Name = "庇护所", Color = 0xFFD1E38F, IconId = 2632 },
        new() { Key = "liturgy", Name = "礼仪之铃", Color = 0xFFFFA6D5, IconId = 2649 },
    ];

    public static readonly PhaseMarker[] Phases =
    [
        new("P1", 0f),
        new("P2", 209.035f),
        new("P3", 428.631f),
        new("P4", 735.007f),
        new("P5", 898.247f),
    ];

    // FFLogs 中首领 StartsUsing 的代表性锚点。Action ID 为十六进制事件 ID。
    public static readonly SyncPoint[] SyncPoints =
    [
        new(0xC403, 11.1f, "P1 开场"), new(0xBCF2, 27.1f, "P1"),
        new(0xBAA6, 42.5f, "P1"), new(0xC622, 58.6f, "P1"),
        new(0xC403, 93.2f, "P1"), new(0xC622, 128.4f, "P1"),
        new(0xBAB9, 147.6f, "P1"), new(0xC554, 166.8f, "P1"),
        new(0xBA94, 182.5f, "P1 末尾"),
        new(0xC24C, 216.3f, "P2 开场"), new(0xBABC, 229.5f, "P2"),
        new(0xBAD2, 252.7f, "P2"), new(0xBADD, 286.6f, "P2"),
        new(0xBADC, 307.8f, "P2"), new(0xBABD, 337.8f, "P2"),
        new(0xBADF, 351.0f, "P2"), new(0xC487, 367.2f, "P2"),
        new(0xC3F7, 385.0f, "P2"), new(0xBAE2, 421.6f, "P2 末尾"),
        new(0xC2E2, 428.8f, "P3 开场"), new(0xBAF2, 446.2f, "P3"),
        new(0xBB12, 463.4f, "P3"), new(0xBAFE, 487.4f, "P3"),
        new(0xBB00, 507.8f, "P3"), new(0xBB09, 533.1f, "P3"),
        new(0xC571, 555.3f, "P3"), new(0xBAE6, 570.8f, "P3"),
        new(0xBB01, 599.9f, "P3"), new(0xBAEC, 628.7f, "P3"),
        new(0xBD66, 667.7f, "P3"), new(0xBAED, 686.7f, "P3"),
        new(0xC61E, 726.6f, "P3 末尾"),
        new(0xC2DC, 740.2f, "P4 开场"), new(0xBB14, 750.2f, "P4"),
        new(0xBB20, 770.3f, "P4"), new(0xC3A1, 796.2f, "P4"),
        new(0xBAA4, 807.0f, "P4"), new(0xC24A, 823.4f, "P4"),
        new(0xBB25, 848.9f, "P4"), new(0xC24A, 862.3f, "P4 末尾"),
        new(0xBB40, 901.4f, "P5 开场"), new(0xC13F, 917.9f, "P5"),
        new(0xBB50, 931.1f, "P5"), new(0xBB42, 952.8f, "P5"),
        new(0xC24F, 974.1f, "P5"), new(0xBB3B, 997.8f, "P5"),
        new(0xBB3E, 1013.9f, "P5"), new(0xBB35, 1047.9f, "P5"),
        new(0xBB3A, 1092.5f, "P5 末尾"),
    ];

    public static List<TimelineEntry> CreateEntries()
    {
        var result = new List<TimelineEntry>();
        Add(result, "temperance", 28.1f, 149.7f, 271.1f, 468.6f, 604.7f, 759.9f, 904.7f, 1054.7f);
        Add(result, "caress", 45.1f, 170.5f, 283.8f, 491.2f, 606.5f, 761.4f, 911.9f, 1069.5f);
        Add(result, "aquaveil", 14.5f, 218.0f, 375.5f, 468.0f, 637.0f, 991.8f);
        Add(result, "benison", 16.7f, 65.8f, 100.5f, 215.5f, 264.8f, 276.9f, 298.6f, 351.6f, 377.4f, 426.9f, 446.3f, 480.2f, 503.8f, 527.3f, 555.0f, 578.5f, 616.3f, 639.7f, 666.9f, 699.1f, 856.8f, 939.1f, 995.5f, 1034.8f, 1039.9f);
        Add(result, "asylum", 25.8f, 116.4f, 223.0f, 314.7f, 456.9f, 555.8f, 646.9f, 755.1f, 902.4f, 993.6f, 1085.5f);
        Add(result, "liturgy", 71.5f, 257.8f, 274.5f, 506.2f, 788.6f, 796.2f, 1061.6f);
        return result.OrderBy(x => x.TimeSeconds).ToList();
    }

    private static void Add(List<TimelineEntry> target, string key, params float[] times)
    {
        foreach (var time in times)
            target.Add(new TimelineEntry { SkillKey = key, TimeSeconds = time });
    }
}
