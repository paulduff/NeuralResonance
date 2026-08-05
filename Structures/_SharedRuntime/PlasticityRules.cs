using System;

internal static class PlasticityRules
{
	private const float MinQuanta = 0.05f;
	private const float MaxQuanta = 5f;

	public static float Stdp(float quanta)
	{
		return 0.01f * (1f - quanta / 5f);
	}

	public static float Bcm(float quanta, double activity)
	{
		return (float)(0.015 * (activity - 0.2 - (double)quanta / 10.0));
	}

	public static float DopamineStdp(float quanta, float dopamine, float rpe)
	{
		return 0.02f * (dopamine + rpe) * (1f - quanta / 5f);
	}

	public static float CerebellarLtd(float quanta)
	{
		return -0.02f * quanta;
	}

	public static float MossyFiberLtp(float quanta)
	{
		return 0.025f * (1f - quanta / 5f);
	}

	public static float SynapticTagCapture(float quanta, float acetylcholine)
	{
		return 0.01f * acetylcholine * (1f - quanta / 6f);
	}

	public static float DecayTrace(float trace, double dtMs, float tauMs)
	{
		if (Math.Abs(trace) <= 0.000001f)
		{
			return 0f;
		}
		double num = Math.Exp(0.0 - dtMs / Math.Max(1f, tauMs));
		return (float)((double)trace * num);
	}

	public static float TracePairStdp(float preTrace, float postTrace, float preImpulse, float postActivity, bool inhibitory)
	{
		float post = Math.Clamp(postActivity, 0f, 1f);
		float pre = Math.Clamp(preImpulse, 0f, 1f);
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
		float saturation = eligibility >= 0f ? 1f - quanta / MaxQuanta : quanta / MaxQuanta;
		return Math.Clamp(eligibility * Math.Clamp(saturation, 0.05f, 1f), -0.05f, 0.05f);
	}

	public static float UpdateBcmTheta(float currentTheta, float postsynActivity, double dtMs)
	{
		float num = 2500f;
		float num2 = (float)(1.0 - Math.Exp(0.0 - dtMs / (double)num));
		float num3 = postsynActivity * postsynActivity;
		return currentTheta + (num3 - currentTheta) * num2;
	}

	public static float BcmWithSlidingThreshold(float postsynActivity, float thetaM)
	{
		return 0.02f * postsynActivity * (postsynActivity - thetaM);
	}

	public static float DopamineThreeFactor(float eligibility, float dopamine, float rpe)
	{
		float teaching = Math.Clamp(rpe + dopamine - 0.42f, -1f, 1f);
		float gain = 0.20f + 0.65f * Math.Clamp(dopamine, 0f, 1f);
		return Math.Clamp(eligibility * gain * teaching, -0.06f, 0.06f);
	}

	public static float NeuromodulatedTraceDelta(float eligibility, float quanta, float dopamine, float acetylcholine, float norepinephrine, float rpe)
		=> NeuromodulatedTraceDelta(eligibility, quanta, dopamine, acetylcholine, norepinephrine, rpe, 1f);

	public static float NeuromodulatedTraceDelta(float eligibility, float quanta, float dopamine, float acetylcholine, float norepinephrine, float rpe, float microtubuleSupport)
	{
		float local = LocalTraceDelta(eligibility, quanta);
		float attentionGate = 0.35f + 0.45f * Math.Clamp(acetylcholine, 0f, 1f) + 0.20f * Math.Clamp(norepinephrine, 0f, 1f);
		float teaching = 0.25f + 0.55f * Math.Clamp(dopamine, 0f, 1f) + 0.20f * Math.Clamp(Math.Abs(rpe), 0f, 1f);
		float intracellularSupport = Math.Clamp(microtubuleSupport, 0.95f, 1.05f);
		return Math.Clamp(local * attentionGate * teaching * intracellularSupport, -0.05f, 0.05f);
	}

	public static float SynapticTagCapture(float signedTag, float quanta, float acetylcholine, float dopamine)
		=> SynapticTagCapture(signedTag, quanta, acetylcholine, dopamine, 1f);

	public static float SynapticTagCapture(float signedTag, float quanta, float acetylcholine, float dopamine, float microtubuleSupport)
	{
		float capture = Math.Clamp(acetylcholine, 0f, 1f) * (0.35f + 0.65f * Math.Clamp(dopamine, 0f, 1f));
		float intracellularSupport = Math.Clamp(microtubuleSupport, 0.95f, 1.05f);
		return LocalTraceDelta(signedTag, quanta) * 0.35f * capture * intracellularSupport;
	}

	public static float CerebellarLtdCoincidence(float quanta, bool climbingCoincident, float parallelFiberActivity)
	{
		if (!climbingCoincident || parallelFiberActivity < 0.1f)
		{
			return 0f;
		}
		return -0.03f * quanta * (0.35f + parallelFiberActivity);
	}

	public static float ClampQuanta(float quanta)
	{
		return float.IsFinite(quanta) ? Math.Clamp(quanta, MinQuanta, MaxQuanta) : 1f;
	}
}
