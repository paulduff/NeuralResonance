using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.ParaventricularHypothalamicNucleus, "Izhikevich", "STDP", "Autonomic command neurons integrate visceral, circadian, and threat-related activity for brainstem regulation.", new DelayWindow(5, 14)));
