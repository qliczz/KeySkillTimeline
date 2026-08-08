using System.Globalization;
using System.Speech.Synthesis;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace KeySkillTimeline;

public sealed class SpeechService : IDisposable
{
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<string, object> edgeTts;
    private SpeechSynthesizer? synthesizer;
    private bool windowsAvailable;

    public SpeechService(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;
        edgeTts = pluginInterface.GetIpcSubscriber<string, object>("EdgeTTS.Speak");
        try
        {
            var candidate = new SpeechSynthesizer();
            var chinese = candidate.GetInstalledVoices(new CultureInfo("zh-CN"))
                .FirstOrDefault(x => x.Enabled);
            if (chinese is null)
            {
                candidate.Dispose();
                log.Warning("No zh-CN Windows speech voice is installed.");
            }
            else
            {
                candidate.SelectVoice(chinese.VoiceInfo.Name);
                synthesizer = candidate;
                windowsAvailable = true;
            }
        }
        catch (Exception ex)
        {
            windowsAvailable = false;
            synthesizer = null;
            log.Warning(ex, "Windows speech is unavailable; voice reminders are disabled, but the plugin will continue loading.");
        }
    }

    public bool Available => EdgeTtsAvailable || windowsAvailable;
    public string ProviderName => EdgeTtsAvailable ? "EdgeTTS.Dalamud" : windowsAvailable ? "Windows TTS" : "不可用";

    private bool EdgeTtsAvailable
    {
        get
        {
            try
            {
                return edgeTts.HasAction;
            }
            catch
            {
                return false;
            }
        }
    }

    public void Speak(string text, int rate)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            if (edgeTts.HasAction)
            {
                edgeTts.InvokeAction(text);
                return;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "EdgeTTS IPC failed; falling back to Windows speech.");
        }

        if (!windowsAvailable || synthesizer is null)
            return;
        try
        {
            synthesizer.Rate = Math.Clamp(rate, -10, 10);
            synthesizer.SpeakAsyncCancelAll();
            synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            windowsAvailable = false;
            log.Warning(ex, "TTS failed and has been disabled for this session.");
        }
    }

    public void Dispose()
    {
        try
        {
            synthesizer?.SpeakAsyncCancelAll();
            synthesizer?.Dispose();
        }
        catch
        {
            // Best effort during plugin unload.
        }
    }
}
