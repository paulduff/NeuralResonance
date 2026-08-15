using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.HypoglossalNucleus, "Izhikevich", "STDP", "Hypoglossal motor-neuron populations integrate cortical, reticular, and solitary input into cholinergic tongue and oral motor efference.", new DelayWindow(2, 8)));
