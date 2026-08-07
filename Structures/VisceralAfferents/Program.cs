using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(
    StructureId.VisceralAfferents,
    "Izhikevich",
    "STDP",
    "Primary visceral chemoreceptor, baroreceptor, thermoreceptor, and osmoreceptor afferents encode physical physiology before brainstem integration.",
    new DelayWindow(2, 8)));
