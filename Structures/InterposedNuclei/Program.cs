using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.InterposedNuclei, "Izhikevich", "STDP", "Intermediate deep-cerebellar populations integrate limb-error signals and Purkinje inhibition, then drive red-nucleus and motor-thalamic correction.", new DelayWindow(3, 9)));
