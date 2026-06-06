namespace NRE.Contracts.Voice;

/// <summary>
/// Output utterance produced by the engine for client-side speech synthesis.
/// Text is the primary speakable surface, Gloss is an optional English gloss,
/// and Phonemes carry the underlying phonological plan.
/// </summary>
public sealed record VoiceUtteranceDto(
    long StepIndex,
    string Text,
    float Rate,
    float Pitch,
    float Volume,
    string? Gloss = null,
    string[]? Phonemes = null);

/// <summary>
/// Loop-closure payload: the browser posts back whatever it spoke so the engine can inject
/// a reafferent auditory drive ("hears itself") without streaming audio.
/// </summary>
public sealed record VoiceReafferenceRequest(
    string Text,
    float Rate,
    float Pitch,
    float Volume,
    float HoldSeconds = 0.45f,
    string[]? Phonemes = null,
    string? Gloss = null);
