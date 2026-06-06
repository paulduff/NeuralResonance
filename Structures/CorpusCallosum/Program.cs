using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.CorpusCallosum, "LIF", "STDP", "Corpus callosum fibers are glutamatergic interhemispheric projections; LIF is suitable for large myelinated relay axons with sparse local integration.", new DelayWindow(4, 12)));

