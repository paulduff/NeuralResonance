using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.MotorThalamus, "Izhikevich", "STDP", "Motor thalamic relay nuclei support thalamocortical drive with tonic and burst-capable relay neurons.", new DelayWindow(4, 10)));

