using System;

internal static class PlasticityRules
{
	private const float MinQuanta = 0.05f;
	private const float MaxQuanta = 5f;
	internal const float InitialPlasticityBudgetQuanta = 0.005f;
	internal const float PlasticityBurstCapacityQuanta = 0.04f;
	internal const float PlasticityRefillQuantaPerBiologicalSecond = 0.01f;
	internal const float RestoredSpikeDensityLearningScale = 0.02f;

	public static float Stdp(float quanta)
	{
		return 0.01f * (1f - ClampQuanta(quanta) / 5f);
	}

	public static float Bcm(float quanta, double activity)
	{
		var boundedActivity = double.IsFinite(activity) ? Math.Clamp(activity, 0.0, 1.0) : 0.0;
		return (float)(0.015 * (boundedActivity - 0.2 - (double)ClampQuanta(quanta) / 10.0));
	}

	public static float DopamineStdp(float quanta, float dopamine, float localTeachingSignal)
	{
		return 0.02f * (FiniteClamp(dopamine, 0f, 1f, 0f) + FiniteClamp(localTeachingSignal, -1f, 1f, 0f)) *
			(1f - ClampQuanta(quanta) / 5f);
	}

	public static float CerebellarLtd(float quanta)
	{
		return -0.02f * ClampQuanta(quanta);
	}

	public static float MossyFiberLtp(float quanta)
	{
		return 0.025f * (1f - ClampQuanta(quanta) / 5f);
	}

	public static float SynapticTagCapture(float quanta, float acetylcholine)
	{
		return 0.01f * FiniteClamp(acetylcholine, 0f, 1f, 0f) * (1f - ClampQuanta(quanta) / 6f);
	}

	public static float DecayTrace(float trace, double dtMs, float tauMs)
	{
		if (!float.IsFinite(trace))
		{
			return 0f;
		}

		if (Math.Abs(trace) <= 0.000001f)
		{
			return 0f;
		}
		var elapsedMs = double.IsFinite(dtMs) ? Math.Max(0.0, dtMs) : 0.0;
		var timeConstantMs = FiniteClamp(tauMs, 1f, float.MaxValue, 1f);
		double num = Math.Exp(0.0 - elapsedMs / timeConstantMs);
		return (float)((double)trace * num);
	}

	public static float TracePairStdp(float preTrace, float postTrace, float preImpulse, float postActivity, bool inhibitory)
	{
		float post = FiniteClamp(postActivity, 0f, 1f, 0f);
		float pre = FiniteClamp(preImpulse, 0f, 1f, 0f);
		preTrace = FiniteClamp(preTrace, 0f, 8f, 0f);
		postTrace = FiniteClamp(postTrace, 0f, 8f, 0f);
		float potentiation = 0.018f * preTrace * post;
		float depression = 0.014f * postTrace * pre;
		float delta = potentiation - depression;
		if (inhibitory)
		{
			delta = 0.012f * pre * (post - 0.22f) - 0.006f * postTrace;
		}
		return Math.Clamp(delta, -0.08f, 0.08f);
	}

	public static float LocalTraceDelta(float eligibility, float quanta)
	{
		eligibility = FiniteClamp(eligibility, -1f, 1f, 0f);
		quanta = ClampQuanta(quanta);
		float saturation = eligibility >= 0f ? 1f - quanta / MaxQuanta : quanta / MaxQuanta;
		return Math.Clamp(eligibility * Math.Clamp(saturation, 0.05f, 1f), -0.05f, 0.05f);
	}

	public static float UpdateBcmTheta(float currentTheta, float postsynActivity, double dtMs)
	{
		float num = 2500f;
		var elapsedMs = double.IsFinite(dtMs) ? Math.Max(0.0, dtMs) : 0.0;
		float num2 = (float)(1.0 - Math.Exp(0.0 - elapsedMs / (double)num));
		postsynActivity = FiniteClamp(postsynActivity, 0f, 1f, 0f);
		currentTheta = FiniteClamp(currentTheta, 0.001f, 10f, 0.2f);
		float num3 = postsynActivity * postsynActivity;
		return currentTheta + (num3 - currentTheta) * num2;
	}

	public static float BcmWithSlidingThreshold(float postsynActivity, float thetaM)
	{
		postsynActivity = FiniteClamp(postsynActivity, 0f, 1f, 0f);
		thetaM = FiniteClamp(thetaM, 0.001f, 10f, 0.2f);
		return 0.02f * postsynActivity * (postsynActivity - thetaM);
	}

	public static float DopamineThreeFactor(float eligibility, float dopamine, float localTeachingSignal)
	{
		eligibility = FiniteClamp(eligibility, -1f, 1f, 0f);
		dopamine = FiniteClamp(dopamine, 0f, 1f, 0f);
		localTeachingSignal = FiniteClamp(localTeachingSignal, -1f, 1f, 0f);
		float teaching = Math.Clamp(localTeachingSignal + dopamine - 0.42f, -1f, 1f);
		float gain = 0.20f + 0.65f * dopamine;
		return Math.Clamp(eligibility * gain * teaching, -0.06f, 0.06f);
	}

	public static float NeuromodulatedTraceDelta(float eligibility, float quanta, float dopamine, float acetylcholine, float norepinephrine, float localTeachingSignal)
		=> NeuromodulatedTraceDelta(eligibility, quanta, dopamine, acetylcholine, norepinephrine, localTeachingSignal, 1f);

	public static float NeuromodulatedTraceDelta(float eligibility, float quanta, float dopamine, float acetylcholine, float norepinephrine, float localTeachingSignal, float microtubuleSupport)
	{
		float local = LocalTraceDelta(eligibility, quanta);
		float attentionGate = 0.35f + 0.45f * FiniteClamp(acetylcholine, 0f, 1f, 0f) + 0.20f * FiniteClamp(norepinephrine, 0f, 1f, 0f);
		float teaching = 0.25f + 0.55f * FiniteClamp(dopamine, 0f, 1f, 0f) + 0.20f * FiniteClamp(Math.Abs(localTeachingSignal), 0f, 1f, 0f);
		float intracellularSupport = FiniteClamp(microtubuleSupport, 0.95f, 1.05f, 1f);
		return Math.Clamp(local * attentionGate * teaching * intracellularSupport, -0.05f, 0.05f);
	}

	public static float SynapticTagCapture(float signedTag, float quanta, float acetylcholine, float dopamine)
		=> SynapticTagCapture(signedTag, quanta, acetylcholine, dopamine, 1f);

	public static float SynapticTagCapture(float signedTag, float quanta, float acetylcholine, float dopamine, float microtubuleSupport)
	{
		float capture = FiniteClamp(acetylcholine, 0f, 1f, 0f) * (0.35f + 0.65f * FiniteClamp(dopamine, 0f, 1f, 0f));
		float intracellularSupport = FiniteClamp(microtubuleSupport, 0.95f, 1.05f, 1f);
		return LocalTraceDelta(signedTag, quanta) * 0.35f * capture * intracellularSupport;
	}

	public static float ApplyCadenceInvariantBudget(
		SynapseState synapse,
		float rawDelta,
		double biologicalTimestampMs,
		float learningScale = RestoredSpikeDensityLearningScale)
	{
		ArgumentNullException.ThrowIfNull(synapse);
		synapse.Stabilize();
		if (!float.IsFinite(rawDelta) || MathF.Abs(rawDelta) <= 0.0000001f)
		{
			return 0f;
		}

		var timestamp = double.IsFinite(biologicalTimestampMs)
			? Math.Max(0.0, biologicalTimestampMs)
			: Math.Max(0.0, synapse.LastPlasticityBudgetTimestampMs);
		if (synapse.LastPlasticityBudgetTimestampMs < 0.0)
		{
			synapse.LastPlasticityBudgetTimestampMs = timestamp;
		}
		else if (timestamp > synapse.LastPlasticityBudgetTimestampMs)
		{
			var elapsedSeconds = (timestamp - synapse.LastPlasticityBudgetTimestampMs) / 1000.0;
			synapse.PlasticityBudgetQuanta = Math.Min(
				PlasticityBurstCapacityQuanta,
				synapse.PlasticityBudgetQuanta +
					(float)(elapsedSeconds * PlasticityRefillQuantaPerBiologicalSecond));
			synapse.LastPlasticityBudgetTimestampMs = timestamp;
		}

		var boundedLearningScale = float.IsFinite(learningScale)
			? Math.Clamp(learningScale, 0f, 1f)
			: RestoredSpikeDensityLearningScale;
		var scaledDelta = rawDelta * boundedLearningScale;
		var available = Math.Clamp(
			synapse.PlasticityBudgetQuanta,
			0f,
			PlasticityBurstCapacityQuanta);
		var appliedMagnitude = Math.Min(MathF.Abs(scaledDelta), available);
		if (appliedMagnitude <= 0f)
		{
			return 0f;
		}

		var applied = MathF.CopySign(appliedMagnitude, scaledDelta);
		synapse.PlasticityBudgetQuanta = Math.Max(0f, available - appliedMagnitude);
		synapse.TotalAbsolutePlasticityChange += appliedMagnitude;
		return applied;
	}

	public static float CerebellarLtdCoincidence(float quanta, bool climbingCoincident, float parallelFiberActivity)
	{
		parallelFiberActivity = FiniteClamp(parallelFiberActivity, 0f, 1f, 0f);
		if (!climbingCoincident || parallelFiberActivity < 0.1f)
		{
			return 0f;
		}
		return -0.03f * ClampQuanta(quanta) * (0.35f + parallelFiberActivity);
	}

	public static float ClampQuanta(float quanta)
	{
		return float.IsFinite(quanta) ? Math.Clamp(quanta, MinQuanta, MaxQuanta) : 1f;
	}

	private static float FiniteClamp(float value, float minimum, float maximum, float fallback)
		=> float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
