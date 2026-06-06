using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Hypothalamus, "Izhikevich", "HomeostaticGain", "Hypothalamic nuclei encode slow homeostatic set-point control and burst-capable neuroendocrine outputs; Izhikevich captures mixed tonic-burst dynamics.", new DelayWindow(6, 22)));

