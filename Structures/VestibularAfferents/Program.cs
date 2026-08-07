using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(
    StructureId.VestibularAfferents,
    "Izhikevich",
    "STDP",
    "Primary semicircular-canal and otolith afferents encode angular velocity and linear acceleration before vestibular-nuclear integration.",
    new DelayWindow(1, 5)));
