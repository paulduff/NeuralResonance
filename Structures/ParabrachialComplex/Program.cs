using NeuralResonanceEngine.Protocol;

StructureHostApplication.Run(args, new StructureProfile(StructureId.ParabrachialComplex, "Izhikevich", "STDP", "Parabrachial populations integrate visceral, respiratory, gustatory, thermal, and nociceptive spikes for insular, amygdala, hypothalamic, and thalamic relay.", new DelayWindow(3, 10)));
