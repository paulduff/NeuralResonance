using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.FrontalEyeFields, "Izhikevich", "STDP", "Frontal eye fields convert visual priority and executive goals into covert attention shifts and saccade plans through parietal and collicular loops.", new DelayWindow(3, 10)));
