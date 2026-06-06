using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Striatum, "LIF", "DopamineModulatedSTDP", "MSN up/down state dynamics and action gating are represented with slowly integrating inhibitory LIF populations under dopamine control.", new DelayWindow(8, 12)));

