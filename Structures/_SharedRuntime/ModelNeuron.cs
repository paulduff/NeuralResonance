using System;
using NeuralResonanceEngine.Protocol;

internal sealed class ModelNeuron
{
	private readonly NeuronModelKind _modelKind;

	private readonly StructureCircuitProfile _circuitProfile;

	private readonly IntracellularMicrotubuleState _microtubules;

	private readonly NeuronSubtypeProfile _subtype;

	private readonly ReceptorProfile _receptors;

	private double _v;

	private double _u;

	private double _phase;

	private double _ampaCurrent;

	private double _nmdaCurrent;

	private double _gabaACurrent;

	private double _gabaBCurrent;

	private double _metabotropicGlutamateCurrent;

	private double _dopamineD1Current;

	private double _dopamineD2Current;

	private double _serotonin5Ht1Current;

	private double _serotonin5Ht2Current;

	private double _nicotinicCurrent;

	private double _muscarinicCurrent;

	private double _adrenergicAlphaCurrent;

	private double _adrenergicBetaCurrent;

	private double _aisVoltage;

	private double _basalDendriteVoltage;

	private double _apicalDendriteVoltage;

	private double _calciumTrace;

	private double _adaptationCurrent;

	private double _relativeRefractoryScale;

	private double _refractoryMs;

	private double _dendriticPlateauTrace;

	private double _proximalClusterTrace;

	private double _distalClusterTrace;

	private double _plateauRecoveryMs;

	private double _homeostaticActivityTrace;

	private double _homeostaticExcitabilityBias;

	private double _homeostaticInhibitoryBias;

	private double _metabolicReserve = 1.0;

	private double _metabolicFatigueTrace;

	private double _astrocyteGlutamateLoad;

	private double _astrocytePotassiumLoad;

	private double _astrocyteLactateSupport;

	public int Index { get; }

	public string Id { get; }

	public bool IsActive { get; private set; }

	public float FiringRateHz { get; private set; }

	public float ActivityTrace { get; private set; }

	public StructureId PreferredTarget { get; private set; }

	public NTEnum PreferredNt { get; private set; }

	public float MicrotubulePlasticitySupport => (float)_microtubules.PlasticitySupport;

	public float MicrotubuleTracePersistenceSupport => (float)_microtubules.TracePersistenceSupport;

	public float CalciumPlasticitySupport => (float)Math.Clamp((0.97 + _calciumTrace * 0.06 + _dendriticPlateauTrace * 0.055) * (0.88 + _metabolicReserve * 0.12), 0.84, 1.08);

	public string MicrotubuleMode => _microtubules.Mode;

	public bool MicrotubuleEnabled => _microtubules.Enabled;

	public bool MicrotubuleExperimental => _microtubules.ExperimentalQuantumTermsEnabled;

	public float MicrotubuleStability => (float)_microtubules.Stability;

	public float MicrotubuleSpineInvasionEligibility => (float)_microtubules.SpineInvasionEligibility;

	public float MicrotubuleTransportSupport => (float)_microtubules.TransportSupport;

	public float MicrotubuleOpticalCollectiveBias => (float)_microtubules.OpticalCollectiveBias;

	public float MicrotubuleRadicalPairSensitivity => (float)_microtubules.RadicalPairSensitivity;

	public float MicrotubuleIntegrationGain => (float)_microtubules.IntegrationGain;

	public ModelNeuron(int index, string neuronModel, StructureCircuitProfile circuitProfile)
	{
		Index = index;
		Id = $"n-{index:000}";
		_circuitProfile = circuitProfile;
		PreferredTarget = circuitProfile.DefaultTarget;
		PreferredNt = circuitProfile.DefaultNt;
		_microtubules = IntracellularMicrotubuleState.Create(index);
		_subtype = NeuronSubtypeProfile.For(circuitProfile, index);
		_receptors = ReceptorProfile.For(circuitProfile, index);
		_modelKind = ParseModel(neuronModel);
		_v = _subtype.RestingPotential;
		_aisVoltage = _subtype.RestingPotential;
		_basalDendriteVoltage = _subtype.RestingPotential - 3.0;
		_apicalDendriteVoltage = _subtype.RestingPotential - 3.0;
		_phase = (double)index * 0.017;
	}

	public void Integrate(SpikeMessage spike, double dtMs)
	{
		var quanta = Math.Max(0.01f, spike.VesicleQuanta);
		var feedbackScale = spike.IsFeedback ? _circuitProfile.FeedbackAttenuation : 1.0;
		_microtubules.ObserveSynapticInput(spike.Neurotransmitter, quanta, spike.ModulationContext, dtMs);
		switch (spike.Neurotransmitter)
		{
		case NTEnum.GLUTAMATE:
			_ampaCurrent += (double)quanta * 8.6 * _receptors.AmpaWeight * feedbackScale;
			_nmdaCurrent += (double)quanta * 4.2 * _subtype.NmdaWeight * _receptors.NmdaWeight * feedbackScale;
			_metabotropicGlutamateCurrent += (double)quanta * 0.62 * _receptors.MetabotropicGlutamateWeight * feedbackScale;
			_basalDendriteVoltage += (double)quanta * 0.18 * feedbackScale;
			_apicalDendriteVoltage += (double)quanta * (spike.IsFeedback ? 0.24 : 0.12) * feedbackScale + _metabotropicGlutamateCurrent * 0.004;
			_proximalClusterTrace = Math.Clamp(_proximalClusterTrace + (double)quanta * (spike.IsFeedback ? 0.015 : 0.025) * feedbackScale, 0.0, 1.4);
			_distalClusterTrace = Math.Clamp(_distalClusterTrace + (double)quanta * (spike.IsFeedback ? 0.032 : 0.014) * feedbackScale, 0.0, 1.4);
			break;
		case NTEnum.GABA:
			_gabaACurrent += (double)quanta * 9.8 * _receptors.GabaAWeight * feedbackScale;
			_gabaBCurrent += (double)quanta * 3.8 * _receptors.GabaBWeight * feedbackScale;
			_basalDendriteVoltage -= (double)quanta * 0.16 * feedbackScale;
			_apicalDendriteVoltage -= (double)quanta * (0.15 + 0.05 * _receptors.GabaBWeight) * feedbackScale;
			_proximalClusterTrace *= Math.Exp(0.0 - (double)quanta * 0.035 * feedbackScale);
			_distalClusterTrace *= Math.Exp(0.0 - (double)quanta * (0.035 + 0.02 * _receptors.GabaBWeight) * feedbackScale);
			break;
		case NTEnum.DOPAMINE:
			_dopamineD1Current += (double)quanta * 0.48 * _receptors.DopamineD1Weight * feedbackScale;
			_dopamineD2Current += (double)quanta * 0.34 * _receptors.DopamineD2Weight * feedbackScale;
			break;
		case NTEnum.SEROTONIN:
			_serotonin5Ht1Current += (double)quanta * 0.42 * _receptors.Serotonin5Ht1Weight * feedbackScale;
			_serotonin5Ht2Current += (double)quanta * 0.28 * _receptors.Serotonin5Ht2Weight * feedbackScale;
			break;
		case NTEnum.ACETYLCHOLINE:
			_nicotinicCurrent += (double)quanta * 0.58 * _receptors.NicotinicWeight * feedbackScale;
			_muscarinicCurrent += (double)quanta * 0.38 * _receptors.MuscarinicWeight * feedbackScale;
			break;
		case NTEnum.NOREPINEPHRINE:
			_adrenergicBetaCurrent += (double)quanta * 0.66 * _receptors.AdrenergicBetaWeight * feedbackScale;
			_adrenergicAlphaCurrent += (double)quanta * 0.34 * _receptors.AdrenergicAlphaWeight * feedbackScale;
			break;
		default:
			_ampaCurrent += (double)quanta * 6.0 * _receptors.AmpaWeight * feedbackScale;
			_metabotropicGlutamateCurrent += (double)quanta * 0.22 * _receptors.MetabotropicGlutamateWeight * feedbackScale;
			_basalDendriteVoltage += (double)quanta * 0.1 * feedbackScale;
			break;
		}
		_basalDendriteVoltage = Math.Clamp(_basalDendriteVoltage, -85.0, -35.0);
		_apicalDendriteVoltage = Math.Clamp(_apicalDendriteVoltage, -85.0, -35.0);
	}

	public void IntegrateIntrinsicDrive(float excitatory, float inhibitory)
	{
		if (excitatory > 0f)
		{
			_ampaCurrent += Math.Clamp(excitatory, 0f, 1f) * 2.2;
			_nmdaCurrent += Math.Clamp(excitatory, 0f, 1f) * 0.65;
		}

		if (inhibitory > 0f)
		{
			_gabaACurrent += Math.Clamp(inhibitory, 0f, 1f) * 2.4;
			_gabaBCurrent += Math.Clamp(inhibitory, 0f, 1f) * 0.55;
		}
	}

	public bool Step(double dtMs, NeuromodState neuromod)
	{
		AdvanceSynapticKinetics(dtMs);
		AdvanceGlialSupport(dtMs);
		var excitabilityGain = (1.0 + 0.16 * (double)neuromod.NorepinephrineLevel + 0.1 * (double)neuromod.AcetylcholineLevel) * _circuitProfile.ExcitabilityScale;
		var dopamineGate = 1.0 + 0.08 * (double)neuromod.DopamineLevel;
		var inhibitoryGain = (1.0 + 0.75 * (double)neuromod.SerotoninLevel + 0.4 * (double)neuromod.AcetylcholineLevel) * _circuitProfile.InhibitoryScale * _subtype.InhibitorySensitivity;
		excitabilityGain *= 1.0 + _homeostaticExcitabilityBias;
		inhibitoryGain *= 1.0 + _homeostaticInhibitoryBias;
		excitabilityGain *= 0.76 + _metabolicReserve * 0.24;
		var receptorExcitability = 1.0 + Math.Clamp(
			_dopamineD1Current * 0.035
			+ _nicotinicCurrent * 0.045
			+ _adrenergicBetaCurrent * 0.04
			+ _serotonin5Ht2Current * 0.018
			- _dopamineD2Current * 0.028
			- _serotonin5Ht1Current * 0.038
			- _muscarinicCurrent * 0.012,
			-0.3,
			0.42);
		var receptorInhibition = 1.0 + Math.Clamp(
			_gabaBCurrent * 0.002
			+ _serotonin5Ht1Current * 0.035
			+ _dopamineD2Current * 0.018
			+ _adrenergicAlphaCurrent * 0.018
			- _nicotinicCurrent * 0.012,
			-0.18,
			0.36);
		excitabilityGain *= receptorExcitability;
		inhibitoryGain *= receptorInhibition;
		inhibitoryGain = Math.Max(0.35, inhibitoryGain);
		var phasicDrive = Math.Clamp(
			_dopamineD1Current * 0.18
			- _dopamineD2Current * 0.1
			- _serotonin5Ht1Current * 0.16
			+ _serotonin5Ht2Current * 0.08
			+ _nicotinicCurrent * 0.2
			+ _muscarinicCurrent * 0.07
			+ _adrenergicBetaCurrent * 0.22
			- _adrenergicAlphaCurrent * 0.08
			+ _metabotropicGlutamateCurrent * 0.05,
			-3.0,
			3.0);

		_phase += 0.011 + (double)Index * 3E-05;
		if (_phase > Math.PI * 2.0)
		{
			_phase -= Math.PI * 2.0;
		}

		var tonicDrive = ((0.82 + 0.38 * Math.Sin(_phase)) * _circuitProfile.TonicDrive * _subtype.TonicDriveScale) * (1.0 - 0.18 * (double)neuromod.SerotoninLevel);
		tonicDrive += Math.Clamp(_metabotropicGlutamateCurrent * 0.012 + _muscarinicCurrent * 0.018 + _adrenergicBetaCurrent * 0.016 - _serotonin5Ht1Current * 0.018, -0.35, 0.55);
		tonicDrive = Math.Max(0.15, tonicDrive);
		var nmdaVoltageRelief = 1.0 / (1.0 + 0.28 * Math.Exp(-0.062 * _v));
		var excitatorySynCurrent = _ampaCurrent + (_nmdaCurrent * nmdaVoltageRelief) + _metabotropicGlutamateCurrent * 0.35;
		var inhibitorySynCurrent = (_gabaACurrent * 1.05) + (_gabaBCurrent * 0.85);
		var netSynCurrent = excitatorySynCurrent - (inhibitorySynCurrent * inhibitoryGain) + phasicDrive;
		netSynCurrent -= _metabolicFatigueTrace * 1.8;
		netSynCurrent -= _astrocytePotassiumLoad * 0.22;
		excitabilityGain *= _microtubules.IntegrationGain;
		AdvanceCompartmentalBiology(dtMs, neuromod, nmdaVoltageRelief, excitatorySynCurrent, inhibitorySynCurrent, tonicDrive, phasicDrive, inhibitoryGain, excitabilityGain, ref netSynCurrent, out var thresholdOffset);
		var canSpike = _refractoryMs <= 0.0;

		switch (_modelKind)
		{
		case NeuronModelKind.Lif:
			_v += dtMs * (((_subtype.RestingPotential - _v) + (netSynCurrent + tonicDrive) * excitabilityGain) / (20.0 * _subtype.SomaTimeScale));
			var lifThreshold = (-50.0 / dopamineGate) + thresholdOffset + _subtype.ThresholdOffset;
			IsActive = canSpike && (_aisVoltage >= lifThreshold || _v >= lifThreshold + 1.0);
			if (IsActive)
			{
				RegisterSpike(_subtype.RestingPotential, dopamineGate);
			}
			break;
		case NeuronModelKind.Hh:
		{
			double x = 1.0 / (1.0 + Math.Exp((0.0 - (_v + 40.0)) / 10.0));
			double num4 = 1.0 / (1.0 + Math.Exp((_v + 62.0) / 10.0));
			double x2 = 1.0 / (1.0 + Math.Exp((0.0 - (_v + 53.0)) / 16.0));
			double num5 = 120.0 * Math.Pow(x, 3.0) * num4 * (_v - 50.0);
			double num6 = 36.0 * Math.Pow(x2, 4.0) * (_v + 77.0);
			double num7 = 0.3 * (_v + 54.387);
			_v += dtMs * ((netSynCurrent + tonicDrive) * excitabilityGain - num5 - num6 - num7) / 100.0;
			var hhThreshold = 30.0 + thresholdOffset + _subtype.ThresholdOffset;
			IsActive = canSpike && (_aisVoltage >= hhThreshold || _v >= hhThreshold);
			if (IsActive)
			{
				RegisterSpike(_subtype.RestingPotential + 7.0, dopamineGate);
			}
			break;
		}
		default:
			_v += dtMs * (0.04 * _v * _v + 5.0 * _v + 140.0 - _u + (netSynCurrent + tonicDrive) * excitabilityGain);
			_u += dtMs * (0.02 * (0.2 * _v - _u));
			var izhikevichThreshold = 30.0 + thresholdOffset + _subtype.ThresholdOffset;
			IsActive = canSpike && (_aisVoltage >= izhikevichThreshold || _v >= izhikevichThreshold);
			if (IsActive)
			{
				_u += 8.0 * dopamineGate;
				RegisterSpike(_subtype.RestingPotential, dopamineGate);
			}
			break;
		}

		ActivityTrace = (float)(0.84 * (double)ActivityTrace + (IsActive ? 0.16 : 0.0));
		FiringRateHz = (float)(0.72 * (double)FiringRateHz + (IsActive ? (1000.0 / Math.Max(dtMs, 1.0) * 0.28) : 0.0));
		AdvanceHomeostaticPlasticity(dtMs);
		AdvanceMetabolicState(dtMs, neuromod, excitatorySynCurrent, inhibitorySynCurrent);
		_microtubules.Advance(dtMs, neuromod, excitatorySynCurrent, netSynCurrent, ActivityTrace, IsActive);
		return IsActive;
	}

	private void AdvanceHomeostaticPlasticity(double dtMs)
	{
		var activityDecay = Math.Exp(0.0 - dtMs / 60000.0);
		_homeostaticActivityTrace = Math.Clamp(_homeostaticActivityTrace * activityDecay + (double)ActivityTrace * (1.0 - activityDecay), 0.0, 1.0);
		var targetActivity = SelectHomeostaticTargetActivity();
		var error = targetActivity - _homeostaticActivityTrace;
		var learningRate = 1.0 - Math.Exp(0.0 - dtMs / 120000.0);
		_homeostaticExcitabilityBias = Math.Clamp(_homeostaticExcitabilityBias + error * learningRate * 0.9, -0.18, 0.22);
		_homeostaticInhibitoryBias = Math.Clamp(_homeostaticInhibitoryBias - error * learningRate * 0.7, -0.15, 0.2);
	}

	private double SelectHomeostaticTargetActivity()
	{
		return _circuitProfile.StructureId switch
		{
			StructureId.Snc or StructureId.Vta or StructureId.LocusCoeruleus or StructureId.RapheNuclei or StructureId.BasalForebrain => 0.035,
			StructureId.Striatum or StructureId.NucleusAccumbens => 0.045,
			StructureId.GlobusPallidus or StructureId.GPe or StructureId.GPi or StructureId.Snr or StructureId.Trn => 0.075,
			StructureId.CerebellarGranule => 0.04,
			StructureId.PurkinjeCellLayer => 0.09,
			_ when _circuitProfile.DefaultNt == NTEnum.GABA => 0.08,
			_ => 0.06,
		};
	}

	private void AdvanceMetabolicState(double dtMs, NeuromodState neuromod, double excitatorySynCurrent, double inhibitorySynCurrent)
	{
		var synapticLoad = Math.Clamp((excitatorySynCurrent + inhibitorySynCurrent) * dtMs * 0.000002, 0.0, 0.0015);
		var spikeCost = IsActive ? 0.0028 : 0.0;
		var plasticityCost = Math.Clamp((_calciumTrace + _dendriticPlateauTrace) * dtMs * 0.000012, 0.0, 0.0012);
		var restOpportunity = Math.Clamp(1.0 - (double)ActivityTrace * 6.0, 0.0, 1.0);
		var recoveryDrive = (0.55 + 0.55 * (double)neuromod.SerotoninLevel + 0.25 * (1.0 - (double)neuromod.NorepinephrineLevel) + _astrocyteLactateSupport * 0.35) * (0.35 + restOpportunity * 0.65);
		var recovery = dtMs / 90000.0 * recoveryDrive;
		_metabolicReserve = Math.Clamp(_metabolicReserve + recovery - spikeCost - synapticLoad - plasticityCost, 0.2, 1.0);
		var fatigueDecay = Math.Exp(0.0 - dtMs / 8000.0);
		_metabolicFatigueTrace = Math.Clamp(_metabolicFatigueTrace * fatigueDecay + (1.0 - _metabolicReserve) * (1.0 - fatigueDecay), 0.0, 1.0);
	}

	private void AdvanceGlialSupport(double dtMs)
	{
		var glutamateLoadDecay = Math.Exp(0.0 - dtMs / 1800.0);
		var potassiumLoadDecay = Math.Exp(0.0 - dtMs / 2400.0);
		var lactateDecay = Math.Exp(0.0 - dtMs / 30000.0);
		var excitatoryLoad = Math.Clamp((_ampaCurrent + _nmdaCurrent + _metabotropicGlutamateCurrent) * dtMs * 0.000006, 0.0, 0.035);
		_astrocyteGlutamateLoad = Math.Clamp(_astrocyteGlutamateLoad * glutamateLoadDecay + excitatoryLoad, 0.0, 1.0);
		_astrocytePotassiumLoad = Math.Clamp(_astrocytePotassiumLoad * potassiumLoadDecay + (IsActive ? 0.025 : 0.0) + (double)ActivityTrace * 0.001, 0.0, 1.0);
		var restOpportunity = Math.Clamp(1.0 - (double)ActivityTrace * 5.0, 0.0, 1.0);
		_astrocyteLactateSupport = Math.Clamp(_astrocyteLactateSupport * lactateDecay + restOpportunity * (1.0 - lactateDecay), 0.0, 1.0);
		var cleanup = Math.Clamp(_astrocyteGlutamateLoad * 0.012, 0.0, 0.11);
		_ampaCurrent *= 1.0 - cleanup;
		_nmdaCurrent *= 1.0 - cleanup * 0.55;
		_metabotropicGlutamateCurrent *= 1.0 - cleanup * 0.35;
	}

	private void AdvanceCompartmentalBiology(double dtMs, NeuromodState neuromod, double nmdaVoltageRelief, double excitatorySynCurrent, double inhibitorySynCurrent, double tonicDrive, double phasicDrive, double inhibitoryGain, double excitabilityGain, ref double netSynCurrent, out double thresholdOffset)
	{
		var dendriticRest = _subtype.RestingPotential - 3.0;
		var dendriticLeak = Math.Exp(0.0 - dtMs / _subtype.DendriticTimeConstantMs);
		_basalDendriteVoltage = dendriticRest + (_basalDendriteVoltage - dendriticRest) * dendriticLeak + Math.Clamp(excitatorySynCurrent * 0.018 - inhibitorySynCurrent * inhibitoryGain * 0.012, -1.2, 1.8);
		_apicalDendriteVoltage = dendriticRest + (_apicalDendriteVoltage - dendriticRest) * dendriticLeak + Math.Clamp((_nmdaCurrent * nmdaVoltageRelief + _metabotropicGlutamateCurrent * 0.22) * 0.02 + phasicDrive * 0.04 - inhibitorySynCurrent * 0.008, -1.0, 1.6);
		_basalDendriteVoltage = Math.Clamp(_basalDendriteVoltage, -85.0, -35.0);
		_apicalDendriteVoltage = Math.Clamp(_apicalDendriteVoltage, -85.0, -35.0);

		_proximalClusterTrace *= Math.Exp(0.0 - dtMs / 38.0);
		_distalClusterTrace *= Math.Exp(0.0 - dtMs / 82.0);
		_plateauRecoveryMs = Math.Max(0.0, _plateauRecoveryMs - dtMs);
		var apicalDepolarisation = Math.Max(0.0, _apicalDendriteVoltage + 64.0) / 18.0;
		var basalDepolarisation = Math.Max(0.0, _basalDendriteVoltage + 64.0) / 20.0;
		var nmdaCoincidence = Math.Clamp((_nmdaCurrent * nmdaVoltageRelief + _metabotropicGlutamateCurrent * 0.18) / 54.0, 0.0, 1.0);
		var clusterCoincidence = Math.Clamp((_distalClusterTrace * 0.65) + (_proximalClusterTrace * 0.35), 0.0, 1.0);
		var inhibitoryShunt = Math.Clamp(inhibitorySynCurrent * inhibitoryGain / 80.0, 0.0, 0.9);
		var plateauDrive = Math.Clamp((nmdaCoincidence * 0.45 + clusterCoincidence * 0.35 + apicalDepolarisation * 0.16 + basalDepolarisation * 0.04) * (1.0 - inhibitoryShunt) * _subtype.PlateauGain, 0.0, 1.0);
		var plateauEntry = _plateauRecoveryMs <= 0.0 && plateauDrive > 0.58 ? (plateauDrive - 0.58) * 0.22 : 0.0;
		if (plateauEntry > 0.0)
		{
			_plateauRecoveryMs = 28.0;
		}
		_dendriticPlateauTrace = Math.Clamp(_dendriticPlateauTrace * Math.Exp(0.0 - dtMs / 125.0) + plateauEntry + plateauDrive * 0.006, 0.0, 1.0);
		var dendriticDrive = Math.Clamp(Math.Max(0.0, _basalDendriteVoltage - (_subtype.RestingPotential + 0.5)) * 0.1 + Math.Max(0.0, _apicalDendriteVoltage - (_subtype.RestingPotential + 0.5)) * 0.08 + _dendriticPlateauTrace * 3.0 * _subtype.PlateauGain, 0.0, 5.8);

		_adaptationCurrent *= Math.Exp(0.0 - dtMs / _subtype.AdaptationDecayMs);
		_calciumTrace = Math.Clamp(_calciumTrace * Math.Exp(0.0 - dtMs / 260.0) + (Math.Clamp((_nmdaCurrent * nmdaVoltageRelief + _metabotropicGlutamateCurrent * 0.12) / 2000.0, 0.0, 0.012) + _dendriticPlateauTrace * 0.004) * _subtype.CalciumGain, 0.0, 1.0);
		_refractoryMs = Math.Max(0.0, _refractoryMs - dtMs);
		_relativeRefractoryScale *= Math.Exp(0.0 - dtMs / 25.0);

		netSynCurrent += dendriticDrive - _adaptationCurrent;
		var axonDrive = (netSynCurrent + tonicDrive) * excitabilityGain + phasicDrive - _adaptationCurrent * (0.5 + 0.2 * (double)neuromod.SerotoninLevel);
		_aisVoltage += dtMs * ((_v - _aisVoltage + axonDrive * _subtype.AisDriveScale) / 4.5);
		_aisVoltage = Math.Clamp(_aisVoltage, -90.0, 35.0);
		thresholdOffset = _relativeRefractoryScale * 4.0 + _adaptationCurrent * 0.3 + Math.Clamp(_serotonin5Ht1Current * 0.1 + _dopamineD2Current * 0.07 - _nicotinicCurrent * 0.08 - _adrenergicBetaCurrent * 0.08, -1.2, 1.4);
	}

	private void RegisterSpike(double resetVoltage, double dopamineGate)
	{
		_v = resetVoltage;
		_aisVoltage = Math.Min(_aisVoltage, resetVoltage + 3.0);
		_refractoryMs = _modelKind == NeuronModelKind.Hh ? Math.Max(2.2, _subtype.RefractoryMs) : _subtype.RefractoryMs;
		_relativeRefractoryScale = Math.Min(1.0, _relativeRefractoryScale + 0.55);
		_adaptationCurrent = Math.Min(12.0, _adaptationCurrent + (_modelKind == NeuronModelKind.Hh ? 1.6 : 2.2) * _subtype.AdaptationIncrementScale * dopamineGate);
		_calciumTrace = Math.Clamp(_calciumTrace + 0.055 * _subtype.CalciumGain, 0.0, 1.0);
	}

	private readonly record struct NeuronSubtypeProfile(
		double RestingPotential,
		double DendriticTimeConstantMs,
		double SomaTimeScale,
		double AisDriveScale,
		double ThresholdOffset,
		double AdaptationIncrementScale,
		double AdaptationDecayMs,
		double RefractoryMs,
		double PlateauGain,
		double CalciumGain,
		double InhibitorySensitivity,
		double NmdaWeight,
		double TonicDriveScale)
	{
		public static NeuronSubtypeProfile For(StructureCircuitProfile circuitProfile, int index)
		{
			return circuitProfile.StructureId switch
			{
				StructureId.PurkinjeCellLayer => new NeuronSubtypeProfile(-66.0, 68.0, 0.9, 0.064, 3.0, 1.45, 260.0, 2.4, 0.75, 1.2, 1.25, 0.75, 0.82),
				StructureId.CerebellarGranule => new NeuronSubtypeProfile(-70.0, 30.0, 0.82, 0.1, -1.0, 0.65, 145.0, 1.2, 0.55, 0.82, 1.0, 0.8, 1.08),
				StructureId.CerebellarVermis or StructureId.CerebellarLobules or StructureId.DeepCerebellarNuclei or StructureId.InferiorOlive => new NeuronSubtypeProfile(-67.0, 54.0, 0.95, 0.078, 0.8, 0.95, 220.0, 1.9, 0.85, 1.0, 1.12, 0.9, 0.94),
				StructureId.Striatum or StructureId.NucleusAccumbens => new NeuronSubtypeProfile(-72.0, 62.0, 1.15, 0.065, 4.0, 1.25, 310.0, 2.6, 0.88, 1.1, 1.35, 1.0, 0.66),
				StructureId.GlobusPallidus or StructureId.GPe or StructureId.GPi or StructureId.Stn or StructureId.Snr or StructureId.VentralPallidum or StructureId.Habenula => new NeuronSubtypeProfile(-68.0, 50.0, 1.0, 0.08, 1.0, 1.1, 215.0, 2.0, 0.78, 0.95, 1.25, 0.9, 0.82),
				StructureId.Thalamus or StructureId.Trn or StructureId.Pulvinar or StructureId.MediodorsalThalamus or StructureId.IntralaminarThalamus or StructureId.MotorThalamus => new NeuronSubtypeProfile(-67.0, 45.0, 0.96, 0.08, 0.5, 0.78, 190.0, 1.7, 0.72, 0.95, 1.2, 0.86, 0.92),
				StructureId.BasalForebrain => new NeuronSubtypeProfile(-63.0, 72.0, 1.18, 0.074, -0.4, 0.72, 640.0, 2.2, 0.82, 1.25, 0.92, 1.08, 1.08),
				StructureId.Snc or StructureId.Vta or StructureId.LocusCoeruleus or StructureId.RapheNuclei => new NeuronSubtypeProfile(-63.0, 72.0, 1.35, 0.056, 2.5, 0.82, 520.0, 2.4, 0.72, 1.35, 1.1, 1.1, 0.76),
				StructureId.EntorhinalCortex or StructureId.DentateGyrus or StructureId.CA3 or StructureId.CA2 or StructureId.CA1 or StructureId.Subiculum or StructureId.Presubiculum or StructureId.Parasubiculum => new NeuronSubtypeProfile(-65.0, 58.0, 1.05, 0.082, -0.5, 1.05, 240.0, 1.9, 1.18, 1.15, 1.0, 1.18, 1.08),
				_ when circuitProfile.DefaultNt == NTEnum.GABA => new NeuronSubtypeProfile(-67.0, 42.0, 0.9, 0.092, -0.8, 0.8, 165.0, 1.4, 0.65, 0.9, 1.15, 0.78, 0.96),
				_ => SelectCorticalSubtype(index),
			};
		}

		private static NeuronSubtypeProfile SelectCorticalSubtype(int index)
		{
			var band = Math.Abs(index) % 20;
			if (band < 3)
			{
				return new NeuronSubtypeProfile(-66.0, 34.0, 0.78, 0.1, -1.2, 0.7, 150.0, 1.3, 0.62, 0.88, 1.18, 0.74, 0.95);
			}
			if (band == 3)
			{
				return new NeuronSubtypeProfile(-64.0, 72.0, 1.2, 0.06, 1.4, 1.25, 360.0, 2.2, 1.3, 1.18, 0.92, 1.22, 1.02);
			}
			return new NeuronSubtypeProfile(-65.0, 48.0, 1.0, 0.082, 0.0, 1.0, 220.0, 1.8, 1.0, 1.0, 1.0, 1.0, 1.0);
		}
	}

	private readonly record struct ReceptorProfile(
		double AmpaWeight,
		double NmdaWeight,
		double MetabotropicGlutamateWeight,
		double GabaAWeight,
		double GabaBWeight,
		double DopamineD1Weight,
		double DopamineD2Weight,
		double Serotonin5Ht1Weight,
		double Serotonin5Ht2Weight,
		double NicotinicWeight,
		double MuscarinicWeight,
		double AdrenergicAlphaWeight,
		double AdrenergicBetaWeight)
	{
		public static ReceptorProfile For(StructureCircuitProfile circuitProfile, int index)
		{
			return circuitProfile.StructureId switch
			{
				StructureId.PurkinjeCellLayer => new ReceptorProfile(1.05, 0.72, 1.45, 1.25, 1.15, 0.55, 0.8, 0.9, 0.7, 0.55, 0.9, 0.8, 0.75),
				StructureId.CerebellarGranule => new ReceptorProfile(1.15, 0.85, 0.7, 0.9, 0.75, 0.45, 0.55, 0.65, 0.55, 0.55, 0.55, 0.6, 0.65),
				StructureId.CerebellarVermis or StructureId.CerebellarLobules or StructureId.DeepCerebellarNuclei or StructureId.InferiorOlive => new ReceptorProfile(1.05, 0.95, 0.9, 1.05, 1.0, 0.65, 0.75, 0.8, 0.7, 0.7, 0.75, 0.75, 0.8),
				StructureId.Striatum or StructureId.NucleusAccumbens => SelectStriatalSubtype(index),
				StructureId.GlobusPallidus or StructureId.GPe or StructureId.GPi or StructureId.Stn or StructureId.Snr or StructureId.VentralPallidum or StructureId.Habenula => new ReceptorProfile(0.92, 1.02, 0.95, 1.05, 1.25, 0.75, 1.1, 0.92, 0.68, 0.7, 0.88, 0.88, 0.8),
				StructureId.Thalamus or StructureId.Trn or StructureId.Pulvinar or StructureId.MediodorsalThalamus or StructureId.IntralaminarThalamus or StructureId.MotorThalamus => new ReceptorProfile(1.05, 0.9, 0.75, 1.15, 1.05, 0.7, 0.78, 0.85, 0.75, 1.05, 0.92, 0.82, 1.05),
				StructureId.Snc or StructureId.Vta or StructureId.LocusCoeruleus or StructureId.RapheNuclei or StructureId.BasalForebrain => new ReceptorProfile(0.9, 0.88, 0.82, 1.0, 1.1, 0.85, 1.28, 1.18, 0.72, 0.9, 1.12, 1.2, 0.88),
				StructureId.EntorhinalCortex or StructureId.DentateGyrus or StructureId.CA3 or StructureId.CA2 or StructureId.CA1 or StructureId.Subiculum or StructureId.Presubiculum or StructureId.Parasubiculum => new ReceptorProfile(1.0, 1.25, 1.1, 0.95, 1.0, 0.85, 0.82, 0.92, 0.86, 1.1, 1.08, 0.8, 0.95),
				_ when circuitProfile.DefaultNt == NTEnum.GABA => new ReceptorProfile(1.15, 0.72, 0.7, 0.78, 0.82, 0.6, 0.72, 0.8, 0.65, 0.82, 0.78, 0.78, 0.86),
				_ => SelectCorticalSubtype(index),
			};
		}

		private static ReceptorProfile SelectStriatalSubtype(int index)
		{
			var d1Dominant = (index & 1) == 0;
			return new ReceptorProfile(0.9, 1.1, 1.05, 0.95, 1.3, d1Dominant ? 1.35 : 0.75, d1Dominant ? 0.75 : 1.35, 0.9, 0.75, 0.72, 0.95, 0.85, 0.82);
		}

		private static ReceptorProfile SelectCorticalSubtype(int index)
		{
			var band = Math.Abs(index) % 20;
			if (band < 3)
			{
				return new ReceptorProfile(1.2, 0.72, 0.68, 0.75, 0.7, 0.55, 0.65, 0.78, 0.65, 0.9, 0.72, 0.78, 0.9);
			}
			if (band == 3)
			{
				return new ReceptorProfile(0.95, 1.28, 1.12, 1.0, 1.08, 0.95, 0.82, 0.9, 0.88, 1.08, 1.1, 0.82, 0.98);
			}
			return new ReceptorProfile(1.0, 1.0, 1.0, 1.0, 1.0, 0.85, 0.85, 0.85, 0.85, 1.0, 1.0, 0.9, 0.95);
		}
	}

	private void AdvanceSynapticKinetics(double dtMs)
	{
		DecayCurrent(ref _ampaCurrent, dtMs, 6.0);
		DecayCurrent(ref _nmdaCurrent, dtMs, 90.0);
		DecayCurrent(ref _metabotropicGlutamateCurrent, dtMs, 480.0);
		DecayCurrent(ref _gabaACurrent, dtMs, 10.0);
		DecayCurrent(ref _gabaBCurrent, dtMs, 120.0);
		DecayCurrent(ref _dopamineD1Current, dtMs, 260.0);
		DecayCurrent(ref _dopamineD2Current, dtMs, 420.0);
		DecayCurrent(ref _serotonin5Ht1Current, dtMs, 520.0);
		DecayCurrent(ref _serotonin5Ht2Current, dtMs, 260.0);
		DecayCurrent(ref _nicotinicCurrent, dtMs, 22.0);
		DecayCurrent(ref _muscarinicCurrent, dtMs, 650.0);
		DecayCurrent(ref _adrenergicAlphaCurrent, dtMs, 360.0);
		DecayCurrent(ref _adrenergicBetaCurrent, dtMs, 280.0);
	}

	private static void DecayCurrent(ref double current, double dtMs, double tauMs)
	{
		current *= Math.Exp(0.0 - dtMs / tauMs);
		if (Math.Abs(current) < 1E-06)
		{
			current = 0.0;
		}
	}

	private static NeuronModelKind ParseModel(string neuronModel)
	{
		if (string.Equals(neuronModel, "LIF", StringComparison.OrdinalIgnoreCase))
		{
			return NeuronModelKind.Lif;
		}
		if (string.Equals(neuronModel, "HH", StringComparison.OrdinalIgnoreCase))
		{
			return NeuronModelKind.Hh;
		}
		return NeuronModelKind.Izhikevich;
	}
}
