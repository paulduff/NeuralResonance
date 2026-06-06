using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.M1, "Izhikevich", "STDP", "Primary motor cortex M1, precentral gyrus/Brodmann area 4, carries a contralateral motor homunculus; layer V corticospinal output uses beta-rhythm motor coding with recurrent excitatory dynamics and inhibitory balance.", new DelayWindow(10, 15)));

