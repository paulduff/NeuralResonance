using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.SuperiorOlive, "LIF", "STDP", "Superior olive performs binaural coincidence and interaural timing/amplitude comparison, which is well approximated by low-latency LIF coincidence detectors with STDP refinement.", new DelayWindow(1, 4)));

