using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.IntralaminarThalamus, "Izhikevich", "STDP", "Intralaminar thalamic nuclei broadcast arousal and salience to cortex/striatum through matrix-like relay cells, requiring burst-capable thalamic dynamics.", new DelayWindow(5, 20)));

