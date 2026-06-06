using System.Reflection;
using System.Runtime.InteropServices;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.WpfEditor;

// Speech output (SAPI worker thread, dispatch-spike triggered phrases, language utterance memory).
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private void StartSpeechWorker()
    {
        if (_speechThread is not null)
        {
            return;
        }

        _speechThread = new Thread(SpeechWorkerLoop)
        {
            IsBackground = true,
            Name = "DNNE-SpeechWorker"
        };
        _speechThread.SetApartmentState(ApartmentState.STA);
        _speechThread.Start();
    }

    private void SpeechWorkerLoop()
    {
        object? voice = null;
        Type? voiceType = null;
        var loggedUnavailable = false;

        try
        {
            try
            {
                voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (voiceType is not null)
                {
                    voice = Activator.CreateInstance(voiceType);
                    if (voice is not null)
                    {
                        var initialRate = ConvertSpeechRatePercentToSapiRate(_speechRatePercent);
                        var initialVolume = Math.Clamp(_speechVolume, 0, 100);
                        voiceType.InvokeMember("Rate", BindingFlags.SetProperty, null, voice, new object[] { initialRate });
                        voiceType.InvokeMember("Volume", BindingFlags.SetProperty, null, voice, new object[] { initialVolume });
                    }
                }
            }
            catch (Exception ex)
            {
                loggedUnavailable = true;
                PostUi(() =>
                {
                    UpdateSpeechStatusText("Speech: unavailable");
                    AddOutputLog($"Speech output unavailable: {ex.Message}");
                });
            }

        while (!_workerCts.IsCancellationRequested)
        {
            string phrase;
            try
            {
                if (!_speechQueue.TryTake(out phrase!, 250))
                {
                    continue;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            if (!_speechOutputEnabled || string.IsNullOrWhiteSpace(phrase))
            {
                continue;
            }

            if (_isSimulationSleeping)
            {
                continue;
            }

            if (!TrySpeakTextViaSapi(voiceType, voice, phrase, out var error))
            {
                if (!loggedUnavailable)
                {
                    loggedUnavailable = true;
                    PostUi(() =>
                    {
                        UpdateSpeechStatusText("Speech: unavailable");
                        AddOutputLog($"Speech output warning: {error}");
                    });
                }

                continue;
            }

            loggedUnavailable = false;
            var preview = phrase.Length > 44 ? $"{phrase[..44]}..." : phrase;
            PostUi(() => UpdateSpeechStatusText($"Speech: {preview}"));
        }
        }
        finally
        {
            // Always release the COM object - including the SAPI mid-Speak shutdown
            // case where the loop body throws and the previous code skipped release.
            if (voice is not null && Marshal.IsComObject(voice))
            {
                try { Marshal.FinalReleaseComObject(voice); }
                catch { }
            }
        }
    }

    private bool TrySpeakTextViaSapi(Type? voiceType, object? voice, string phrase, out string error)
    {
        if (voiceType is null || voice is null)
        {
            error = "SAPI voice is not available on this machine.";
            return false;
        }

        try
        {
            // Keep speech behavior synchronized with UI controls.
            var rate = ConvertSpeechRatePercentToSapiRate(_speechRatePercent);
            var volume = Math.Clamp(_speechVolume, 0, 100);
            voiceType.InvokeMember("Rate", BindingFlags.SetProperty, null, voice, new object[] { rate });
            voiceType.InvokeMember("Volume", BindingFlags.SetProperty, null, voice, new object[] { volume });
            // Speak flags: 0 = synchronous/default behavior on speech thread.
            voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { phrase, 0 });
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void TryQueueSpeechFromLanguageDispatch(IReadOnlyList<DispatchSpikeTrace> dispatchSpikes)
    {
        if (!_speechOutputEnabled || _isSimulationSleeping || dispatchSpikes.Count == 0)
        {
            return;
        }

        var triggerMode = _speechTriggerMode;
        var selectedSpikes = triggerMode == SpeechTriggerMode.GlobalDispatch
            ? dispatchSpikes.ToList()
            : dispatchSpikes
                .Where(trace => IsLanguageStructure(trace.SourceStructure) || IsLanguageStructure(trace.TargetStructure))
                .ToList();
        if (selectedSpikes.Count < _speechMinDispatchSpikes)
        {
            return;
        }

        var latestDispatchMs = selectedSpikes.Max(s => s.WallClockUnixMs);
        if (latestDispatchMs <= _lastSpeechDispatchWallClockMs)
        {
            return;
        }

        if (_languageUtteranceSequence <= _lastSpokenLanguageUtteranceSequence)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastSpeechUtc) < SpeechCooldown)
        {
            return;
        }

        var phrase = BuildSpeechPhrase(selectedSpikes, now, triggerMode);
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return;
        }

        if (string.Equals(_lastSpokenPhrase, phrase, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastSpeechUtc) < SpeechDuplicateSuppression)
        {
            return;
        }

        _lastSpeechDispatchWallClockMs = latestDispatchMs;
        _lastSpeechUtc = now;
        _lastSpokenPhrase = phrase;
        _lastSpokenLanguageUtteranceSequence = _languageUtteranceSequence;
        if (!_speechQueue.TryAdd(phrase))
        {
            _speechQueue.TryTake(out _);
            _speechQueue.TryAdd(phrase);
        }
    }

    private string BuildSpeechPhrase(IReadOnlyList<DispatchSpikeTrace> selectedSpikes, DateTime nowUtc, SpeechTriggerMode triggerMode)
    {
        if (!string.IsNullOrWhiteSpace(_lastLanguageUtterance) &&
            (nowUtc - _lastLanguageUtteranceUtc) <= LanguageUtteranceRetention)
        {
            return _lastLanguageUtterance;
        }

        return triggerMode == SpeechTriggerMode.GlobalDispatch
            ? BuildGlobalDispatchSpeechPhrase(selectedSpikes)
            : BuildLanguageSpeechPhrase(selectedSpikes, nowUtc);
    }

    private string BuildLanguageSpeechPhrase(IReadOnlyList<DispatchSpikeTrace> languageSpikes, DateTime nowUtc)
    {
        // Only announce concrete trigger text, never inferred "structure active" phrases.
        return string.Empty;
    }

    private static string BuildGlobalDispatchSpeechPhrase(IReadOnlyList<DispatchSpikeTrace> dispatchSpikes)
    {
        // Global mode still requires explicit trigger text (for example user/microphone language input).
        return string.Empty;
    }

    private static bool IsLanguageStructure(string structureId)
    {
        if (string.IsNullOrWhiteSpace(structureId))
        {
            return false;
        }

        var normalized = structureId.Trim();
        if (normalized.Length > 2 && normalized[1] == '_' && (normalized[0] == 'L' || normalized[0] == 'R' || normalized[0] == 'M'))
        {
            normalized = normalized[2..];
        }

        return SpeechLanguageStructures.Contains(normalized);
    }

    private static void AccumulateStructureSpeechCount(Dictionary<string, int> structureCounts, string structureId)
    {
        if (string.IsNullOrWhiteSpace(structureId))
        {
            return;
        }

        var normalized = NormalizeStructureIdForSpeech(structureId);
        structureCounts[normalized] = structureCounts.TryGetValue(normalized, out var count)
            ? count + 1
            : 1;
    }

    private static string NormalizeStructureIdForSpeech(string structureId)
    {
        var normalized = structureId.Trim();
        if (normalized.Length > 2 &&
            normalized[1] == '_' &&
            (normalized[0] == 'L' || normalized[0] == 'R' || normalized[0] == 'M'))
        {
            normalized = normalized[2..];
        }

        return normalized.Replace('_', ' ');
    }

    private static int ConvertSpeechRatePercentToSapiRate(int percent)
    {
        var clampedPercent = Math.Clamp(percent, 50, 200);
        // Map UI range 50%-200% to SAPI's nominal -10..+10 rate range (100% => 0).
        return Math.Clamp((int)Math.Round((clampedPercent - 100) / 10.0), -10, 10);
    }

    private void RememberLanguageUtterance(string utterance, bool force)
    {
        var normalized = NormalizeSpeechUtterance(utterance);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!force)
        {
            if (string.Equals(_lastLanguageUtterance, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if ((now - _lastLanguageUtteranceUtc) < PassiveLanguageUtteranceUpdateCooldown)
            {
                return;
            }
        }

        _lastLanguageUtterance = normalized;
        _lastLanguageUtteranceUtc = now;
        _languageUtteranceSequence++;
    }

    private static string NormalizeSpeechUtterance(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return string.Empty;
        }

        var normalized = utterance.Trim();
        normalized = normalized.Replace('\r', ' ').Replace('\n', ' ');
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (normalized.Length > 120)
        {
            normalized = $"{normalized[..120]}";
        }

        return normalized;
    }
}
