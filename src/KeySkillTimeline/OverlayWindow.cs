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
        var height = config.OverlayHeight;
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
        var next = engine.Upcoming(engine.CurrentTime - 0.01f, DancingMadPreset.DurationSeconds).FirstOrDefault();
        ImGui.SetWindowFontScale(plugin.Configuration.OverlayContentScale);
        if (next.Entry is null)
        {
            DrawCenteredLine("没有后续技能", new Vector4(0.65f, 0.67f, 0.72f, 1f), 0.5f);
            return;
        }

        var remaining = Math.Max(0f, next.Entry.TimeSeconds - engine.CurrentTime);
        var color = ImGui.ColorConvertU32ToFloat4(next.Skill.Color);
        DrawCenteredLine(next.Skill.Name, color, 0.18f);
        DrawCenteredLine($"{remaining:0.0} 秒", new Vector4(1f, 1f, 1f, 1f), 0.68f);
    }

    private static void DrawCenteredLine(string text, Vector4 color, float verticalRatio)
    {
        var available = ImGui.GetContentRegionAvail();
        var size = ImGui.CalcTextSize(text);
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetCursorPosX() + (available.X - size.X) / 2f));
        ImGui.SetCursorPosY(Math.Max(ImGui.GetCursorPosY(), ImGui.GetWindowHeight() * verticalRatio));
        ImGui.TextColored(color, text);
    }
}
