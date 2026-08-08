using Dalamud.Configuration;
using Dalamud.Plugin;

namespace KeySkillTimeline;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;
    public bool Enabled { get; set; } = true;
    public bool AutoStart { get; set; } = true;
    public bool AutoOpen { get; set; } = true;
    public bool OnlyInTargetTerritory { get; set; } = true;
    public bool OnlyOnWhiteMage { get; set; } = true;
    public bool EnableVisualBanner { get; set; } = true;
    public bool EnableDalamudNotification { get; set; } = true;
    public bool EnableChineseTts { get; set; } = true;
    public bool EnableAutoSync { get; set; } = true;
    public bool ShowPastEntries { get; set; }
    public bool ShowOverlay { get; set; } = true;
    public bool OverlayLocked { get; set; }
    public bool OverlayClickThrough { get; set; }
    public bool OverlayOnlyWhileRunning { get; set; } = true;
    public bool OverlayOnlyWhenApplicable { get; set; } = true;
    public float VisibleFutureSeconds { get; set; } = 70f;
    public float PixelsPerSecond { get; set; } = 9f;
    public float BannerSeconds { get; set; } = 4f;
    public float ResetAfterOutOfCombatSeconds { get; set; } = 15f;
    public float SyncWindowSeconds { get; set; } = 25f;
    public float ManualOffsetSeconds { get; set; }
    public float OverlayBackgroundOpacity { get; set; } = 0.86f;
    public float OverlayWidth { get; set; } = 420f;
    public float OverlayHeight { get; set; } = 120f;
    public int OverlayFontSizePx { get; set; } = 24;
    public int TtsRate { get; set; }
    public List<SkillSetting> Skills { get; set; } = [];
    public List<TimelineEntry> Entries { get; set; } = [];

    [NonSerialized] private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
    {
        pluginInterface = pi;
        var migrated = false;
        if (Version < 2)
        {
            Version = 2;
            AutoOpen = false;
            ShowOverlay = true;
            migrated = true;
        }
        if (Version < 3)
        {
            Version = 3;
            OverlayWidth = 420f;
            OverlayHeight = 120f;
            migrated = true;
        }
        if (Version < 4)
        {
            Version = 4;
            OverlayFontSizePx = 24;
            migrated = true;
        }
        if (Skills.Count == 0)
            Skills = DancingMadPreset.CreateSkills();
        if (Entries.Count == 0)
            Entries = DancingMadPreset.CreateEntries();
        Normalize();
        if (migrated)
            pluginInterface.SavePluginConfig(this);
    }

    public void Normalize()
    {
        foreach (var preset in DancingMadPreset.CreateSkills())
        {
            if (Skills.All(x => x.Key != preset.Key))
                Skills.Add(preset);
        }

        foreach (var skill in Skills)
        {
            skill.LeadSeconds = Math.Clamp(skill.LeadSeconds, 0f, 30f);
            if (string.IsNullOrWhiteSpace(skill.Name))
                skill.Name = skill.Key;
        }

        foreach (var entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
                entry.Id = Guid.NewGuid().ToString("N");
            entry.TimeSeconds = Math.Max(0f, entry.TimeSeconds);
        }
        OverlayBackgroundOpacity = Math.Clamp(OverlayBackgroundOpacity, 0.05f, 1f);
        OverlayWidth = Math.Clamp(OverlayWidth, 220f, 900f);
        OverlayHeight = Math.Clamp(OverlayHeight, 70f, 300f);
        OverlayFontSizePx = Math.Clamp(OverlayFontSizePx, 14, 48);
        Entries = Entries.OrderBy(x => x.TimeSeconds).ToList();
    }

    public void Save()
    {
        Normalize();
        pluginInterface?.SavePluginConfig(this);
    }

    public void ResetPlan()
    {
        Skills = DancingMadPreset.CreateSkills();
        Entries = DancingMadPreset.CreateEntries();
        Save();
    }
}
