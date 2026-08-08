using System.Globalization;
using System.Speech.Synthesis;
using Dalamud.Plugin.Services;

namespace KeySkillTimeline;

public sealed class SpeechService : IDisposable
{
    private readonly IPluginLog log;
    private SpeechSynthesizer? synthesizer;
    private bool available;

    public SpeechService(IPluginLog log)
    {
        this.log = log;
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
                available = true;
            }
        }
        catch (Exception ex)
        {
            available = false;
            synthesizer = null;
            log.Warning(ex, "Windows speech is unavailable; voice reminders are disabled, but the plugin will continue loading.");
        }
    }

    public bool Available => available;

    public void Speak(string text, int rate)
    {
        if (!available || synthesizer is null || string.IsNullOrWhiteSpace(text))
            return;
        try
        {
            synthesizer.Rate = Math.Clamp(rate, -10, 10);
            synthesizer.SpeakAsyncCancelAll();
            synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            available = false;
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
