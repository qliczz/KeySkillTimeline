using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace KeySkillTimeline;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private string importExportStatus = string.Empty;
    private string selectedSkillKey = "temperance";

    public ConfigWindow(Plugin plugin)
        : base("关键技能时间轴设置##KeySkillTimelineConfig")
    {
        this.plugin = plugin;
        configuration = plugin.Configuration;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("settingsTabs"))
            return;
        if (ImGui.BeginTabItem("通用"))
        {
            DrawGeneral();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("技能"))
        {
            DrawSkills();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("计划编辑"))
        {
            DrawPlanEditor();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("导入 / 导出"))
        {
            DrawImportExport();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawGeneral()
    {
        var changed = false;
        changed |= EditCheckbox("启用提醒", configuration.Enabled, x => configuration.Enabled = x);
        changed |= EditCheckbox("进入战斗时自动开始", configuration.AutoStart, x => configuration.AutoStart = x);
        changed |= EditCheckbox("进入战斗时自动打开时间轴", configuration.AutoOpen, x => configuration.AutoOpen = x);
        changed |= EditCheckbox("仅在妖星乱舞绝境战生效", configuration.OnlyInTargetTerritory, x => configuration.OnlyInTargetTerritory = x);
        changed |= EditCheckbox("仅在白魔法师生效", configuration.OnlyOnWhiteMage, x => configuration.OnlyOnWhiteMage = x);
        ImGui.Separator();
        ImGui.TextUnformatted("悬浮时间轴");
        changed |= EditCheckbox("启用紧凑悬浮窗", configuration.ShowOverlay, plugin.SetOverlayOpen);
        changed |= EditCheckbox("仅在时间轴运行时显示", configuration.OverlayOnlyWhileRunning, x => configuration.OverlayOnlyWhileRunning = x);
        changed |= EditCheckbox("仅在副本与职业匹配时显示", configuration.OverlayOnlyWhenApplicable, x => configuration.OverlayOnlyWhenApplicable = x);
        changed |= EditCheckbox("锁定悬浮窗位置", configuration.OverlayLocked, x => configuration.OverlayLocked = x);
        changed |= EditCheckbox("鼠标穿透（需用 /kst unlock 解锁）", configuration.OverlayClickThrough, x => configuration.OverlayClickThrough = x);
        ImGui.TextDisabled("悬浮窗不显示标题栏；解锁后可拖动窗口空白区域。文字使用真实像素字号，不做模糊缩放。");
        if (ImGui.Button(plugin.IsOverlayVisible ? "隐藏悬浮窗" : "立即显示悬浮窗"))
        {
            if (plugin.IsOverlayVisible)
                plugin.SetOverlayOpen(false);
            else
                plugin.UnlockOverlay();
        }
        ImGui.SameLine();
        ImGui.TextColored(
            plugin.IsOverlayVisible ? new Vector4(0.35f, 0.9f, 0.48f, 1f) : new Vector4(1f, 0.68f, 0.25f, 1f),
            plugin.OverlayVisibilityStatus);

        var overlayOpacity = configuration.OverlayBackgroundOpacity;
        if (ImGui.SliderFloat("悬浮窗背景透明度", ref overlayOpacity, 0.05f, 1f, "%.2f"))
        {
            configuration.OverlayBackgroundOpacity = overlayOpacity;
            changed = true;
        }
        var overlayWidth = configuration.OverlayWidth;
        if (ImGui.SliderFloat("悬浮窗宽度", ref overlayWidth, 220f, 900f, "%.0f px"))
        {
            configuration.OverlayWidth = overlayWidth;
            changed = true;
        }
        var overlayHeight = configuration.OverlayHeight;
        if (ImGui.SliderFloat("悬浮窗高度", ref overlayHeight, 70f, 300f, "%.0f px"))
        {
            configuration.OverlayHeight = overlayHeight;
            changed = true;
        }
        var overlayFontSize = configuration.OverlayFontSizePx;
        if (ImGui.SliderInt("悬浮窗文字字号", ref overlayFontSize, 14, 48, "%d px"))
        {
            configuration.OverlayFontSizePx = overlayFontSize;
            changed = true;
        }
        if (ImGui.Button("恢复悬浮窗外观默认值"))
        {
            configuration.OverlayBackgroundOpacity = 0.86f;
            configuration.OverlayWidth = 420f;
            configuration.OverlayHeight = 120f;
            configuration.OverlayFontSizePx = 24;
            configuration.OverlayLocked = false;
            configuration.OverlayClickThrough = false;
            changed = true;
        }
        ImGui.Separator();
        changed |= EditCheckbox("显示大字视觉提醒", configuration.EnableVisualBanner, x => configuration.EnableVisualBanner = x);
        changed |= EditCheckbox("显示 Dalamud 桌面通知", configuration.EnableDalamudNotification, x => configuration.EnableDalamudNotification = x);
        changed |= EditCheckbox("中文语音提醒（优先 EdgeTTS）", configuration.EnableChineseTts, x => configuration.EnableChineseTts = x);
        ImGui.TextDisabled($"当前语音提供者：{plugin.Engine.SpeechProviderName}");
        if (!plugin.Engine.SpeechAvailable)
            ImGui.TextColored(new Vector4(1f, 0.55f, 0.3f, 1f), "当前 Windows 没有可用的语音引擎；视觉提醒不受影响。");
        changed |= EditCheckbox("依据首领读条自动校时", configuration.EnableAutoSync, x => configuration.EnableAutoSync = x);

        var future = configuration.VisibleFutureSeconds;
        if (ImGui.SliderFloat("即将到来列表范围（秒）", ref future, 20f, 180f, "%.0f"))
        {
            configuration.VisibleFutureSeconds = future;
            changed = true;
        }
        var zoom = configuration.PixelsPerSecond;
        if (ImGui.SliderFloat("时间轴缩放（像素/秒）", ref zoom, 3f, 22f, "%.1f"))
        {
            configuration.PixelsPerSecond = zoom;
            changed = true;
        }
        var banner = configuration.BannerSeconds;
        if (ImGui.SliderFloat("提醒显示时长（秒）", ref banner, 1f, 10f, "%.1f"))
        {
            configuration.BannerSeconds = banner;
            changed = true;
        }
        var reset = configuration.ResetAfterOutOfCombatSeconds;
        if (ImGui.SliderFloat("脱战后重置等待（秒）", ref reset, 5f, 60f, "%.0f"))
        {
            configuration.ResetAfterOutOfCombatSeconds = reset;
            changed = true;
        }
        var syncWindow = configuration.SyncWindowSeconds;
        if (ImGui.SliderFloat("自动校时匹配窗口（秒）", ref syncWindow, 5f, 45f, "%.0f"))
        {
            configuration.SyncWindowSeconds = syncWindow;
            changed = true;
        }
        var rate = configuration.TtsRate;
        if (ImGui.SliderInt("语音速度", ref rate, -5, 5))
        {
            configuration.TtsRate = rate;
            changed = true;
        }
        changed |= EditCheckbox("时间轴显示少量已过去项目", configuration.ShowPastEntries, x => configuration.ShowPastEntries = x);
        if (changed)
            configuration.Save();
    }

    private void DrawSkills()
    {
        ImGui.TextWrapped("每项技能都可以独立开关、修改显示名、颜色和提前提醒秒数。默认提前 4 秒。");
        if (!ImGui.BeginTable("skillsTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;
        ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, 48);
        ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("颜色", ImGuiTableColumnFlags.WidthFixed, 85);
        ImGui.TableSetupColumn("提前秒数", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("时间点", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("测试", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableHeadersRow();
        var changed = false;
        foreach (var skill in configuration.Skills)
        {
            ImGui.PushID(skill.Key);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            var enabled = skill.Enabled;
            if (ImGui.Checkbox("##enabled", ref enabled)) { skill.Enabled = enabled; changed = true; }
            ImGui.TableSetColumnIndex(1);
            var name = skill.Name;
            if (ImGui.InputText("##name", ref name, 32)) { skill.Name = name; changed = true; }
            ImGui.TableSetColumnIndex(2);
            var color = ImGui.ColorConvertU32ToFloat4(skill.Color);
            if (ImGui.ColorEdit4("##color", ref color, ImGuiColorEditFlags.NoInputs)) { skill.Color = ImGui.ColorConvertFloat4ToU32(color); changed = true; }
            ImGui.TableSetColumnIndex(3);
            var lead = skill.LeadSeconds;
            if (ImGui.DragFloat("##lead", ref lead, 0.1f, 0f, 30f, "%.1f")) { skill.LeadSeconds = lead; changed = true; }
            ImGui.TableSetColumnIndex(4);
            ImGui.Text(configuration.Entries.Count(x => x.SkillKey == skill.Key).ToString());
            ImGui.TableSetColumnIndex(5);
            if (ImGui.SmallButton("播放")) plugin.Engine.TestReminder(skill.Key);
            ImGui.PopID();
        }
        ImGui.EndTable();
        if (changed)
            configuration.Save();
    }

    private void DrawPlanEditor()
    {
        ImGui.TextWrapped($"基准：{DancingMadPreset.Source}。时间为战斗开始后的相对秒数；可以编辑、禁用、新增或删除。");
        if (ImGui.Button("新增时间点"))
        {
            configuration.Entries.Add(new TimelineEntry { SkillKey = selectedSkillKey, TimeSeconds = plugin.Engine.CurrentTime });
            configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("按时间排序")) configuration.Save();

        string? removeId = null;
        var changed = false;
        if (ImGui.BeginTable("planTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, -1)))
        {
            ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, 88);
            ImGui.TableSetupColumn("技能", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("提前", ImGuiTableColumnFlags.WidthFixed, 58);
            ImGui.TableSetupColumn("阶段", ImGuiTableColumnFlags.WidthFixed, 48);
            ImGui.TableSetupColumn("备注");
            ImGui.TableSetupColumn("删除", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableHeadersRow();
            foreach (var entry in configuration.Entries)
            {
                ImGui.PushID(entry.Id);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                var enabled = entry.Enabled;
                if (ImGui.Checkbox("##entryEnabled", ref enabled)) { entry.Enabled = enabled; changed = true; }
                ImGui.TableSetColumnIndex(1);
                var time = entry.TimeSeconds;
                if (ImGui.InputFloat("##time", ref time, 0.1f, 1f, "%.1f")) { entry.TimeSeconds = Math.Max(0, time); changed = true; }
                ImGui.TableSetColumnIndex(2);
                var currentSkill = configuration.Skills.FirstOrDefault(x => x.Key == entry.SkillKey);
                if (ImGui.BeginCombo("##skill", currentSkill?.Name ?? entry.SkillKey))
                {
                    foreach (var skill in configuration.Skills)
                    {
                        if (ImGui.Selectable(skill.Name, entry.SkillKey == skill.Key))
                        {
                            entry.SkillKey = skill.Key;
                            selectedSkillKey = skill.Key;
                            changed = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.TableSetColumnIndex(3);
                ImGui.Text(currentSkill is null ? "-" : $"{currentSkill.LeadSeconds:0.0}s");
                ImGui.TableSetColumnIndex(4);
                ImGui.Text(PhaseAt(entry.TimeSeconds));
                ImGui.TableSetColumnIndex(5);
                var note = entry.Note;
                if (ImGui.InputText("##note", ref note, 100)) { entry.Note = note; changed = true; }
                ImGui.TableSetColumnIndex(6);
                if (ImGui.SmallButton("×")) removeId = entry.Id;
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (removeId is not null)
        {
            configuration.Entries.RemoveAll(x => x.Id == removeId);
            changed = true;
        }
        if (changed)
            configuration.Save();
    }

    private void DrawImportExport()
    {
        ImGui.TextWrapped("导出文件位于插件配置目录。导入会替换当前技能与时间点，但通用显示设置保持不变。建议先导出备份。");
        if (ImGui.Button("导出 JSON"))
            importExportStatus = plugin.ExportPlan();
        ImGui.SameLine();
        if (ImGui.Button("从 JSON 导入"))
            importExportStatus = plugin.ImportPlan();
        ImGui.SameLine();
        if (ImGui.Button("恢复 FFLogs 默认计划"))
        {
            configuration.ResetPlan();
            importExportStatus = "已恢复默认计划。";
        }
        if (!string.IsNullOrWhiteSpace(importExportStatus))
        {
            ImGui.Spacing();
            ImGui.TextWrapped(importExportStatus);
        }
        ImGui.Separator();
        ImGui.TextWrapped("自动校时只接受当前预测时间附近的首领读条锚点，避免同一 Action ID 在不同阶段出现时跳错。若队伍打法造成时间明显偏离，可用主窗口的 ±1 秒按钮调整，或重置后重新开始。");
    }

    private static bool EditCheckbox(string label, bool current, Action<bool> setter)
    {
        var value = current;
        if (!ImGui.Checkbox(label, ref value))
            return false;
        setter(value);
        return true;
    }

    private static string PhaseAt(float time)
        => DancingMadPreset.Phases.LastOrDefault(x => x.TimeSeconds <= time)?.Name ?? "P1";
}
