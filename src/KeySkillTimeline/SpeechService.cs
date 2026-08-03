using System.Globalization;
using System.Speech.Synthesis;
using Dalamud.Plugin.Services;

namespace KeySkillTimeline;

public sealed class SpeechService : IDisposable
{
    private readonly IPluginLog log;
    private readonly SpeechSynthesizer synthesizer = new();
    private bool available = true;

    public SpeechService(IPluginLog log)
    {
        this.log = log;
        try
        {
            var chinese = synthesizer.GetInstalledVoices(new CultureInfo("zh-CN"))
                .FirstOrDefault(x => x.Enabled);
            if (chinese is null)
            {
                available = false;
                log.Warning("No zh-CN Windows speech voice is installed.");
            }
            else
            {
                synthesizer.SelectVoice(chinese.VoiceInfo.Name);
            }
        }
        catch (Exception ex)
        {
            available = false;
            log.Warning(ex, "No usable Windows speech voice was found.");
        }
    }

    public bool Available => available;

    public void Speak(string text, int rate)
    {
        if (!available || string.IsNullOrWhiteSpace(text))
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
            synthesizer.SpeakAsyncCancelAll();
            synthesizer.Dispose();
        }
        catch
        {
            // Best effort during plugin unload.
        }
    }
}
