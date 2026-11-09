using System;
using NeuralResonanceEngine.Protocol;

internal sealed class SynapseState(
	Guid synapseId,
	NTEnum neurotransmitter,
	float vesicleQuanta,
	float? baselineVesicleQuanta = null)
{
	public Guid SynapseId { get; } = synapseId;

	public NTEnum Neurotransmitter { get; } = neurotransmitter;

	public float VesicleQuanta { get; set; } = vesicleQuanta;

	public float BaselineVesicleQuanta { get; set; } = baselineVesicleQuanta ?? vesicleQuanta;
	// Transient chemistry is reinitialized on a learned-weight warm start.
	internal PurkinjeCalciumPlasticity? PurkinjeCalcium { get; set; }
	internal CorticalContactNumerics? CorticalPrecision { get; private set; }
	private bool _corticalPairChangesWeight;

	internal void EnableCorticalPrecision(CorticalContactState state, bool pairChangesWeight)
	{
		if (CorticalPrecision is not null || state.ContactId != SynapseId ||
			Neurotransmitter != NTEnum.GLUTAMATE || IsSyntheticBackground)
			throw new InvalidOperationException("Precision state requires its own explicit receiving glutamate contact.");
		CorticalPrecision = new(state, pairChangesWeight);
		_corticalPairChangesWeight = pairChangesWeight;
		RefreshCorticalEvidence();
	}

	// Legacy floats are one-way diagnostic mirrors. Never reconstruct the
	// accepted double state from them, including during persistence or delivery.
	internal void RefreshCorticalEvidence()
	{
		if (CorticalPrecision is not { } numerical) return;
		var s = numerical.State;
		VesicleQuanta = (float)s.Weight; BaselineVesicleQuanta = (float)s.Baseline;
		PreTrace = (float)s.PreTrace; PostTrace = (float)s.PostTrace; ThetaM = (float)s.Theta;
		EligibilityTrace = (float)s.Eligibility; SynapticTagTrace = (float)s.Tag;
		UpdateCount = s.Updates; LastUpdateTimestampMs = s.TimeMs;
		PlasticityBudgetQuanta = (float)s.Budget; LastPlasticityBudgetTimestampMs = s.BudgetTimeMs;
		TotalAbsolutePlasticityChange = s.ChargedPlasticity;
	}

	internal void InitializeCorticalClock(double timestampMs)
	{
		if (CorticalPrecision is not { } numerical) return;
		var s = numerical.State;
		if (s.TimeMs != 0 || s.PreTrace != 0 || s.PostTrace != 0 || s.Eligibility != 0 || s.Tag != 0)
			throw new InvalidOperationException("Only cleared warm-start chemistry may acquire a new biological origin.");
		CorticalPrecision = new(s with { TimeMs=timestampMs, BudgetTimeMs=timestampMs }, _corticalPairChangesWeight);
		RefreshCorticalEvidence();
	}

	public Guid ProjectionId { get; set; }
	public StructureId SourceStructure { get; set; }
	public string SourceInstanceKey { get; set; } = string.Empty;
	public string SourceNeuronId { get; set; } = string.Empty;
	public bool IsFeedback { get; set; }
	public bool IsSyntheticBackground { get; set; }

	// Warm initialization preserves learned efficacy and remaining plasticity
	// capacity. It deliberately does not pretend to resume membrane/trace state.
	public void PrepareWarmStart()
	{
		if (CorticalPrecision is { } numerical)
		{
			CorticalPrecision = new(numerical.State with
				{ TimeMs=0, PreTrace=0, PostTrace=0, Eligibility=0, Tag=0, BudgetTimeMs=-1 }, _corticalPairChangesWeight);
			RefreshCorticalEvidence();
		}
		PreTrace = PostTrace = EligibilityTrace = SynapticTagTrace = 0f;
		PurkinjeCalcium = null;
		LastUpdateTimestampMs = 0;
		LastPlasticityBudgetTimestampMs = -1;
	}

	public float PreTrace { get; set; }

	public float PostTrace { get; set; }

	public float ThetaM { get; set; } = 0.2f;

	public float EligibilityTrace { get; set; }

	public float SynapticTagTrace { get; set; }

	public double LastUpdateTimestampMs { get; set; }

	public int LastTargetNeuronIndex { get; set; } = -1;

	public int UpdateCount { get; set; }

	public float PlasticityBudgetQuanta { get; set; } = PlasticityRules.InitialPlasticityBudgetQuanta;

	public double LastPlasticityBudgetTimestampMs { get; set; } = -1.0;

	public double TotalAbsolutePlasticityChange { get; set; }

	public void Stabilize()
	{
		if (CorticalPrecision is not null) { RefreshCorticalEvidence(); return; }
		VesicleQuanta = PlasticityRules.ClampQuanta(VesicleQuanta);
		BaselineVesicleQuanta = PlasticityRules.ClampQuanta(BaselineVesicleQuanta);
		PreTrace = FiniteClamp(PreTrace, 0f, 8f, 0f);
		PostTrace = FiniteClamp(PostTrace, 0f, 8f, 0f);
		ThetaM = FiniteClamp(ThetaM, 0.001f, 10f, 0.2f);
		EligibilityTrace = FiniteClamp(EligibilityTrace, -1f, 1f, 0f);
		SynapticTagTrace = FiniteClamp(SynapticTagTrace, -1f, 1f, 0f);
		LastUpdateTimestampMs = double.IsFinite(LastUpdateTimestampMs)
			? Math.Max(0.0, LastUpdateTimestampMs)
			: 0.0;
		UpdateCount = Math.Max(0, UpdateCount);
		PlasticityBudgetQuanta = FiniteClamp(
			PlasticityBudgetQuanta,
			0f,
			PlasticityRules.PlasticityBurstCapacityQuanta,
			PlasticityRules.InitialPlasticityBudgetQuanta);
		LastPlasticityBudgetTimestampMs = double.IsFinite(LastPlasticityBudgetTimestampMs)
			? Math.Max(-1.0, LastPlasticityBudgetTimestampMs)
			: -1.0;
		TotalAbsolutePlasticityChange = double.IsFinite(TotalAbsolutePlasticityChange)
			? Math.Max(0.0, TotalAbsolutePlasticityChange)
			: 0.0;
	}

	private static float FiniteClamp(float value, float minimum, float maximum, float fallback)
		=> float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
