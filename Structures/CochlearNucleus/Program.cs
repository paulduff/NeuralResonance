using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.CochlearNucleus, "Izhikevich", "STDP", "Cochlear nucleus populations mix onset, chopper, and sustained response motifs; Izhikevich dynamics capture this heterogeneity while preserving millisecond timing plasticity.", new DelayWindow(1, 5)));

