using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Pons, "LIF", "STDP", "Pontine nuclei relay cortico-cerebellar and arousal pathways with high-throughput relay dynamics; LIF is appropriate for this feed-forward relay role.", new DelayWindow(4, 14)));

