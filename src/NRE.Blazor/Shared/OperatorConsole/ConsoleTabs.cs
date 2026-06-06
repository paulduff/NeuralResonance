namespace NRE.Blazor.Shared.OperatorConsole;

public static class ConsoleTabs
{
    public const string Stimulus = "Stim";
    public const string Modulators = "Mods";
    public const string Pons = "Pons";
    public const string Scenes = "Scenes";
    public const string Systems = "Systems";
    public const string Voice = "Voice";
    public const string Peer = "Peer";
    public const string View = "View";
    public const string Monitor = "Monitor";

    public static readonly IReadOnlyList<ConsoleTabDefinition> All =
    [
        new(Stimulus, "Stimulus"),
        new(Modulators, "Mods"),
        new(Pons, "Pons"),
        new(Scenes, "Scenes"),
        new(Systems, "Systems"),
        new(Voice, "Voice"),
        new(Peer, "Peer"),
        new(View, "View"),
        new(Monitor, "Monitor")
    ];
}

public sealed record ConsoleTabDefinition(string Key, string Title);
