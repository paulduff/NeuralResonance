using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Thalamus, "Izhikevich", "STDP", "Thalamic tonic-burst transitions are naturally represented with Izhikevich parameters and relay/matrix subpopulations.", new DelayWindow(8, 12)));

