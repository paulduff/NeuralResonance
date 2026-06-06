using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.LocusCoeruleus, "LIF", "HomeostaticGain", "LC tonic/phasic gain modulation can be modeled with LIF populations projecting neuromodulatory state changes.", new DelayWindow(0, 0)));

