namespace NRE.SimAvatar;

public sealed record AvatarAudioFrame(
    long Sequence,
    long CaptureTimestampMs,
    int SampleRate,
    int Channels,
    int SamplesPerChannel,
    byte[] Pcm16Le)
{
    public const int MinimumSampleRate = 8000;
    public const int MaximumSampleRate = 48000;
    public const int MaximumSamplesPerChannel = 4096;

    public int RequiredBytes => checked(SamplesPerChannel * Channels * sizeof(short));

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Pcm16Le);
        if (SampleRate is < MinimumSampleRate or > MaximumSampleRate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SampleRate),
                $"Audio sample rate must be between {MinimumSampleRate} and {MaximumSampleRate} Hz.");
        }

        if (Channels is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(Channels), "Audio channel count must be one or two.");
        }

        if (SamplesPerChannel is <= 0 or > MaximumSamplesPerChannel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SamplesPerChannel),
                $"Audio frame must contain between 1 and {MaximumSamplesPerChannel} samples per channel.");
        }

        if (Pcm16Le.Length != RequiredBytes)
        {
            throw new ArgumentException(
                $"PCM buffer must contain exactly {RequiredBytes} bytes.",
                nameof(Pcm16Le));
        }
    }
}
