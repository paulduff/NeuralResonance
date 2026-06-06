using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.EntorhinalCortex, "Izhikevich", "STDP", "Grid-like phase responses and mixed stellate/pyramidal dynamics are approximated with Izhikevich conductances.", new DelayWindow(3, 8)));

