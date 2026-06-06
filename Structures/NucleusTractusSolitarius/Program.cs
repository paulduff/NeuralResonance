using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.NucleusTractusSolitarius, "LIF", "HomeostaticGain", "Nucleus tractus solitarius encodes visceral afferents and autonomic set-point signals, so homeostatic gain modulation with low-latency LIF neurons captures baroreflex-like control.", new DelayWindow(2, 8)));

