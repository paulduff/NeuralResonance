using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.DorsomedialPrefrontalCortex, "Izhikevich", "DopamineModulatedSTDP+SynapticTaggingCapture", "Dorsomedial prefrontal cortex monitors inferred goals, uncertainty, social context, and high-level action policy.", new DelayWindow(6, 16)));
