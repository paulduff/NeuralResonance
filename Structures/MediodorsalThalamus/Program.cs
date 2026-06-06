using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.MediodorsalThalamus, "Izhikevich", "STDP", "Mediodorsal thalamus supports prefrontal gating with thalamocortical burst/tonic transitions, well modeled by Izhikevich dynamics.", new DelayWindow(5, 20)));

