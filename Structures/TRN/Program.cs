using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Trn, "LIF", "STDP", "TRN is predominantly inhibitory and fast-gating; LIF captures efficient GABAergic gating and spotlight competition.", new DelayWindow(0, 0)));

