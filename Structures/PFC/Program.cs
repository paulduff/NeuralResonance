using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.Pfc, "Izhikevich", "DopamineModulatedSTDP+SynapticTaggingCapture", "Persistent activity, D1/D2 gating, and flexible control are captured with recurrent Izhikevich cells plus dopamine-modulated learning.", new DelayWindow(5, 20)));

