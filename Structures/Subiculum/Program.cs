using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Subiculum, "LIF", "STDP", "Subiculum relay and place-related burst output can be abstracted as thresholded relay populations with adaptive firing.", new DelayWindow(3, 8)));

