using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.A1, "Izhikevich", "STDP", "A1 onset/sustained channels require adaptable spike-frequency response; Izhikevich neurons represent temporal precision and adaptation.", new DelayWindow(5, 20)));

