using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.TemporalPole, "Izhikevich", "DopamineModulatedSTDP+SynapticTaggingCapture", "The temporal pole binds person, social, emotional, and semantic context into multimodal conceptual representations.", new DelayWindow(6, 15)));
