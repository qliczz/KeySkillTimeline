namespace KeySkillTimeline;

public sealed class SkillSetting
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public float LeadSeconds { get; set; } = 4f;
    public uint Color { get; set; } = 0xFFFFFFFF;
    public uint IconId { get; set; }
}

public sealed class TimelineEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SkillKey { get; set; } = string.Empty;
    public float TimeSeconds { get; set; }
    public string Note { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public sealed record PhaseMarker(string Name, float TimeSeconds);

public sealed record SyncPoint(uint ActionId, float TimeSeconds, string Label);

public sealed record ReminderEvent(TimelineEntry Entry, SkillSetting Skill, float FiredAt);
