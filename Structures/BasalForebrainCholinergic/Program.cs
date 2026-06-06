using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.BasalForebrain, "LIF", "HomeostaticGain", "Cholinergic transients and attention gating are modeled as burst-capable modulatory LIF outputs.", new DelayWindow(0, 0)));

