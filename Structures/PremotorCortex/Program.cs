using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.PremotorCortex, "Izhikevich", "DopamineModulatedSTDP", "Premotor planning requires persistent preparatory activity gated by dopaminergic reinforcement of action policies.", new DelayWindow(5, 15)));

