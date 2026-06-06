using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.CA3, "Izhikevich", "MossyFiberLTP", "CA3 autoassociation and recurrent bursting require nonlinear spiking with recurrent collateral support.", new DelayWindow(2, 5)));

