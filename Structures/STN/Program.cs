using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Stn, "Izhikevich", "STDP", "STN rebound bursts and hyperdirect stopping signals need burst-capable nonlinear dynamics.", new DelayWindow(8, 12)));

