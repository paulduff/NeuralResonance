using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.PosteriorCingulate, "Izhikevich", "STDP+SynapticTaggingCapture", "Posterior cingulate integrates default-mode and autobiographical context via recurrent associative populations.", new DelayWindow(5, 14)));

