using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.SpinalCordMotor, "Izhikevich", "STDP", "Spinal motor pools require rapid premotor integration and reflex-loop timing, which is represented by Izhikevich spiking with timing-dependent strengthening of effective motor synergies.", new DelayWindow(1, 4)));

