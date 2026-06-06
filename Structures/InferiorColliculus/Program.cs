using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.InferiorColliculus, "Izhikevich", "STDP", "Inferior colliculus integrates ascending auditory streams with multimodal orienting inputs; Izhikevich neurons preserve burst/tonic transitions needed for salience coding.", new DelayWindow(2, 6)));

