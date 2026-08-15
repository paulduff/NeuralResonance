using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.MedialGeniculateNucleus, "Izhikevich", "STDP", "The medial geniculate nucleus preserves auditory channels and gates inferior-colliculus input into primary auditory cortex.", new DelayWindow(5, 10)));
