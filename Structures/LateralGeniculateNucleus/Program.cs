using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.LateralGeniculateNucleus, "Izhikevich", "STDP", "The lateral geniculate nucleus preserves retinal topology while neuronal tonic-burst state gates visual transmission to V1.", new DelayWindow(4, 8)));
