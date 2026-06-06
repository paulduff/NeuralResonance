using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Mt, "Izhikevich", "STDP", "Area MT motion-selective populations are modeled with fast spiking dynamics and feed-forward temporal integration.", new DelayWindow(3, 9)));

