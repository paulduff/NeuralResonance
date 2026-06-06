using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.GPi, "LIF", "STDP", "Internal globus pallidus is the major inhibitory basal ganglia output nucleus; tonic inhibitory output is captured with LIF dynamics.", new DelayWindow(5, 14)));

