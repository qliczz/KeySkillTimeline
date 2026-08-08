using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace KeySkillTimeline;

public sealed class OverlayWindow : Window
{
    private readonly Plugin plugin;
    private readonly TimelineEngine engine;
    private DateTime previewUntil;
    private bool manualVisibilityOverride;

    public OverlayWindow(Plugin plugin, TimelineEngine engine)
        : base("技能时间轴悬浮窗##KeySkillTimelineOverlay")
    {
        this.plugin = plugin;
        this.engine = engine;
        Position = new Vector2(38, 210);
        PositionCondition = ImGuiCond.FirstUseEver;
        ShowCloseButton = false;
        AllowClickthrough = false;
    }

    public override bool DrawConditions()
    {
        var config = plugin.Configuration;
        var preview = DateTime.UtcNow <= previewUntil;
        if (!config.ShowOverlay)
            return false;
        if (manualVisibilityOverride)
            return true;
        if (!preview && config.OverlayOnlyWhenApplicable && !engine.IsApplicable())
            return false;
        if (!preview && config.OverlayOnlyWhileRunning && engine.State is TimelineRunState.Idle or TimelineRunState.Complete)
            return false;
        return true;
    }

    public void ShowPreview() => previewUntil = DateTime.UtcNow.AddSeconds(30);

    public void ShowManually()
    {
        manualVisibilityOverride = true;
        IsOpen = true;
    }

    public void HideManually()
    {
        manualVisibilityOverride = false;
        IsOpen = false;
    }

    public bool IsCurrentlyVisible => IsOpen && DrawConditions();

    public string VisibilityStatus()
    {
        var config = plugin.Configuration;
        if (!config.ShowOverlay)
            return "已关闭：悬浮窗功能未启用";
        if (!IsOpen)
            return "已关闭：窗口开关处于关闭状态";
        if (manualVisibilityOverride)
            return "正在显示：手动显示模式";
        if (config.OverlayOnlyWhenApplicable && !engine.IsApplicable())
            return "自动隐藏：当前副本或职业不匹配";
        if (config.OverlayOnlyWhileRunning && engine.State is TimelineRunState.Idle or TimelineRunState.Complete)
            return "自动隐藏：时间轴尚未运行";
        return "正在显示：自动显示条件已满足";
    }

    public override void PreDraw()
    {
        var config = plugin.Configuration;
        var flags = ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse;
        if (config.OverlayLocked || config.OverlayClickThrough)
            flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove;
        if (config.OverlayClickThrough)
            flags |= ImGuiWindowFlags.NoInputs;
        Flags = flags;
        BgAlpha = config.OverlayBackgroundOpacity;

        var width = config.OverlayWidth;
        var height = 126f + config.OverlayUpcomingRows * 24f;
        Size = new Vector2(width, height);
        SizeCondition = ImGuiCond.Always;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(width, height),
            MaximumSize = new Vector2(width, height),
        };
    }

    public override void Draw()
    {
        DrawHeader();
        DrawActiveOrNext();
        DrawCompactTimeline();
        DrawUpcomingRows();
    }

    private void DrawHeader()
    {
        var stateColor = engine.State == TimelineRunState.Running
            ? new Vector4(0.35f, 0.9f, 0.48f, 1f)
            : new Vector4(1f, 0.68f, 0.25f, 1f);
        ImGui.TextColored(stateColor, StateText(engine.State));
        ImGui.SameLine();
        ImGui.Text($"{MainWindow.FormatTime(engine.CurrentTime)}  {engine.CurrentPhase().Name}");
        ImGui.SameLine();
        ImGui.TextDisabled($"校时：{engine.LastSyncLabel}");
    }

    private void DrawActiveOrNext()
    {
        var reminder = engine.ActiveReminder;
        if (plugin.Configuration.EnableVisualBanner
            && reminder is not null
            && DateTime.UtcNow <= engine.ActiveReminderUntil)
        {
            var color = ImGui.ColorConvertU32ToFloat4(reminder.Skill.Color);
            ImGui.SetWindowFontScale(1.25f);
            ImGui.TextColored(color, $"现在准备：{reminder.Skill.Name}");
            ImGui.SetWindowFontScale(1f);
            if (plugin.Configuration.OverlayShowNotes && !string.IsNullOrWhiteSpace(reminder.Entry.Note))
            {
                ImGui.SameLine();
                ImGui.TextDisabled(reminder.Entry.Note);
            }
            return;
        }

        var next = engine.Upcoming(engine.CurrentTime - 0.01f, DancingMadPreset.DurationSeconds).FirstOrDefault();
        if (next.Entry is null)
        {
            ImGui.TextDisabled("当前计划没有后续技能。");
            return;
        }

        var remaining = next.Entry.TimeSeconds - engine.CurrentTime;
        var colorNext = ImGui.ColorConvertU32ToFloat4(next.Skill.Color);
        ImGui.Text("下一项：");
        ImGui.SameLine();
        ImGui.SetWindowFontScale(1.18f);
        ImGui.TextColored(colorNext, next.Skill.Name);
        ImGui.SetWindowFontScale(1f);
        ImGui.SameLine();
        ImGui.Text($"{remaining:0.0}s");
        if (plugin.Configuration.OverlayShowNotes && !string.IsNullOrWhiteSpace(next.Entry.Note))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(next.Entry.Note);
        }
    }

    private void DrawCompactTimeline()
    {
        const float height = 46f;
        var size = new Vector2(Math.Max(100, ImGui.GetContentRegionAvail().X), height);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##overlayTimeline", size);
        var draw = ImGui.GetWindowDrawList();
        var background = ImGui.GetColorU32(new Vector4(0.055f, 0.065f, 0.085f, 0.96f));
        var grid = ImGui.GetColorU32(new Vector4(0.38f, 0.42f, 0.48f, 0.60f));
        var text = ImGui.GetColorU32(new Vector4(0.82f, 0.84f, 0.88f, 1f));
        draw.AddRectFilled(origin, origin + size, background, 4f);

        var future = plugin.Configuration.OverlayFutureSeconds;
        for (var second = 0; second <= future; second += 10)
        {
            var x = origin.X + second / future * size.X;
            draw.AddLine(new Vector2(x, origin.Y + 15), new Vector2(x, origin.Y + size.Y), grid, second == 0 ? 1.5f : 0.6f);
            draw.AddText(new Vector2(x + 3, origin.Y + 2), text, second == 0 ? "现在" : $"+{second}s");
        }

        var upcoming = engine.Upcoming(engine.CurrentTime, engine.CurrentTime + future);
        for (var i = 0; i < upcoming.Count; i++)
        {
            var row = upcoming[i];
            var delta = Math.Max(0, row.Entry.TimeSeconds - engine.CurrentTime);
            var x = origin.X + delta / future * size.X;
            var y0 = origin.Y + 19 + (i % 2) * 10;
            draw.AddRectFilled(new Vector2(x - 2, y0), new Vector2(x + 3, origin.Y + size.Y - 3), row.Skill.Color, 2f);
            if (i < 3)
            {
                var label = row.Skill.Name;
                var labelSize = ImGui.CalcTextSize(label);
                var labelX = Math.Clamp(x + 5, origin.X + 4, origin.X + size.X - labelSize.X - 4);
                draw.AddText(new Vector2(labelX, y0 - 1), row.Skill.Color, label);
            }
        }
    }

    private void DrawUpcomingRows()
    {
        var rows = engine.Upcoming(engine.CurrentTime, engine.CurrentTime + plugin.Configuration.VisibleFutureSeconds)
            .Take(plugin.Configuration.OverlayUpcomingRows)
            .ToList();
        if (rows.Count == 0)
            return;

        if (!ImGui.BeginTable("overlayUpcomingRows", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg))
            return;
        ImGui.TableSetupColumn("倒计时", ImGuiTableColumnFlags.WidthFixed, 65);
        ImGui.TableSetupColumn("技能", ImGuiTableColumnFlags.WidthFixed, 105);
        ImGui.TableSetupColumn("备注");
        foreach (var row in rows)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text($"{row.Entry.TimeSeconds - engine.CurrentTime:0.0}s");
            ImGui.TableSetColumnIndex(1);
            ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(row.Skill.Color), row.Skill.Name);
            ImGui.TableSetColumnIndex(2);
            if (plugin.Configuration.OverlayShowNotes)
                ImGui.TextDisabled(row.Entry.Note);
        }
        ImGui.EndTable();
    }

    private static string StateText(TimelineRunState state) => state switch
    {
        TimelineRunState.Running => "运行中",
        TimelineRunState.Paused => "已暂停",
        TimelineRunState.Complete => "已结束",
        _ => "待机",
    };
}
