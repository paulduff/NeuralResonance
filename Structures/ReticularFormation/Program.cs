using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.ReticularFormation, "Izhikevich", "HomeostaticGain", "Reticular formation mixes arousal, posture, and premotor gating with broad recurrent motifs; Izhikevich neurons with homeostatic gain reflect state-dependent tonic-burst transitions.", new DelayWindow(2, 10)));

