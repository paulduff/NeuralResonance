using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(
    StructureId.ProprioceptiveAfferents,
    "Izhikevich",
    "STDP",
    "Primary muscle, tendon, and joint afferents encode body-local motion and its temporal change before central interpretation.",
    new DelayWindow(1, 6)));
