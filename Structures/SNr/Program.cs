using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Snr, "LIF", "STDP", "SNr output neurons are tonically active inhibitory gates suited to LIF tonic firing.", new DelayWindow(8, 12)));

