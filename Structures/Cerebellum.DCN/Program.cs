using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.DeepCerebellarNuclei, "Izhikevich", "STDP", "DCN rebound output after Purkinje inhibition is naturally represented by rebound-capable Izhikevich neurons.", new DelayWindow(10, 15)));

