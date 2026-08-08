using Dalamud.Game.Command;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;

namespace KeySkillTimeline;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/kst";
    private const string ExportFileName = "DancingMad_WHm_KeySkillPlan.json";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("KeySkillTimeline");
    private readonly SpeechService speech;
    private readonly MainWindow mainWindow;
    private readonly OverlayWindow overlayWindow;
    private readonly ConfigWindow configWindow;
    private IFontHandle? overlayFont;
    private int overlayFontSizePx;

    public Configuration Configuration { get; }
    public TimelineEngine Engine { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);
        speech = new SpeechService(PluginInterface, Log);
        Engine = new TimelineEngine(Configuration, ClientState, Condition, PlayerState, ObjectTable, NotificationManager, Log, speech);
        mainWindow = new MainWindow(this, Engine);
        overlayWindow = new OverlayWindow(this, Engine) { IsOpen = Configuration.ShowOverlay };
        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(overlayWindow);
        windowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开关键技能时间轴。参数：overlay / unlock / config / start / reset / pause / test / +1 / -1",
        });
        Framework.Update += Engine.Update;
        Engine.EncounterStarted += OnEncounterStarted;
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        Log.Information("Key Skill Timeline loaded with {Count} plan entries.", Configuration.Entries.Count);
    }

    public void Dispose()
    {
        Framework.Update -= Engine.Update;
        Engine.EncounterStarted -= OnEncounterStarted;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        overlayFont?.Dispose();
        speech.Dispose();
    }

    public void ToggleMainUi() => mainWindow.Toggle();
    public void ToggleConfigUi() => configWindow.Toggle();
    public bool IsOverlayVisible => overlayWindow.IsCurrentlyVisible;
    public string OverlayVisibilityStatus => overlayWindow.VisibilityStatus();

    public IDisposable PushOverlayFont()
    {
        var sizePx = Math.Clamp(Configuration.OverlayFontSizePx, 14, 48);
        if (overlayFont is null || overlayFontSizePx != sizePx)
        {
            overlayFont?.Dispose();
            overlayFontSizePx = sizePx;
            overlayFont = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                step => step.OnPreBuild(toolkit => toolkit.AddDalamudDefaultFont(sizePx)));
        }

        return overlayFont.Push();
    }

    public void SetOverlayOpen(bool open)
    {
        overlayWindow.IsOpen = open;
        Configuration.ShowOverlay = open;
        if (open)
            overlayWindow.ShowManually();
        else
            overlayWindow.HideManually();
        Configuration.Save();
        Log.Information("Timeline overlay manually set to {State}.", open ? "visible" : "hidden");
    }

    public void ToggleOverlayUi() => SetOverlayOpen(!overlayWindow.IsCurrentlyVisible);

    public void UnlockOverlay()
    {
        Configuration.OverlayLocked = false;
        Configuration.OverlayClickThrough = false;
        Configuration.ShowOverlay = true;
        Configuration.Save();
        overlayWindow.ShowManually();
        overlayWindow.BringToFront();
    }

    public string ExportPlan()
    {
        try
        {
            var path = Path.Combine(PluginInterface.GetPluginConfigDirectory(), ExportFileName);
            var model = new PlanFile
            {
                Encounter = "Dancing Mad (Ultimate)",
                Source = DancingMadPreset.Source,
                Skills = Configuration.Skills,
                Entries = Configuration.Entries,
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(model, Formatting.Indented));
            return $"已导出：{path}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Plan export failed.");
            return $"导出失败：{ex.Message}";
        }
    }

    public string ImportPlan()
    {
        try
        {
            var path = Path.Combine(PluginInterface.GetPluginConfigDirectory(), ExportFileName);
            if (!File.Exists(path))
                return $"未找到导入文件：{path}";
            var model = JsonConvert.DeserializeObject<PlanFile>(File.ReadAllText(path));
            if (model?.Skills is null || model.Entries is null || model.Skills.Count == 0)
                return "文件格式无效：缺少 skills 或 entries。";
            var knownKeys = model.Skills.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (model.Entries.Any(x => !knownKeys.Contains(x.SkillKey)))
                return "文件格式无效：存在找不到对应技能的时间点。";
            Configuration.Skills = model.Skills;
            Configuration.Entries = model.Entries;
            Configuration.Save();
            Engine.Reset();
            return $"已导入 {Configuration.Entries.Count} 个时间点：{path}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Plan import failed.");
            return $"导入失败：{ex.Message}";
        }
    }

    private void OnEncounterStarted()
    {
        if (Configuration.AutoOpen)
            mainWindow.IsOpen = true;
        if (Configuration.ShowOverlay)
            overlayWindow.IsOpen = true;
    }

    private void OnCommand(string _, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "config": ToggleConfigUi(); break;
            case "overlay": ToggleOverlayUi(); break;
            case "hud": ToggleOverlayUi(); break;
            case "unlock": UnlockOverlay(); break;
            case "start": Engine.Start(); mainWindow.IsOpen = true; break;
            case "reset": Engine.Reset(); break;
            case "pause": Engine.TogglePause(); break;
            case "test": Engine.TestReminder(); break;
            case "+1": Engine.SeekBy(1f); break;
            case "-1": Engine.SeekBy(-1f); break;
            default: ToggleMainUi(); break;
        }
    }

    private sealed class PlanFile
    {
        public int Version { get; set; } = 1;
        public string Encounter { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public List<SkillSetting> Skills { get; set; } = [];
        public List<TimelineEntry> Entries { get; set; } = [];
    }
}
