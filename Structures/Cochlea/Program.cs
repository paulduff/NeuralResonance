using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Cochlea, "LIF", "STDP", "Cochlear transduction emphasizes phase-locked spike timing across tonotopic fibers, making fast LIF membranes with timing-sensitive STDP appropriate for auditory nerve-like drive.", new DelayWindow(1, 3)));

