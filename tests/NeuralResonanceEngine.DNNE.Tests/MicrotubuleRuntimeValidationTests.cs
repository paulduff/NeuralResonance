using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MicrotubuleRuntimeValidationTests
{
    [Fact]
    public void Disabled_Microtubule_Mode_Is_Neutral_For_Plasticity_And_Integration()
    {
        var previous = Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", "off");
            var state = global::IntracellularMicrotubuleState.Create(12);
            var neuromod = new NeuromodState
            {
                DopamineLevel = 1f,
                AcetylcholineLevel = 1f,
                NorepinephrineLevel = 1f,
                SerotoninLevel = 0.25f
            };

            state.ObserveSynapticInput(NTEnum.GLUTAMATE, 5f, neuromod, 20.0);
            state.Advance(20.0, neuromod, excitatoryCurrent: 80.0, netDrive: 80.0, activityTrace: 1f, spiked: true);

            Assert.Equal("off", state.Mode);
            Assert.False(state.Enabled);
            Assert.False(state.ExperimentalQuantumTermsEnabled);
            Assert.Equal(1.0, state.PlasticitySupport, 6);
            Assert.Equal(1.0, state.TracePersistenceSupport, 6);
            Assert.Equal(1.0, state.IntegrationGain, 6);
            Assert.Equal(0.0, state.OpticalCollectiveBias, 6);
            Assert.Equal(0.0, state.RadicalPairSensitivity, 6);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", previous);
        }
    }

    [Fact]
    public void Classical_Mode_Keeps_Experimental_Quantum_Terms_Disabled()
    {
        var previous = Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", "classical");
            var state = global::IntracellularMicrotubuleState.Create(12);

            Assert.Equal("classical", state.Mode);
            Assert.True(state.Enabled);
            Assert.False(state.ExperimentalQuantumTermsEnabled);
            Assert.Equal(0.0, state.OpticalCollectiveBias, 6);
            Assert.Equal(0.0, state.RadicalPairSensitivity, 6);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", previous);
        }
    }
}
