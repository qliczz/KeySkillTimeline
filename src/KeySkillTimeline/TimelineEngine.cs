using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;

namespace KeySkillTimeline;

public enum TimelineRunState
{
    Idle,
    Running,
    Paused,
    Complete,
}

public sealed class TimelineEngine
{
    private readonly Configuration configuration;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IPlayerState playerState;
    private readonly IObjectTable objectTable;
    private readonly INotificationManager notifications;
    private readonly IPluginLog log;
    private readonly SpeechService speech;
    private readonly HashSet<string> fired = [];
    private readonly Dictionary<string, uint> lastCastByObject = [];

    private long anchorTicks;
    private float anchorTime;
    private DateTime? outOfCombatSince;
    private uint lastTerritory;
    private bool wasInCombat;

    public TimelineEngine(
        Configuration configuration,
        IClientState clientState,
        ICondition condition,
        IPlayerState playerState,
        IObjectTable objectTable,
        INotificationManager notifications,
        IPluginLog log,
        SpeechService speech)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.condition = condition;
        this.playerState = playerState;
        this.objectTable = objectTable;
        this.notifications = notifications;
        this.log = log;
        this.speech = speech;
        lastTerritory = clientState.TerritoryType;
    }

    public TimelineRunState State { get; private set; } = TimelineRunState.Idle;
    public float CurrentTime { get; private set; }
    public float LastSyncCorrection { get; private set; }
    public string LastSyncLabel { get; private set; } = "尚未校时";
    public ReminderEvent? ActiveReminder { get; private set; }
    public DateTime ActiveReminderUntil { get; private set; }
    public bool SpeechAvailable => speech.Available;
    public string SpeechProviderName => speech.ProviderName;

    public event Action? EncounterStarted;

    public void Update(IFramework _)
    {
        try
        {
            UpdateCore();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Key Skill Timeline update failed.");
        }
    }

    private void UpdateCore()
    {
        var territory = clientState.TerritoryType;
        if (territory != lastTerritory)
        {
            lastTerritory = territory;
            Reset();
        }

        var inCombat = condition[ConditionFlag.InCombat];
        if (configuration.AutoStart && IsApplicable() && inCombat && !wasInCombat && State == TimelineRunState.Idle)
        {
            Start();
            EncounterStarted?.Invoke();
        }

        if (inCombat)
        {
            outOfCombatSince = null;
        }
        else if (State is TimelineRunState.Running or TimelineRunState.Paused)
        {
            outOfCombatSince ??= DateTime.UtcNow;
            if ((DateTime.UtcNow - outOfCombatSince.Value).TotalSeconds >= configuration.ResetAfterOutOfCombatSeconds)
                Reset();
        }
        wasInCombat = inCombat;

        if (State != TimelineRunState.Running)
            return;

        CurrentTime = anchorTime + ElapsedSinceAnchor();
        if (CurrentTime >= DancingMadPreset.DurationSeconds + 10f)
        {
            CurrentTime = DancingMadPreset.DurationSeconds;
            State = TimelineRunState.Complete;
            return;
        }

        if (configuration.EnableAutoSync && IsApplicable())
            ScanBossCasts();
        ProcessReminders();
    }

    public bool IsApplicable()
    {
        if (configuration.OnlyInTargetTerritory && clientState.TerritoryType != DancingMadPreset.TerritoryId)
            return false;
        if (configuration.OnlyOnWhiteMage && playerState.ClassJob.RowId != DancingMadPreset.WhiteMageJobId)
            return false;
        return true;
    }

    public void Start(float time = 0f)
    {
        anchorTime = Math.Clamp(time + configuration.ManualOffsetSeconds, 0f, DancingMadPreset.DurationSeconds);
        anchorTicks = Stopwatch.GetTimestamp();
        CurrentTime = anchorTime;
        State = TimelineRunState.Running;
        fired.Clear();
        lastCastByObject.Clear();
        outOfCombatSince = null;
        MarkPastRemindersAsFired(CurrentTime);
    }

    public void Reset()
    {
        State = TimelineRunState.Idle;
        CurrentTime = 0f;
        anchorTime = 0f;
        fired.Clear();
        lastCastByObject.Clear();
        outOfCombatSince = null;
        ActiveReminder = null;
        LastSyncCorrection = 0f;
        LastSyncLabel = "尚未校时";
    }

    public void TogglePause()
    {
        if (State == TimelineRunState.Running)
        {
            CurrentTime = anchorTime + ElapsedSinceAnchor();
            anchorTime = CurrentTime;
            State = TimelineRunState.Paused;
        }
        else if (State == TimelineRunState.Paused)
        {
            anchorTicks = Stopwatch.GetTimestamp();
            State = TimelineRunState.Running;
        }
    }

    public void SeekBy(float deltaSeconds) => SetCurrentTime(CurrentTime + deltaSeconds, false, "手动调整");

    public void SeekTo(float timeSeconds) => SetCurrentTime(timeSeconds, false, "手动定位");

    public void TestReminder(string? skillKey = null)
    {
        var skill = configuration.Skills.FirstOrDefault(x => x.Key == skillKey)
                    ?? configuration.Skills.FirstOrDefault(x => x.Enabled);
        if (skill is null)
            return;
        FireReminder(new TimelineEntry { SkillKey = skill.Key, TimeSeconds = CurrentTime }, skill, true);
    }

    public IReadOnlyList<(TimelineEntry Entry, SkillSetting Skill)> Upcoming(float fromTime, float untilTime)
    {
        var skillMap = configuration.Skills.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        return configuration.Entries
            .Where(x => x.Enabled && x.TimeSeconds >= fromTime && x.TimeSeconds <= untilTime)
            .Where(x => skillMap.TryGetValue(x.SkillKey, out var skill) && skill.Enabled)
            .Select(x => (Entry: x, Skill: skillMap[x.SkillKey]))
            .OrderBy(x => x.Entry.TimeSeconds)
            .ToList();
    }

    public PhaseMarker CurrentPhase()
        => DancingMadPreset.Phases.LastOrDefault(x => x.TimeSeconds <= CurrentTime) ?? DancingMadPreset.Phases[0];

    private void ProcessReminders()
    {
        if (!configuration.Enabled)
            return;

        var skills = configuration.Skills.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in configuration.Entries)
        {
            if (!entry.Enabled || fired.Contains(entry.Id) || !skills.TryGetValue(entry.SkillKey, out var skill) || !skill.Enabled)
                continue;
            var triggerAt = entry.TimeSeconds - skill.LeadSeconds;
            if (CurrentTime >= triggerAt && CurrentTime <= entry.TimeSeconds + 1.5f)
            {
                fired.Add(entry.Id);
                FireReminder(entry, skill, false);
            }
        }
    }

    private void FireReminder(TimelineEntry entry, SkillSetting skill, bool isTest)
    {
        var reminder = new ReminderEvent(entry, skill, CurrentTime);
        ActiveReminder = reminder;
        ActiveReminderUntil = DateTime.UtcNow.AddSeconds(configuration.BannerSeconds);
        var prefix = isTest ? "测试：" : string.Empty;
        var content = $"{prefix}{skill.Name}{(string.IsNullOrWhiteSpace(entry.Note) ? string.Empty : "，" + entry.Note)}";

        if (configuration.EnableChineseTts)
            speech.Speak($"{content}，准备", configuration.TtsRate);

        if (configuration.EnableDalamudNotification)
        {
            notifications.AddNotification(new Notification
            {
                Title = "关键技能提醒",
                Content = content,
                Type = NotificationType.Warning,
                InitialDuration = TimeSpan.FromSeconds(configuration.BannerSeconds),
            });
        }
    }

    private void ScanBossCasts()
    {
        foreach (var gameObject in objectTable)
        {
            if (gameObject is not IBattleChara battle || !battle.IsCasting || battle.CastActionId == 0)
                continue;

            var key = battle.GameObjectId.ToString();
            var actionId = battle.CastActionId;
            if (lastCastByObject.TryGetValue(key, out var previous) && previous == actionId)
                continue;
            lastCastByObject[key] = actionId;

            var predictedCastStart = CurrentTime - Math.Max(0f, battle.CurrentCastTime);
            var candidate = DancingMadPreset.SyncPoints
                .Where(x => x.ActionId == actionId)
                .Select(x => new { Point = x, Delta = Math.Abs(x.TimeSeconds - predictedCastStart) })
                .Where(x => x.Delta <= configuration.SyncWindowSeconds)
                .OrderBy(x => x.Delta)
                .FirstOrDefault();
            if (candidate is null)
                continue;

            var targetTime = candidate.Point.TimeSeconds + Math.Max(0f, battle.CurrentCastTime);
            SetCurrentTime(targetTime, true, $"{candidate.Point.Label} / {actionId:X4}");
        }

        foreach (var key in lastCastByObject.Keys.ToArray())
        {
            var stillCasting = objectTable.Any(x => x is IBattleChara b && b.GameObjectId.ToString() == key && b.IsCasting);
            if (!stillCasting)
                lastCastByObject.Remove(key);
        }
    }

    private void SetCurrentTime(float time, bool isSync, string label)
    {
        time = Math.Clamp(time, 0f, DancingMadPreset.DurationSeconds);
        var correction = time - CurrentTime;
        anchorTime = time;
        CurrentTime = time;
        anchorTicks = Stopwatch.GetTimestamp();
        if (isSync)
        {
            LastSyncCorrection = correction;
            LastSyncLabel = label;
        }
        MarkPastRemindersAsFired(CurrentTime - 1.5f);
    }

    private void MarkPastRemindersAsFired(float beforeTime)
    {
        var skills = configuration.Skills.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in configuration.Entries)
        {
            if (skills.TryGetValue(entry.SkillKey, out var skill) && entry.TimeSeconds - skill.LeadSeconds < beforeTime)
                fired.Add(entry.Id);
        }
    }

    private float ElapsedSinceAnchor()
        => (float)((Stopwatch.GetTimestamp() - anchorTicks) / (double)Stopwatch.Frequency);
}
