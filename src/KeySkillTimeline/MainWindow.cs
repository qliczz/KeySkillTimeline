using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace KeySkillTimeline;

public sealed class MainWindow : Window
{
    private readonly Plugin plugin;
    private readonly TimelineEngine engine;

    public MainWindow(Plugin plugin, TimelineEngine engine)
        : base("关键技能时间轴##KeySkillTimeline")
    {
        this.plugin = plugin;
        this.engine = engine;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        DrawToolbar();
        DrawStatus();
        DrawReminderBanner();
        DrawNextSkill();
        DrawTimeline();
        DrawUpcoming();
    }

    private void DrawToolbar()
    {
        if (engine.State is TimelineRunState.Idle or TimelineRunState.Complete)
        {
            if (ImGui.Button("开始"))
                engine.Start();
        }
        else
        {
            if (ImGui.Button(engine.State == TimelineRunState.Paused ? "继续" : "暂停"))
                engine.TogglePause();
        }
        ImGui.SameLine();
        if (ImGui.Button("重置"))
            engine.Reset();
        ImGui.SameLine();
        if (ImGui.Button("-1 秒"))
            engine.SeekBy(-1f);
        ImGui.SameLine();
        if (ImGui.Button("+1 秒"))
            engine.SeekBy(1f);
        ImGui.SameLine();
        if (ImGui.Button("测试提醒"))
            engine.TestReminder();
        ImGui.SameLine();
        if (ImGui.Button(plugin.IsOverlayVisible ? "隐藏悬浮窗" : "显示悬浮窗"))
            plugin.ToggleOverlayUi();
        ImGui.SameLine();
        if (ImGui.Button("设置 / 编辑计划"))
            plugin.ToggleConfigUi();
    }

    private void DrawStatus()
    {
        var phase = engine.CurrentPhase();
        var applicable = engine.IsApplicable();
        var statusColor = applicable ? new Vector4(0.45f, 0.9f, 0.55f, 1f) : new Vector4(1f, 0.65f, 0.25f, 1f);
        ImGui.TextColored(statusColor, applicable ? "副本/职业匹配" : "当前不在妖星乱舞，或不是白魔法师");
        ImGui.SameLine();
        ImGui.Text($"  {FormatTime(engine.CurrentTime)}  {phase.Name}  {StateText(engine.State)}");
        ImGui.TextDisabled($"校时：{engine.LastSyncLabel}（最近修正 {engine.LastSyncCorrection:+0.00;-0.00;0.00} 秒）");
    }

    private void DrawReminderBanner()
    {
        var reminder = engine.ActiveReminder;
        if (!plugin.Configuration.EnableVisualBanner || reminder is null || DateTime.UtcNow > engine.ActiveReminderUntil)
            return;

        var color = ImGui.ColorConvertU32ToFloat4(reminder.Skill.Color);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(color.X * 0.35f, color.Y * 0.35f, color.Z * 0.35f, 0.95f));
        if (ImGui.BeginChild("reminderBanner", new Vector2(0, 62), true))
        {
            var text = $"准备使用：{reminder.Skill.Name}";
            var size = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX(Math.Max(8, (ImGui.GetContentRegionAvail().X - size.X) / 2));
            ImGui.SetWindowFontScale(1.45f);
            ImGui.TextColored(color, text);
            ImGui.SetWindowFontScale(1f);
            if (!string.IsNullOrWhiteSpace(reminder.Entry.Note))
                ImGui.TextWrapped(reminder.Entry.Note);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawNextSkill()
    {
        var next = engine.Upcoming(engine.CurrentTime - 0.01f, DancingMadPreset.DurationSeconds).FirstOrDefault();
        if (next.Entry is null)
        {
            ImGui.TextDisabled("当前计划已没有后续技能。");
            return;
        }

        var remaining = next.Entry.TimeSeconds - engine.CurrentTime;
        var color = ImGui.ColorConvertU32ToFloat4(next.Skill.Color);
        ImGui.Text("下一项：");
        ImGui.SameLine();
        ImGui.TextColored(color, next.Skill.Name);
        ImGui.SameLine();
        ImGui.Text($"  {remaining:0.0} 秒后  ({FormatTime(next.Entry.TimeSeconds)})");
        if (!string.IsNullOrWhiteSpace(next.Entry.Note))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(next.Entry.Note);
        }
    }

    private void DrawTimeline()
    {
        var enabledSkills = plugin.Configuration.Skills.Where(x => x.Enabled).ToList();
        if (enabledSkills.Count == 0)
        {
            ImGui.TextDisabled("所有技能均已关闭，请在设置中至少启用一项。");
            return;
        }

        const float labelWidth = 78f;
        const float headerHeight = 24f;
        const float rowHeight = 27f;
        var size = new Vector2(Math.Max(100, ImGui.GetContentRegionAvail().X), headerHeight + enabledSkills.Count * rowHeight + 4);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##timelineCanvas", size);
        var draw = ImGui.GetWindowDrawList();
        var bg = ImGui.GetColorU32(new Vector4(0.06f, 0.07f, 0.09f, 0.92f));
        var grid = ImGui.GetColorU32(new Vector4(0.32f, 0.34f, 0.38f, 0.65f));
        var text = ImGui.GetColorU32(new Vector4(0.85f, 0.86f, 0.88f, 1f));
        var nowColor = ImGui.GetColorU32(new Vector4(1f, 0.35f, 0.25f, 1f));
        draw.AddRectFilled(origin, origin + size, bg, 5f);

        var startX = origin.X + labelWidth;
        var timelineWidth = size.X - labelWidth - 4;
        var pps = plugin.Configuration.PixelsPerSecond;
        var pastSeconds = plugin.Configuration.ShowPastEntries ? Math.Min(12f, timelineWidth / pps * 0.2f) : 0f;
        var nowX = startX + pastSeconds * pps;

        for (var second = -((int)pastSeconds / 5) * 5; second * pps < timelineWidth; second += 5)
        {
            var x = nowX + second * pps;
            if (x < startX || x > origin.X + size.X)
                continue;
            draw.AddLine(new Vector2(x, origin.Y + headerHeight), new Vector2(x, origin.Y + size.Y), grid, second % 10 == 0 ? 1.2f : 0.6f);
            draw.AddText(new Vector2(x + 2, origin.Y + 3), text, second == 0 ? "现在" : $"{second:+0;-0}");
        }

        for (var row = 0; row < enabledSkills.Count; row++)
        {
            var skill = enabledSkills[row];
            var y0 = origin.Y + headerHeight + row * rowHeight;
            var y1 = y0 + rowHeight;
            if (row % 2 == 0)
                draw.AddRectFilled(new Vector2(origin.X, y0), new Vector2(origin.X + size.X, y1), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.025f)));
            draw.AddText(new Vector2(origin.X + 6, y0 + 5), skill.Color, skill.Name);
            draw.AddLine(new Vector2(origin.X, y1), new Vector2(origin.X + size.X, y1), grid, 0.5f);

            foreach (var entry in plugin.Configuration.Entries.Where(x => x.Enabled && x.SkillKey == skill.Key))
            {
                var delta = entry.TimeSeconds - engine.CurrentTime;
                var x = nowX + delta * pps;
                if (x < startX || x > origin.X + size.X - 3)
                    continue;
                draw.AddRectFilled(new Vector2(x - 3, y0 + 3), new Vector2(x + 3, y1 - 3), skill.Color, 2f);
                if (delta >= 0 && delta < 8f)
                    draw.AddText(new Vector2(x + 5, y0 + 5), skill.Color, $"{delta:0.0}s");
            }
        }
        draw.AddLine(new Vector2(nowX, origin.Y), new Vector2(nowX, origin.Y + size.Y), nowColor, 2f);
    }

    private void DrawUpcoming()
    {
        ImGui.Spacing();
        ImGui.Text("即将到来");
        var rows = engine.Upcoming(engine.CurrentTime, engine.CurrentTime + plugin.Configuration.VisibleFutureSeconds).Take(12).ToList();
        if (rows.Count == 0)
        {
            ImGui.TextDisabled($"未来 {plugin.Configuration.VisibleFutureSeconds:0} 秒没有已启用项目。");
            return;
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("upcomingTable", 4, flags))
            return;
        ImGui.TableSetupColumn("倒计时", ImGuiTableColumnFlags.WidthFixed, 72);
        ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, 62);
        ImGui.TableSetupColumn("技能", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("备注");
        ImGui.TableHeadersRow();
        foreach (var row in rows)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0); ImGui.Text($"{row.Entry.TimeSeconds - engine.CurrentTime:0.0}s");
            ImGui.TableSetColumnIndex(1); ImGui.Text(FormatTime(row.Entry.TimeSeconds));
            ImGui.TableSetColumnIndex(2); ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(row.Skill.Color), row.Skill.Name);
            ImGui.TableSetColumnIndex(3); ImGui.TextUnformatted(row.Entry.Note);
        }
        ImGui.EndTable();
    }

    internal static string FormatTime(float seconds)
        => $"{(int)seconds / 60:00}:{seconds % 60:00.0}";

    private static string StateText(TimelineRunState state) => state switch
    {
        TimelineRunState.Running => "运行中",
        TimelineRunState.Paused => "已暂停",
        TimelineRunState.Complete => "已结束",
        _ => "待机",
    };
}
