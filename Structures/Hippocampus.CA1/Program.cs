using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.CA1, "Izhikevich", "SynapticTaggingCapture", "CA1 comparator behavior uses temporally sensitive pyramidal firing and longer-timescale consolidation mechanisms.", new DelayWindow(3, 8)));

