using System;
using NeuralResonanceEngine.Protocol;

internal sealed class IntracellularMicrotubuleState
{
	private const double BaselineStability = 0.58;
	private const double BaselineTransportSupport = 0.52;
	private const double BaselineSpineEligibility = 0.08;

	private readonly double _cellBias;
	private readonly bool _enabled;
	private readonly bool _experimentalQuantumTermsEnabled;
	private readonly string _mode;
	private double _radicalPairPhase;
	private double _recentExcitatoryDrive;

	public double Stability { get; private set; }

	public double SpineInvasionEligibility { get; private set; }

	public double TransportSupport { get; private set; }

	public double OpticalCollectiveBias { get; private set; }

	public double RadicalPairSensitivity { get; private set; }

	public string Mode => _mode;

	public bool Enabled => _enabled;

	public bool ExperimentalQuantumTermsEnabled => _experimentalQuantumTermsEnabled;

	public double PlasticitySupport => !_enabled
		? 1.0
		: Math.Clamp(0.972 + Stability * 0.03 + SpineInvasionEligibility * 0.025 + TransportSupport * 0.02, 0.95, 1.05);

	public double TracePersistenceSupport => !_enabled
		? 1.0
		: Math.Clamp(0.974 + Stability * 0.04 + SpineInvasionEligibility * 0.03, 0.97, 1.03);

	public double IntegrationGain => !_enabled
		? 1.0
		: Math.Clamp(0.997 + Stability * 0.004 + TransportSupport * 0.002, 0.997, 1.003);

	private IntracellularMicrotubuleState(int neuronIndex, string mode, bool enabled, bool experimentalQuantumTermsEnabled)
	{
		_enabled = enabled;
		_experimentalQuantumTermsEnabled = experimentalQuantumTermsEnabled;
		_mode = mode;
		_cellBias = ((Math.Abs(HashCode.Combine(neuronIndex, 0x4D544255)) % 1000) / 1000.0 - 0.5) * 0.02;
		Stability = Math.Clamp(BaselineStability + _cellBias, 0.0, 1.0);
		SpineInvasionEligibility = BaselineSpineEligibility;
		TransportSupport = BaselineTransportSupport;
		RadicalPairSensitivity = experimentalQuantumTermsEnabled ? Math.Clamp(0.5 + _cellBias, 0.0, 1.0) : 0.0;
	}

	public static IntracellularMicrotubuleState Create(int neuronIndex)
	{
		string mode = NormalizeMode(Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE"));
		bool enabled = !mode.Equals("off", StringComparison.OrdinalIgnoreCase);
		bool experimental = enabled && mode.Equals("experimental", StringComparison.OrdinalIgnoreCase);
		return new IntracellularMicrotubuleState(neuronIndex, mode, enabled, experimental);
	}

	public static string NormalizeMode(string? mode)
	{
		if (string.IsNullOrWhiteSpace(mode))
		{
			return "classical";
		}

		mode = mode.Trim();
		if (mode.Equals("off", StringComparison.OrdinalIgnoreCase)
			|| mode.Equals("false", StringComparison.OrdinalIgnoreCase)
			|| mode.Equals("0", StringComparison.OrdinalIgnoreCase))
		{
			return "off";
		}

		if (mode.Equals("experimental", StringComparison.OrdinalIgnoreCase))
		{
			return "experimental";
		}

		return "classical";
	}

	public void ObserveSynapticInput(NTEnum neurotransmitter, float vesicleQuanta, NeuromodState? modulationContext, double dtMs)
	{
		if (!_enabled)
		{
			return;
		}

		double quanta = Math.Clamp(vesicleQuanta, 0.0f, 5.0f);
		if (neurotransmitter == NTEnum.GLUTAMATE)
		{
			// Null context means "no modulation broadcast on the wire" - treat as zeros.
			double acetylcholine = modulationContext is null ? 0.0 : Math.Clamp(modulationContext.AcetylcholineLevel, 0.0f, 1.0f);
			double dopamine = modulationContext is null ? 0.0 : Math.Clamp(modulationContext.DopamineLevel, 0.0f, 1.0f);
			_recentExcitatoryDrive = Math.Clamp(_recentExcitatoryDrive + quanta * (0.030 + acetylcholine * 0.012 + dopamine * 0.006), 0.0, 1.0);
		}
		else if (neurotransmitter == NTEnum.GABA)
		{
			_recentExcitatoryDrive = Math.Clamp(_recentExcitatoryDrive - quanta * 0.012, 0.0, 1.0);
		}

		AdvanceRecentDrive(dtMs);
	}

	public void Advance(double dtMs, NeuromodState neuromod, double excitatoryCurrent, double netDrive, float activityTrace, bool spiked)
	{
		if (!_enabled)
		{
			return;
		}

		double dtScale = Math.Clamp(dtMs / 20.0, 0.05, 4.0);
		double acetylcholine = Math.Clamp(neuromod.AcetylcholineLevel, 0.0f, 1.0f);
		double dopamine = Math.Clamp(neuromod.DopamineLevel, 0.0f, 1.0f);
		double norepinephrine = Math.Clamp(neuromod.NorepinephrineLevel, 0.0f, 1.0f);
		double serotonin = Math.Clamp(neuromod.SerotoninLevel, 0.0f, 1.0f);
		double boundedExcitation = Math.Clamp(excitatoryCurrent / 18.0, 0.0, 1.0);
		double boundedNet = Math.Clamp(Math.Max(0.0, netDrive) / 18.0, 0.0, 1.0);
		double activity = Math.Clamp(activityTrace, 0.0f, 1.0f);

		double invasionTarget = BaselineSpineEligibility
			+ _recentExcitatoryDrive * 0.30
			+ boundedExcitation * 0.18
			+ activity * 0.20
			+ acetylcholine * 0.08
			+ (spiked ? 0.06 : 0.0);
		SpineInvasionEligibility = Approach(SpineInvasionEligibility, Math.Clamp(invasionTarget, 0.0, 1.0), 0.065 * dtScale);

		double transportTarget = BaselineTransportSupport
			+ SpineInvasionEligibility * 0.18
			+ dopamine * 0.08
			+ acetylcholine * 0.06
			- serotonin * 0.05;
		TransportSupport = Approach(TransportSupport, Math.Clamp(transportTarget, 0.0, 1.0), 0.018 * dtScale);

		double stressPenalty = norepinephrine > 0.74 ? (norepinephrine - 0.74) * 0.10 : 0.0;
		double stabilityTarget = BaselineStability
			+ TransportSupport * 0.18
			+ SpineInvasionEligibility * 0.10
			+ boundedNet * 0.04
			- stressPenalty
			+ _cellBias;

		if (_experimentalQuantumTermsEnabled)
		{
			_radicalPairPhase += dtMs * (0.0017 + RadicalPairSensitivity * 0.0008);
			OpticalCollectiveBias = Math.Sin(_radicalPairPhase) * RadicalPairSensitivity * 0.06;
			stabilityTarget += OpticalCollectiveBias * 0.04;
		}
		else
		{
			OpticalCollectiveBias = 0.0;
		}

		Stability = Approach(Stability, Math.Clamp(stabilityTarget, 0.0, 1.0), 0.012 * dtScale);
		AdvanceRecentDrive(dtMs);
	}

	private void AdvanceRecentDrive(double dtMs)
	{
		_recentExcitatoryDrive *= Math.Exp(0.0 - Math.Max(0.0, dtMs) / 180.0);
		if (_recentExcitatoryDrive < 0.000001)
		{
			_recentExcitatoryDrive = 0.0;
		}
	}

	private static double Approach(double current, double target, double rate)
	{
		return current + (target - current) * Math.Clamp(rate, 0.0, 1.0);
	}
}
