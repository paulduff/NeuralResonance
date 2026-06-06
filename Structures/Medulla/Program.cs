using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Medulla, "LIF", "HomeostaticGain", "Medullary reticular and autonomic circuits regulate rhythmic visceral drive; LIF plus homeostatic gain captures persistent baseline excitability control.", new DelayWindow(4, 16)));

