using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.DentateNucleus, "Izhikevich", "STDP", "Lateral deep-cerebellar populations integrate Purkinje inhibition and mossy/climbing collaterals, then relay planning correction to motor thalamus and prefrontal cortex.", new DelayWindow(3, 9)));
