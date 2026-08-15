using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.DiagonalBandNucleus, "Izhikevich", "STDP", "Cholinergic and GABAergic diagonal-band populations coordinate septo-hippocampal, entorhinal, and olfactory timing.", new DelayWindow(5, 13)));
