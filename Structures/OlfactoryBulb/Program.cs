using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.OlfactoryBulb, "Izhikevich", "STDP", "Bulbar gamma and lateral inhibition are reproduced with resonant Izhikevich-like excitatory/inhibitory dynamics.", new DelayWindow(0, 0)));

