using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.SecondarySomatosensoryCortex, "LIF", "STDP", "Secondary somatosensory cortex integrates bilateral somatic, texture, nociceptive, and interoceptive activity from S1, thalamic, and insular pathways.", new DelayWindow(4, 10)));
