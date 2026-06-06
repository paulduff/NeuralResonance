using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.VestibularNuclei, "LIF", "STDP", "Vestibular nuclei require continuous head-motion integration and rapid reflex transfer, favoring stable LIF firing with timing-dependent adaptation for vestibulo-ocular calibration.", new DelayWindow(1, 5)));

