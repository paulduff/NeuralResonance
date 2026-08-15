using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.FacialMotorNucleus, "Izhikevich", "STDP", "Facial motor-neuron populations transform pontine and cortical premotor spikes into cholinergic facial and branchial motor efference.", new DelayWindow(2, 8)));
