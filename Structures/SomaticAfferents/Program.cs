using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(
    StructureId.SomaticAfferents,
    "Izhikevich",
    "STDP",
    "Primary somatic afferent neurons encode adaptive cutaneous pressure, onset, vibration, stretch, and high-threshold mechanical input before central interpretation.",
    new DelayWindow(1, 6)));
