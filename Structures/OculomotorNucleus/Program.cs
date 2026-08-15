using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.OculomotorNucleus, "Izhikevich", "STDP", "Oculomotor populations integrate collicular, vestibular, and gaze-command spikes into cholinergic extraocular motor efference.", new DelayWindow(2, 8)));
