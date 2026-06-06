using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed class StructureEngine : IStructureHost, IDisposable
{
	private readonly StructureProfile _profile;

	private readonly StructureCircuitProfile _circuit;

	private readonly ICircuitKernel _kernel;

	private readonly ConcurrentQueue<SpikeEnvelope> _feedForward = new ConcurrentQueue<SpikeEnvelope>();

	private readonly ConcurrentQueue<SpikeEnvelope> _feedback = new ConcurrentQueue<SpikeEnvelope>();

	private readonly ConcurrentQueue<SpikeMessage> _outbound = new ConcurrentQueue<SpikeMessage>();

	private readonly SynapsePersistenceStore _synapseStore;

	private readonly ModelNeuron[] _neurons;

	private readonly Dictionary<Guid, SynapseState> _inboundSynapses = new Dictionary<Guid, SynapseState>();

	private readonly Dictionary<string, SynapseState> _outboundSynapses = new Dictionary<string, SynapseState>(StringComparer.Ordinal);

	// Allocation-free lookup mirror for the outbound synapse cache. The string-keyed
	// dictionary above is preserved for the persistence store; this tuple-keyed map is
	// populated on first access of each (source, target, neuron, transmitter, feedback)
	// pair and removes the per-spike string concat that used to run inside the tick
	// loop hot path.
	private readonly Dictionary<(string sourceId, StructureId target, string targetNeuronId, NTEnum nt, bool isFeedback), SynapseState> _outboundSynapseLookup = new();

	private readonly object _stateGate = new object();

	// Dedicated gate for the diagnostic top-N scratch buffers so concurrent /top
	// callers do not race on _topRates/_topIds without blocking /tick.
	private readonly object _topGate = new object();

	private readonly DelayWindow _feedbackDelayWindow;

	private readonly float[] _topRates = new float[100];

	private readonly string[] _topIds = new string[100];

	private int _spikeInCount;

	private int _spikeOutCount;

	private int _feedForwardDepth;

	private int _feedbackDepth;

	private int _activeNeuronCount;

	private float _meanFiringRateHz;

	private float _meanActivityTrace;

	public StructureEngine(StructureProfile profile)
	{
		_profile = profile;
		_circuit = StructureCircuitProfile.For(profile.StructureId);
		_kernel = CircuitKernelFactory.For(profile.StructureId);
		_neurons = (from i in Enumerable.Range(0, _circuit.NeuronCount)
			select new ModelNeuron(i, profile.NeuronModel, _circuit)).ToArray();
		_feedbackDelayWindow = profile.FeedbackDelay;
		_synapseStore = new SynapsePersistenceStore(profile.StructureId);
		_synapseStore.Load(_inboundSynapses, _outboundSynapses);
		// Capture the handlers as fields so they can be unsubscribed in Dispose.
		// Without this, recreating an engine in-process (tests, hot reload) would
		// leak subscriptions and trigger SaveSynapseState on stale engines.
		_onProcessExit = (_, _) => SaveSynapseState();
		_onCancelKeyPress = (_, _) => SaveSynapseState();
		AppDomain.CurrentDomain.ProcessExit += _onProcessExit;
		Console.CancelKeyPress += _onCancelKeyPress;
	}

	private readonly EventHandler _onProcessExit;
	private readonly ConsoleCancelEventHandler _onCancelKeyPress;

	public ValueTask EnqueueSpikeAsync(SpikeMessage message, CancellationToken cancellationToken = default(CancellationToken))
	{
		int num = ComputeConductionDelayMs(message);
		SpikeEnvelope item = new SpikeEnvelope(message, message.TimestampMs + (double)num);
		if (message.IsFeedback)
		{
			_feedback.Enqueue(item);
			Interlocked.Increment(ref _feedbackDepth);
		}
		else
		{
			_feedForward.Enqueue(item);
			Interlocked.Increment(ref _feedForwardDepth);
		}
		Interlocked.Increment(ref _spikeInCount);
		return ValueTask.CompletedTask;
	}

	public ValueTask<TickAck> ProcessTickAsync(TickSignal tickSignal, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_stateGate)
		{
			ProcessDueQueue(_feedForward, tickSignal, isFeedback: false);
			ProcessDueQueue(_feedback, tickSignal, isFeedback: true);
			int num = 0;
			int num2 = 0;
			double num3 = 0.0;
			double num4 = 0.0;
			double stabilityTotal = 0.0;
			double spineEligibilityTotal = 0.0;
			double transportSupportTotal = 0.0;
			double opticalBiasTotal = 0.0;
			double radicalSensitivityTotal = 0.0;
			double plasticitySupportTotal = 0.0;
			double tracePersistenceSupportTotal = 0.0;
			double integrationGainTotal = 0.0;
			for (int i = 0; i < _neurons.Length; i++)
			{
				ModelNeuron modelNeuron = _neurons[i];
				if (modelNeuron.Step(tickSignal.TickDurationMs, tickSignal.GlobalNeuromodState))
				{
					num++;
					_outbound.Enqueue(BuildOutboundSpike(modelNeuron, tickSignal, modelNeuron.PreferredTarget, modelNeuron.PreferredNt, isFeedback: false));
					Interlocked.Increment(ref _spikeOutCount);
					if (_profile.StructureId == StructureId.CA3)
					{
						_outbound.Enqueue(BuildOutboundSpike(modelNeuron, tickSignal, StructureId.CA3, NTEnum.GLUTAMATE, isFeedback: true));
						Interlocked.Increment(ref _spikeOutCount);
					}
				}
				if (modelNeuron.IsActive)
				{
					num2++;
				}
				num3 += (double)modelNeuron.FiringRateHz;
				num4 += (double)modelNeuron.ActivityTrace;
				stabilityTotal += (double)modelNeuron.MicrotubuleStability;
				spineEligibilityTotal += (double)modelNeuron.MicrotubuleSpineInvasionEligibility;
				transportSupportTotal += (double)modelNeuron.MicrotubuleTransportSupport;
				opticalBiasTotal += (double)modelNeuron.MicrotubuleOpticalCollectiveBias;
				radicalSensitivityTotal += (double)modelNeuron.MicrotubuleRadicalPairSensitivity;
				plasticitySupportTotal += (double)modelNeuron.MicrotubulePlasticitySupport;
				tracePersistenceSupportTotal += (double)modelNeuron.MicrotubuleTracePersistenceSupport;
				integrationGainTotal += (double)modelNeuron.MicrotubuleIntegrationGain;
			}
			_activeNeuronCount = num2;
			_meanFiringRateHz = (float)(num3 / (double)_neurons.Length);
			_meanActivityTrace = (float)(num4 / (double)_neurons.Length);
			var neuronCount = Math.Max(1, _neurons.Length);
			var microtubules = new MicrotubuleDiagnostics(
				_neurons[0].MicrotubuleMode,
				_neurons[0].MicrotubuleEnabled,
				_neurons[0].MicrotubuleExperimental,
				(float)(stabilityTotal / neuronCount),
				(float)(spineEligibilityTotal / neuronCount),
				(float)(transportSupportTotal / neuronCount),
				(float)(opticalBiasTotal / neuronCount),
				(float)(radicalSensitivityTotal / neuronCount),
				(float)(plasticitySupportTotal / neuronCount),
				(float)(tracePersistenceSupportTotal / neuronCount),
				(float)(integrationGainTotal / neuronCount),
				(float)(plasticitySupportTotal / neuronCount));
			var bodySchema = BuildBodySchemaDiagnostics();
			var basalGanglia = BuildBasalGangliaDiagnostics(tickSignal.GlobalNeuromodState);
			var cerebellar = BuildCerebellarDiagnostics();
			var vestibuloReticular = BuildVestibuloReticularDiagnostics(tickSignal.GlobalNeuromodState);
			var superiorColliculus = BuildSuperiorColliculusDiagnostics(tickSignal.GlobalNeuromodState);
			var hippocampalSpatial = BuildHippocampalSpatialDiagnostics(tickSignal.GlobalNeuromodState);
			var salienceAffect = BuildSalienceAffectDiagnostics(tickSignal.GlobalNeuromodState);
			var prefrontalWorkingMemory = BuildPrefrontalWorkingMemoryDiagnostics(tickSignal.GlobalNeuromodState);
			var thalamicAttentionGate = BuildThalamicAttentionGateDiagnostics(tickSignal.GlobalNeuromodState);
			var hypothalamicHomeostasis = BuildHypothalamicHomeostasisDiagnostics(tickSignal.GlobalNeuromodState);
			var sleepWakeArousal = BuildSleepWakeArousalDiagnostics(tickSignal.GlobalNeuromodState);
			var descendingDefense = BuildDescendingDefenseDiagnostics(tickSignal.GlobalNeuromodState);
			var dopamineReward = BuildDopamineRewardDiagnostics(tickSignal.GlobalNeuromodState, tickSignal.RewardPredictionError);
			var septohippocampalTheta = BuildSeptohippocampalThetaDiagnostics(tickSignal.GlobalNeuromodState);
			var spinalProprioceptive = BuildSpinalProprioceptiveDiagnostics(tickSignal.GlobalNeuromodState);
			var olfactoryLimbicMemory = BuildOlfactoryLimbicMemoryDiagnostics(tickSignal.GlobalNeuromodState);
			TickAck result = new TickAck(_profile.StructureId, tickSignal.Tick, num, _meanFiringRateHz, Math.Max(0, Volatile.Read(in _feedbackDepth)), Volatile.Read(in _spikeInCount), Volatile.Read(in _spikeOutCount), _activeNeuronCount, SelectDominantRhythm(_profile.StructureId), tickSignal.GlobalNeuromodState, microtubules, bodySchema, basalGanglia, cerebellar, vestibuloReticular, superiorColliculus, hippocampalSpatial, salienceAffect, prefrontalWorkingMemory, thalamicAttentionGate, hypothalamicHomeostasis, sleepWakeArousal, descendingDefense, dopamineReward, septohippocampalTheta, spinalProprioceptive, olfactoryLimbicMemory);
			return ValueTask.FromResult(result);
		}
	}

	public async ValueTask<StructureStepResult> ProcessStepAsync(TickSignal tickSignal, int topK, CancellationToken cancellationToken = default(CancellationToken))
	{
		TickAck ack = await ProcessTickAsync(tickSignal, cancellationToken);
		IReadOnlyList<SpikeMessage> spikes = await DrainOutboundSpikesAsync(cancellationToken);
		IReadOnlyList<NeuronActivity> top = Array.Empty<NeuronActivity>();
		if (topK > 0)
		{
			top = await GetTopActiveNeuronsAsync(Math.Clamp(topK, 1, 100), cancellationToken);
		}
		return new StructureStepResult(ack, spikes, top);
	}

	public ValueTask<IReadOnlyList<SpikeMessage>> DrainOutboundSpikesAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		// _outbound is a ConcurrentQueue; no _stateGate needed. Removing the lock
		// lets HTTP /drain calls run while a tick is still computing on _stateGate.
		if (_outbound.IsEmpty)
		{
			return ValueTask.FromResult((IReadOnlyList<SpikeMessage>)Array.Empty<SpikeMessage>());
		}
		List<SpikeMessage> list = new List<SpikeMessage>(32);
		while (_outbound.TryDequeue(out SpikeMessage? result))
		{
			list.Add(result);
		}
		return ValueTask.FromResult((IReadOnlyList<SpikeMessage>)list);
	}

	public ValueTask<IReadOnlyList<NeuronActivity>> GetTopActiveNeuronsAsync(int topK, CancellationToken cancellationToken = default(CancellationToken))
	{
		// Use the small _topGate (not _stateGate) so this diagnostic snapshot does
		// not block /tick. Neuron-field reads may briefly return values from a tick
		// that is still in flight; for a top-N firing-rate list this is acceptable.
		lock (_topGate)
		{
			topK = Math.Clamp(topK, 1, Math.Min(100, _neurons.Length));
			Array.Clear(_topRates, 0, topK);
			Array.Clear(_topIds, 0, topK);
			int num = 0;
			for (int i = 0; i < _neurons.Length; i++)
			{
				ModelNeuron modelNeuron = _neurons[i];
				float firingRateHz = modelNeuron.FiringRateHz;
				if (num != topK || !(firingRateHz <= _topRates[topK - 1]))
				{
					int num2 = Math.Min(num, topK - 1);
					while (num2 > 0 && firingRateHz > _topRates[num2 - 1])
					{
						_topRates[num2] = _topRates[num2 - 1];
						_topIds[num2] = _topIds[num2 - 1];
						num2--;
					}
					_topRates[num2] = firingRateHz;
					_topIds[num2] = modelNeuron.Id;
					if (num < topK)
					{
						num++;
					}
				}
			}
			List<NeuronActivity> list = new List<NeuronActivity>(num);
			for (int j = 0; j < num; j++)
			{
				list.Add(new NeuronActivity(_topIds[j], _topRates[j]));
			}
			return ValueTask.FromResult((IReadOnlyList<NeuronActivity>)list);
		}
	}

	private void ProcessDueQueue(ConcurrentQueue<SpikeEnvelope> queue, TickSignal tickSignal, bool isFeedback)
	{
		SpikeEnvelope result;
		while (queue.TryPeek(out result) && result.DeliverAtTimestampMs <= tickSignal.TimestampMs)
		{
			if (queue.TryDequeue(out SpikeEnvelope result2))
			{
				if (isFeedback)
				{
					Interlocked.Decrement(ref _feedbackDepth);
				}
				else
				{
					Interlocked.Decrement(ref _feedForwardDepth);
				}
				ModelNeuron modelNeuron = SelectInboundNeuron(result2.Message);
				modelNeuron.Integrate(result2.Message, tickSignal.TickDurationMs);
				ApplyPlasticity(result2.Message, modelNeuron.Index, modelNeuron.ActivityTrace, modelNeuron.MicrotubulePlasticitySupport * modelNeuron.CalciumPlasticitySupport, modelNeuron.MicrotubuleTracePersistenceSupport, tickSignal.TimestampMs, tickSignal.GlobalNeuromodState, tickSignal.RewardPredictionError);
			}
		}
	}

	private ModelNeuron SelectInboundNeuron(SpikeMessage message)
	{
		int num = ResolveInboundNeuronIndex(message);
		return _neurons[num];
	}

	private int ResolveInboundNeuronIndex(SpikeMessage message)
	{
		return _kernel.ResolveInboundNeuronIndex(message, _neurons.Length, _circuit);
	}

	private void ApplyPlasticity(SpikeMessage message, int targetNeuronIndex, float postsynActivity, float microtubulePlasticitySupport, float microtubuleTracePersistenceSupport, double timestampMs, NeuromodState neuromod, float rewardPredictionError)
	{
		if (!_inboundSynapses.TryGetValue(message.SynapseId, out SynapseState value))
		{
			value = new SynapseState(message.SynapseId, message.Neurotransmitter, Math.Clamp(message.VesicleQuanta, 0.05f, 5f));
			_inboundSynapses[message.SynapseId] = value;
		}
		double num = Math.Max(0.0, timestampMs - value.LastUpdateTimestampMs);
		float traceDelta = UpdateTraceState(value, num, 1f, postsynActivity, message.Neurotransmitter, microtubuleTracePersistenceSupport);
		value.ThetaM = PlasticityRules.UpdateBcmTheta(value.ThetaM, postsynActivity, num);
		value.LastUpdateTimestampMs = timestampMs;
		value.LastTargetNeuronIndex = targetNeuronIndex;
		bool climbingCoincident = _profile.StructureId == StructureId.PurkinjeCellLayer && (message.SourceStructure == StructureId.InferiorOlive || message.SpikeType == SpikeTypeEnum.COMPLEX);
		float delta = ComputeInboundPlasticityDelta(value, traceDelta, postsynActivity, microtubulePlasticitySupport, neuromod, rewardPredictionError, climbingCoincident);
		value.VesicleQuanta = PlasticityRules.ClampQuanta(value.VesicleQuanta + delta);
		PersistSynapses(timestampMs);
	}

	private static float UpdateTraceState(SynapseState synapse, double dtMs, float preImpulse, float postActivity, NTEnum neurotransmitter, float microtubuleTracePersistenceSupport)
	{
		synapse.PreTrace = PlasticityRules.DecayTrace(synapse.PreTrace, dtMs, 20f);
		synapse.PostTrace = PlasticityRules.DecayTrace(synapse.PostTrace, dtMs, 35f);
		float traceSupport = Math.Clamp(microtubuleTracePersistenceSupport, 0.97f, 1.03f);
		synapse.EligibilityTrace = PlasticityRules.DecayTrace(synapse.EligibilityTrace, dtMs, 900f * traceSupport);
		synapse.SynapticTagTrace = PlasticityRules.DecayTrace(synapse.SynapticTagTrace, dtMs, 8000f * traceSupport);
		float boundedPost = Math.Clamp(postActivity, 0f, 1f);
		bool inhibitory = neurotransmitter == NTEnum.GABA;
		float traceDelta = PlasticityRules.TracePairStdp(synapse.PreTrace, synapse.PostTrace, preImpulse, boundedPost, inhibitory);
		synapse.PreTrace = Math.Clamp(synapse.PreTrace + preImpulse, 0f, 8f);
		synapse.PostTrace = Math.Clamp(synapse.PostTrace + boundedPost, 0f, 8f);
		synapse.EligibilityTrace = Math.Clamp(synapse.EligibilityTrace + traceDelta, -1f, 1f);
		synapse.SynapticTagTrace = Math.Clamp(synapse.SynapticTagTrace + traceDelta, -1f, 1f);
		return traceDelta;
	}

	private float ComputeInboundPlasticityDelta(SynapseState synapse, float traceDelta, float postsynActivity, float microtubulePlasticitySupport, NeuromodState neuromod, float rewardPredictionError, bool climbingCoincident)
	{
		float traceConsolidation = PlasticityRules.NeuromodulatedTraceDelta(synapse.EligibilityTrace, synapse.VesicleQuanta, neuromod.DopamineLevel, neuromod.AcetylcholineLevel, neuromod.NorepinephrineLevel, rewardPredictionError, microtubulePlasticitySupport);
		float tagCapture = PlasticityRules.SynapticTagCapture(synapse.SynapticTagTrace, synapse.VesicleQuanta, neuromod.AcetylcholineLevel, neuromod.DopamineLevel, microtubulePlasticitySupport);
		float vesicleQuanta = synapse.VesicleQuanta;
		return _profile.PlasticityRule switch
		{
			"BCM" => PlasticityRules.BcmWithSlidingThreshold(postsynActivity, synapse.ThetaM) + PlasticityRules.LocalTraceDelta(traceDelta, vesicleQuanta) * 0.35f,
			"DopamineModulatedSTDP" => traceConsolidation + PlasticityRules.DopamineThreeFactor(synapse.EligibilityTrace, neuromod.DopamineLevel, rewardPredictionError) * 0.25f,
			"DopamineModulatedSTDP+SynapticTaggingCapture" => traceConsolidation + tagCapture,
			"CerebellarLTD" => PlasticityRules.CerebellarLtdCoincidence(vesicleQuanta, climbingCoincident, postsynActivity),
			"MossyFiberLTP" => PlasticityRules.MossyFiberLtp(vesicleQuanta) * 0.35f + PlasticityRules.LocalTraceDelta(traceDelta, vesicleQuanta),
			"SynapticTaggingCapture" => tagCapture,
			"STDP+SynapticTaggingCapture" => PlasticityRules.LocalTraceDelta(traceDelta, vesicleQuanta) + tagCapture,
			"DopamineHomeostasis" => traceConsolidation * 0.5f + PlasticityRules.DopamineThreeFactor(synapse.EligibilityTrace * 0.5f, neuromod.DopamineLevel, rewardPredictionError * 0.5f) * 0.25f,
			"HomeostaticGain" => PlasticityRules.BcmWithSlidingThreshold(postsynActivity, synapse.ThetaM),
			"BCM+STDP" => PlasticityRules.BcmWithSlidingThreshold(postsynActivity, synapse.ThetaM) + PlasticityRules.LocalTraceDelta(traceDelta, vesicleQuanta),
			_ => PlasticityRules.LocalTraceDelta(traceDelta, vesicleQuanta),
		};
	}

	private float ComputeOutboundPlasticityDelta(SynapseState synapse, float sourceActivity, NeuromodState neuromod, float rewardPredictionError, double timestampMs)
	{
		double num = Math.Max(0.0, timestampMs - synapse.LastUpdateTimestampMs);
		float traceDelta = UpdateTraceState(synapse, num, 1f, sourceActivity * 0.35f, synapse.Neurotransmitter, 1f);
		synapse.ThetaM = PlasticityRules.UpdateBcmTheta(synapse.ThetaM, sourceActivity, num);
		synapse.LastUpdateTimestampMs = timestampMs;
		float microtubulePlasticitySupport = Math.Clamp(0.98f + sourceActivity * 0.04f, 0.98f, 1.02f);
		float traceConsolidation = PlasticityRules.NeuromodulatedTraceDelta(synapse.EligibilityTrace, synapse.VesicleQuanta, neuromod.DopamineLevel, neuromod.AcetylcholineLevel, neuromod.NorepinephrineLevel, rewardPredictionError, microtubulePlasticitySupport);
		float tagCapture = PlasticityRules.SynapticTagCapture(synapse.SynapticTagTrace, synapse.VesicleQuanta, neuromod.AcetylcholineLevel, neuromod.DopamineLevel, microtubulePlasticitySupport);
		return _profile.PlasticityRule switch
		{
			"DopamineModulatedSTDP" => traceConsolidation + PlasticityRules.DopamineThreeFactor(synapse.EligibilityTrace, neuromod.DopamineLevel, rewardPredictionError) * 0.25f,
			"DopamineModulatedSTDP+SynapticTaggingCapture" => traceConsolidation + tagCapture,
			"BCM" => PlasticityRules.BcmWithSlidingThreshold(sourceActivity, synapse.ThetaM) + PlasticityRules.LocalTraceDelta(traceDelta, synapse.VesicleQuanta) * 0.35f,
			"BCM+STDP" => PlasticityRules.BcmWithSlidingThreshold(sourceActivity, synapse.ThetaM) + PlasticityRules.LocalTraceDelta(traceDelta, synapse.VesicleQuanta),
			"SynapticTaggingCapture" => tagCapture,
			"STDP+SynapticTaggingCapture" => PlasticityRules.LocalTraceDelta(traceDelta, synapse.VesicleQuanta) + tagCapture,
			_ => PlasticityRules.LocalTraceDelta(traceDelta, synapse.VesicleQuanta),
		};
	}

	private SynapseState GetOrCreateOutboundSynapse(ModelNeuron source, StructureId target, NTEnum neurotransmitter, bool isFeedback, string targetNeuronId)
	{
		var lookupKey = (source.Id, target, targetNeuronId, neurotransmitter, isFeedback);
		if (_outboundSynapseLookup.TryGetValue(lookupKey, out SynapseState? cached))
		{
			return cached;
		}

		// Cache miss: build the persistence-format key once. For synapses loaded from
		// disk into _outboundSynapses, this also promotes them into the lookup cache so
		// subsequent spikes hit the allocation-free path above.
		string key = source.Id + "|" + target + "|" + targetNeuronId + "|" + neurotransmitter + "|" + (isFeedback ? "F" : "N");
		if (_outboundSynapses.TryGetValue(key, out SynapseState? loaded))
		{
			_outboundSynapseLookup[lookupKey] = loaded;
			return loaded;
		}

		float baselineQuanta = Math.Clamp(1f + source.ActivityTrace * 0.5f, 0.05f, 5f);
		SynapseState synapseState = new SynapseState(Guid.NewGuid(), neurotransmitter, baselineQuanta);
		_outboundSynapses[key] = synapseState;
		_outboundSynapseLookup[lookupKey] = synapseState;
		return synapseState;
	}

	private SpikeMessage BuildOutboundSpike(ModelNeuron source, TickSignal tickSignal, StructureId target, NTEnum neurotransmitter, bool isFeedback)
	{
		int num = _kernel.ResolveOutboundTargetIndex(source, target, _circuit);
		string targetNeuronId = $"auto-{target}-{num:000}";
		SynapseState synapseState = GetOrCreateOutboundSynapse(source, target, neurotransmitter, isFeedback, targetNeuronId);
		float outboundDelta = ComputeOutboundPlasticityDelta(synapseState, source.ActivityTrace, tickSignal.GlobalNeuromodState, tickSignal.RewardPredictionError, tickSignal.TimestampMs);
		synapseState.VesicleQuanta = PlasticityRules.ClampQuanta(synapseState.VesicleQuanta + outboundDelta);
		PersistSynapses(tickSignal.TimestampMs);
		SpikeMessage spikeMessage = new SpikeMessage();
		spikeMessage.MessageId = Guid.NewGuid();
		spikeMessage.TimestampMs = tickSignal.TimestampMs;
		spikeMessage.SourceStructure = _profile.StructureId;
		spikeMessage.TargetStructure = target;
		spikeMessage.SourceNeuronId = source.Id;
		spikeMessage.TargetNeuronId = targetNeuronId;
		spikeMessage.SynapseId = synapseState.SynapseId;
		spikeMessage.Neurotransmitter = neurotransmitter;
		spikeMessage.VesicleQuanta = Math.Max(0.05f, synapseState.VesicleQuanta);
		SpikeMessage spikeMessage2 = spikeMessage;
		float reuptakeRate = neurotransmitter switch
		{
			NTEnum.DOPAMINE => 40f, 
			NTEnum.SEROTONIN => 50f, 
			NTEnum.ACETYLCHOLINE => 20f, 
			NTEnum.NOREPINEPHRINE => 30f, 
			NTEnum.GABA => 12f, 
			_ => 8f, 
		};
		spikeMessage2.ReuptakeRate = reuptakeRate;
		spikeMessage.SpikeType = _kernel.SelectSpikeType(_profile.StructureId, isFeedback, tickSignal);
		spikeMessage.IsFeedback = isFeedback;
		spikeMessage.ModulationContext = tickSignal.GlobalNeuromodState;
		return spikeMessage;
	}

	private void PersistSynapses(double timestampMs)
	{
		_synapseStore.MarkChanged(_inboundSynapses, _outboundSynapses, timestampMs);
	}

	private void SaveSynapseState()
	{
		lock (_stateGate)
		{
			_synapseStore.Save(_inboundSynapses, _outboundSynapses);
		}
	}

	public void Dispose()
	{
		AppDomain.CurrentDomain.ProcessExit -= _onProcessExit;
		Console.CancelKeyPress -= _onCancelKeyPress;
		SaveSynapseState();
		_synapseStore.Dispose();
	}

	private static BrainRhythm SelectDominantRhythm(StructureId structureId)
	{
		BrainRhythm result;
		switch (structureId)
		{
		case StructureId.OlfactoryBulb:
			result = BrainRhythm.GAMMA;
			break;
		case StructureId.EntorhinalCortex:
		case StructureId.DentateGyrus:
		case StructureId.CA3:
		case StructureId.CA1:
		case StructureId.Subiculum:
			result = BrainRhythm.THETA;
			break;
		case StructureId.Pfc:
		case StructureId.Ppc:
			result = BrainRhythm.BETA;
			break;
		case StructureId.M1:
		case StructureId.Sma:
			result = BrainRhythm.BETA;
			break;
		default:
			result = BrainRhythm.ALPHA;
			break;
		}
		return result;
	}

	private BodySchemaDiagnostics? BuildBodySchemaDiagnostics()
	{
		if (_profile.StructureId != StructureId.M1 &&
			_profile.StructureId != StructureId.S1 &&
			_profile.StructureId != StructureId.Ppc)
		{
			return null;
		}

		var bodySums = new float[4];
		var bodyCounts = new int[4];
		var spatialSums = new float[4];
		var spatialCounts = new int[4];
		var count = Math.Max(1, _neurons.Length);

		for (var i = 0; i < _neurons.Length; i++)
		{
			var rate = _neurons[i].FiringRateHz;
			var bodyZone = _profile.StructureId == StructureId.Ppc
				? ResolvePpcBodyZone(i, count)
				: ResolveHomuncularBodyZone(i, count);
			bodySums[bodyZone] += rate;
			bodyCounts[bodyZone]++;

			if (_profile.StructureId == StructureId.Ppc)
			{
				var spatialZone = ResolvePpcSpatialZone(i, count);
				spatialSums[spatialZone] += rate;
				spatialCounts[spatialZone]++;
			}
		}

		var bodyActivations = AverageChannels(bodySums, bodyCounts);
		var spatialActivations = AverageChannels(spatialSums, spatialCounts);
		var dominantSpatialZone = _profile.StructureId == StructureId.Ppc
			? SelectDominantName(spatialActivations, SpatialZoneNames)
			: "Somatotopic";

		return new BodySchemaDiagnostics(
			SelectDominantName(bodyActivations, BodyZoneNames),
			dominantSpatialZone,
			bodyActivations[0],
			bodyActivations[1],
			bodyActivations[2],
			bodyActivations[3],
			spatialActivations[0],
			spatialActivations[1],
			spatialActivations[2],
			spatialActivations[3]);
	}

	private static int ResolveHomuncularBodyZone(int index, int neuronCount)
	{
		var handStart = ScaleHomuncularBoundary(96, neuronCount);
		var trunkStart = ScaleHomuncularBoundary(224, neuronCount);
		var legStart = ScaleHomuncularBoundary(288, neuronCount);
		if (index < handStart)
		{
			return 0;
		}

		if (index < trunkStart)
		{
			return 1;
		}

		return index < legStart ? 2 : 3;
	}

	private static int ResolvePpcBodyZone(int index, int neuronCount)
		=> Math.Clamp((int)((long)index * 16L / Math.Max(1, neuronCount)) / 4, 0, 3);

	private static int ResolvePpcSpatialZone(int index, int neuronCount)
		=> Math.Clamp((int)((long)index * 16L / Math.Max(1, neuronCount)) % 4, 0, 3);

	private static int ScaleHomuncularBoundary(int canonicalIndex, int neuronCount)
		=> Math.Clamp((int)MathF.Round(canonicalIndex / 384f * Math.Max(1, neuronCount)), 1, Math.Max(1, neuronCount));

	private static float[] AverageChannels(float[] sums, int[] counts)
	{
		var averages = new float[sums.Length];
		for (var i = 0; i < sums.Length; i++)
		{
			averages[i] = counts[i] > 0 ? sums[i] / counts[i] : 0f;
		}

		return averages;
	}

	private static string SelectDominantName(float[] activations, string[] names)
	{
		var best = 0;
		for (var i = 1; i < activations.Length && i < names.Length; i++)
		{
			if (activations[i] > activations[best])
			{
				best = i;
			}
		}

		return names[Math.Clamp(best, 0, names.Length - 1)];
	}

	private static readonly string[] BodyZoneNames = ["FaceHead", "HandArm", "Trunk", "LegFoot"];
	private static readonly string[] SpatialZoneNames = ["NearBody", "LeftPeripersonal", "RightPeripersonal", "FarSpace"];

	private BasalGangliaDiagnostics? BuildBasalGangliaDiagnostics(NeuromodState neuromod)
	{
		if (!IsBasalGangliaDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var dopamine = Math.Clamp(neuromod.DopamineLevel, 0f, 1f);
		var direct = 0f;
		var indirect = 0f;
		var hyperdirect = 0f;
		var outputInhibition = 0f;
		var dopamineModulation = dopamine;

		switch (_profile.StructureId)
		{
		case StructureId.Striatum:
		case StructureId.NucleusAccumbens:
		{
			var d1 = AveragePartitionRate(0, 2);
			var d2 = AveragePartitionRate(1, 2);
			direct = d1 * (0.75f + dopamine);
			indirect = d2 * (1.25f - (dopamine * 0.50f));
			break;
		}
		case StructureId.GPe:
		case StructureId.GlobusPallidus:
		case StructureId.VentralPallidum:
			indirect = mean;
			break;
		case StructureId.Stn:
			hyperdirect = mean;
			break;
		case StructureId.GPi:
		case StructureId.Snr:
			outputInhibition = mean;
			break;
		case StructureId.Snc:
			dopamineModulation = Math.Clamp(dopamine + (mean / 50f), 0f, 2f);
			break;
		}

		var thalamicDisinhibition = Math.Max(0f, direct - (outputInhibition * 0.50f));
		var bias = direct - Math.Max(indirect, hyperdirect);
		return new BasalGangliaDiagnostics(
			SelectBasalGangliaMode(direct, indirect, hyperdirect, outputInhibition),
			direct,
			indirect,
			hyperdirect,
			outputInhibition,
			thalamicDisinhibition,
			dopamineModulation,
			bias);
	}

	private float AveragePartitionRate(int partition, int partitionCount)
	{
		var total = 0f;
		var count = 0;
		var safePartitionCount = Math.Max(1, partitionCount);
		for (var i = 0; i < _neurons.Length; i++)
		{
			if (i % safePartitionCount != partition)
			{
				continue;
			}

			total += _neurons[i].FiringRateHz;
			count++;
		}

		return count > 0 ? total / count : 0f;
	}

	private static bool IsBasalGangliaDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.Striatum
			or StructureId.NucleusAccumbens
			or StructureId.GlobusPallidus
			or StructureId.VentralPallidum
			or StructureId.GPe
			or StructureId.GPi
			or StructureId.Stn
			or StructureId.Snr
			or StructureId.Snc;

	private static string SelectBasalGangliaMode(float direct, float indirect, float hyperdirect, float outputInhibition)
	{
		var suppressive = Math.Max(indirect, Math.Max(hyperdirect, outputInhibition));
		if (direct > suppressive * 1.15f && direct > 0.05f)
		{
			return "Go";
		}

		if (hyperdirect > Math.Max(direct, indirect) * 1.10f && hyperdirect > 0.05f)
		{
			return "Stop";
		}

		return "Hold";
	}

	private CerebellarDiagnostics? BuildCerebellarDiagnostics()
	{
		if (!IsCerebellarDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var mossy = 0f;
		var climbing = 0f;
		var purkinje = 0f;
		var dcn = 0f;
		var vermis = 0f;

		switch (_profile.StructureId)
		{
		case StructureId.CerebellarGranule:
			mossy = mean;
			break;
		case StructureId.CerebellarLobules:
			mossy = mean * 0.65f;
			dcn = mean * 0.20f;
			break;
		case StructureId.CerebellarVermis:
			mossy = mean * 0.35f;
			vermis = mean;
			dcn = mean * 0.15f;
			break;
		case StructureId.PurkinjeCellLayer:
			purkinje = mean;
			break;
		case StructureId.DeepCerebellarNuclei:
			dcn = mean;
			break;
		case StructureId.InferiorOlive:
			climbing = mean;
			break;
		}

		var correctionGain = Math.Max(0f, dcn + (climbing * 0.45f) + (vermis * 0.20f) - (purkinje * 0.35f));
		var predictionError = climbing;
		return new CerebellarDiagnostics(
			SelectCerebellarCorrectionMode(mossy, climbing, purkinje, dcn, correctionGain),
			mossy,
			climbing,
			purkinje,
			dcn,
			vermis,
			correctionGain,
			predictionError);
	}

	private static bool IsCerebellarDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.CerebellarGranule
			or StructureId.CerebellarVermis
			or StructureId.CerebellarLobules
			or StructureId.PurkinjeCellLayer
			or StructureId.DeepCerebellarNuclei
			or StructureId.InferiorOlive;

	private static string SelectCerebellarCorrectionMode(float mossy, float climbing, float purkinje, float dcn, float correctionGain)
	{
		if (climbing > Math.Max(0.20f, mossy * 0.85f) && dcn > purkinje * 1.15f)
		{
			return "Overcorrecting";
		}

		if (climbing > 0.05f || correctionGain > Math.Max(0.08f, purkinje * 0.35f))
		{
			return "Correcting";
		}

		return "Stable";
	}

	private VestibuloReticularDiagnostics? BuildVestibuloReticularDiagnostics(NeuromodState neuromod)
	{
		if (!IsVestibuloReticularDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var vestibular = 0f;
		var reticular = 0f;
		var vermis = 0f;
		var spinalTone = 0f;

		switch (_profile.StructureId)
		{
		case StructureId.VestibularNuclei:
			vestibular = mean;
			break;
		case StructureId.ReticularFormation:
			reticular = mean * (0.80f + Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.55f);
			break;
		case StructureId.CerebellarVermis:
			vermis = mean;
			break;
		case StructureId.SpinalCordMotor:
			spinalTone = mean;
			break;
		}

		var balanceError = Math.Max(0f, vestibular - ((vermis * 0.55f) + (spinalTone * 0.25f)));
		var postureStability = Math.Clamp((vermis * 0.35f) + (spinalTone * 0.30f) + (reticular * 0.20f) - (balanceError * 0.25f), 0f, 120f);
		return new VestibuloReticularDiagnostics(
			SelectVestibuloReticularMode(vestibular, reticular, vermis, spinalTone, balanceError),
			vestibular,
			reticular,
			vermis,
			spinalTone,
			postureStability,
			balanceError);
	}

	private static bool IsVestibuloReticularDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.VestibularNuclei
			or StructureId.ReticularFormation
			or StructureId.CerebellarVermis
			or StructureId.SpinalCordMotor;

	private static string SelectVestibuloReticularMode(float vestibular, float reticular, float vermis, float spinalTone, float balanceError)
	{
		if (balanceError > Math.Max(0.20f, vermis * 0.75f))
		{
			return "Rebalancing";
		}

		if (reticular > Math.Max(0.18f, spinalTone * 1.20f))
		{
			return "Aroused";
		}

		return "Steady";
	}

	private SuperiorColliculusDiagnostics? BuildSuperiorColliculusDiagnostics(NeuromodState neuromod)
	{
		if (!IsSuperiorColliculusDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var visual = 0f;
		var auditory = 0f;
		var nigrotectal = 0f;
		var pulvinar = 0f;
		var headEye = 0f;
		var salienceGain = 0.75f +
			(Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.20f) +
			(Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.30f);

		switch (_profile.StructureId)
		{
		case StructureId.Retina:
		case StructureId.V1:
		case StructureId.Mt:
			visual = mean;
			break;
		case StructureId.InferiorColliculus:
			auditory = mean;
			break;
		case StructureId.SuperiorColliculus:
			visual = mean * 0.55f;
			auditory = mean * 0.30f;
			headEye = mean * 0.70f;
			break;
		case StructureId.Snr:
			nigrotectal = mean;
			break;
		case StructureId.Pulvinar:
			pulvinar = mean;
			break;
		case StructureId.PremotorCortex:
		case StructureId.Pons:
			headEye = mean * 0.65f;
			break;
		}

		var sensoryDrive = ((visual * 0.65f) + (auditory * 0.45f)) * salienceGain;
		var saccadeReadiness = Math.Max(0f, sensoryDrive + (pulvinar * 0.25f) + (headEye * 0.35f) - (nigrotectal * 0.50f));
		var salienceBias = Math.Max(0f, sensoryDrive + (pulvinar * 0.20f) - (nigrotectal * 0.25f));
		return new SuperiorColliculusDiagnostics(
			SelectSuperiorColliculusOrientingMode(saccadeReadiness, sensoryDrive, nigrotectal, headEye),
			visual,
			auditory,
			nigrotectal,
			pulvinar,
			headEye,
			saccadeReadiness,
			salienceBias);
	}

	private static bool IsSuperiorColliculusDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.Retina
			or StructureId.V1
			or StructureId.Mt
			or StructureId.InferiorColliculus
			or StructureId.SuperiorColliculus
			or StructureId.Snr
			or StructureId.Pulvinar
			or StructureId.PremotorCortex
			or StructureId.Pons;

	private static string SelectSuperiorColliculusOrientingMode(float saccadeReadiness, float sensoryDrive, float nigrotectal, float headEye)
	{
		if (nigrotectal > Math.Max(sensoryDrive, headEye) * 1.20f && nigrotectal > 0.10f)
		{
			return "Suppressed";
		}

		if (saccadeReadiness > Math.Max(0.20f, nigrotectal * 0.80f) && headEye > 0.05f)
		{
			return "Orienting";
		}

		if (sensoryDrive > 0.08f)
		{
			return "Primed";
		}

		return "Holding";
	}

	private HippocampalSpatialDiagnostics? BuildHippocampalSpatialDiagnostics(NeuromodState neuromod)
	{
		if (!IsHippocampalSpatialDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var entorhinal = 0f;
		var dentate = 0f;
		var ca3 = 0f;
		var ca1 = 0f;
		var subicular = 0f;
		var headDirection = 0f;
		var noveltyGain = 0.80f +
			(Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.30f) +
			(Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.20f);

		switch (_profile.StructureId)
		{
		case StructureId.EntorhinalCortex:
			entorhinal = mean;
			break;
		case StructureId.DentateGyrus:
			dentate = mean;
			break;
		case StructureId.CA3:
			ca3 = mean;
			break;
		case StructureId.CA2:
			ca3 = mean * 0.35f;
			ca1 = mean * 0.25f;
			headDirection = mean * 0.20f;
			break;
		case StructureId.CA1:
			ca1 = mean;
			break;
		case StructureId.Subiculum:
			subicular = mean;
			headDirection = mean * 0.20f;
			break;
		case StructureId.Presubiculum:
		case StructureId.Parasubiculum:
			headDirection = mean;
			entorhinal = mean * 0.30f;
			break;
		case StructureId.RetrosplenialCortex:
			headDirection = mean * 0.55f;
			break;
		case StructureId.Ppc:
			entorhinal = mean * 0.25f;
			break;
		case StructureId.VestibularNuclei:
			headDirection = mean * 0.45f;
			break;
		}

		var novelty = Math.Max(0f, ((entorhinal + dentate) * 0.50f * noveltyGain) - ((ca3 + ca1 + subicular) * 0.25f));
		var coherence = Math.Clamp((ca1 * 0.30f) + (subicular * 0.25f) + (headDirection * 0.25f) + (ca3 * 0.15f) - (novelty * 0.20f), 0f, 120f);
		return new HippocampalSpatialDiagnostics(
			SelectHippocampalSpatialMode(novelty, coherence, ca3, ca1, subicular),
			entorhinal,
			dentate,
			ca3,
			ca1,
			subicular,
			headDirection,
			coherence,
			novelty);
	}

	private static bool IsHippocampalSpatialDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.EntorhinalCortex
			or StructureId.DentateGyrus
			or StructureId.CA3
			or StructureId.CA2
			or StructureId.CA1
			or StructureId.Subiculum
			or StructureId.Presubiculum
			or StructureId.Parasubiculum
			or StructureId.RetrosplenialCortex
			or StructureId.Ppc
			or StructureId.VestibularNuclei;

	private static string SelectHippocampalSpatialMode(float novelty, float coherence, float ca3, float ca1, float subicular)
	{
		if (novelty > Math.Max(0.20f, coherence * 0.55f))
		{
			return "Encoding";
		}

		if (ca3 > 0.05f && ca1 > 0.05f && ca3 > novelty * 0.85f)
		{
			return "Recalling";
		}

		if (coherence > Math.Max(0.15f, subicular * 0.35f))
		{
			return "Aligned";
		}

		return "Searching";
	}

	private SalienceAffectDiagnostics? BuildSalienceAffectDiagnostics(NeuromodState neuromod)
	{
		if (!IsSalienceAffectDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var threat = 0f;
		var interoception = 0f;
		var conflict = 0f;
		var arousal = 0f;
		var attention = 0f;
		var defensive = 0f;
		var control = 0f;
		var neGain = 0.80f + (Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.45f);
		var achGain = 0.85f + (Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.35f);

		switch (_profile.StructureId)
		{
		case StructureId.Amygdala:
			threat = mean * neGain;
			defensive = mean * 0.55f;
			break;
		case StructureId.Insula:
			interoception = mean;
			threat = mean * 0.20f;
			break;
		case StructureId.Acc:
			conflict = mean;
			control = mean * 0.45f;
			break;
		case StructureId.Hypothalamus:
			arousal = mean * 0.70f;
			interoception = mean * 0.25f;
			break;
		case StructureId.LocusCoeruleus:
			arousal = mean * neGain;
			threat = mean * 0.20f;
			break;
		case StructureId.BasalForebrain:
			attention = mean * achGain;
			break;
		case StructureId.NucleusAccumbens:
			threat = mean * 0.15f;
			control = mean * 0.30f;
			break;
		case StructureId.Pfc:
			control = mean;
			break;
		case StructureId.PeriaqueductalGray:
			defensive = mean;
			break;
		}

		var affect = Math.Max(threat, interoception) + (arousal * 0.35f) + (conflict * 0.25f);
		var controlBias = Math.Max(0f, control + (attention * 0.30f) - ((threat + defensive) * 0.25f));
		return new SalienceAffectDiagnostics(
			SelectSalienceAffectMode(threat, interoception, conflict, defensive, controlBias),
			threat,
			interoception,
			conflict,
			arousal,
			attention,
			defensive,
			controlBias,
			affect);
	}

	private static bool IsSalienceAffectDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.Amygdala
			or StructureId.Insula
			or StructureId.Acc
			or StructureId.Hypothalamus
			or StructureId.LocusCoeruleus
			or StructureId.BasalForebrain
			or StructureId.NucleusAccumbens
			or StructureId.Pfc
			or StructureId.PeriaqueductalGray;

	private static string SelectSalienceAffectMode(float threat, float interoception, float conflict, float defensive, float controlBias)
	{
		if (defensive > Math.Max(0.20f, controlBias * 1.20f))
		{
			return "Defensive";
		}

		if (threat > Math.Max(interoception, conflict) * 1.15f && threat > 0.08f)
		{
			return "Threat";
		}

		if (interoception > Math.Max(threat, conflict) * 1.10f && interoception > 0.08f)
		{
			return "Interoceptive";
		}

		if (conflict > 0.08f)
		{
			return "Conflict";
		}

		return "Monitoring";
	}

	private PrefrontalWorkingMemoryDiagnostics? BuildPrefrontalWorkingMemoryDiagnostics(NeuromodState neuromod)
	{
		if (!IsPrefrontalWorkingMemoryDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var pfc = 0f;
		var md = 0f;
		var frontoparietal = 0f;
		var semantic = 0f;
		var striatalGate = 0f;
		var accDemand = 0f;
		var dopamineGate = 0.80f + (Math.Clamp(neuromod.DopamineLevel, 0f, 1f) * 0.35f);
		var attentionGain = 0.85f +
			(Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.25f) +
			(Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.20f);

		switch (_profile.StructureId)
		{
		case StructureId.Pfc:
			pfc = mean * attentionGain;
			break;
		case StructureId.MediodorsalThalamus:
			md = mean;
			break;
		case StructureId.Ppc:
			frontoparietal = mean;
			break;
		case StructureId.TemporalAssociation:
			semantic = mean;
			break;
		case StructureId.Striatum:
			striatalGate = mean * dopamineGate;
			break;
		case StructureId.Acc:
			accDemand = mean;
			break;
		case StructureId.OrbitofrontalCortex:
			pfc = mean * 0.25f;
			semantic = mean * 0.25f;
			break;
		case StructureId.BasalForebrain:
			pfc = mean * 0.20f;
			break;
		case StructureId.LocusCoeruleus:
			accDemand = mean * 0.25f;
			break;
		}

		var topDown = Math.Max(0f, pfc + (md * 0.35f) + (frontoparietal * 0.25f) + (semantic * 0.20f) - (accDemand * 0.15f));
		var stability = Math.Clamp((pfc * 0.35f) + (md * 0.25f) + (striatalGate * 0.20f) + (frontoparietal * 0.15f) - (accDemand * 0.10f), 0f, 120f);
		return new PrefrontalWorkingMemoryDiagnostics(
			SelectPrefrontalWorkingMemoryMode(stability, striatalGate, accDemand, topDown),
			pfc,
			md,
			frontoparietal,
			semantic,
			striatalGate,
			accDemand,
			topDown,
			stability);
	}

	private static bool IsPrefrontalWorkingMemoryDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.Pfc
			or StructureId.MediodorsalThalamus
			or StructureId.Ppc
			or StructureId.TemporalAssociation
			or StructureId.Striatum
			or StructureId.Acc
			or StructureId.OrbitofrontalCortex
			or StructureId.BasalForebrain
			or StructureId.LocusCoeruleus;

	private static string SelectPrefrontalWorkingMemoryMode(float stability, float striatalGate, float accDemand, float topDown)
	{
		if (accDemand > Math.Max(stability, topDown) * 0.90f && accDemand > 0.10f)
		{
			return "Updating";
		}

		if (stability > Math.Max(0.20f, accDemand * 1.15f) && striatalGate > 0.05f)
		{
			return "Maintaining";
		}

		if (topDown > 0.08f)
		{
			return "Biasing";
		}

		return "Idle";
	}

	private ThalamicAttentionGateDiagnostics? BuildThalamicAttentionGateDiagnostics(NeuromodState neuromod)
	{
		if (!IsThalamicAttentionGateDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var relay = 0f;
		var trnGate = 0f;
		var pulvinar = 0f;
		var mediodorsal = 0f;
		var intralaminar = 0f;
		var corticalContext = 0f;
		var achGain = 0.85f + (Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.35f);
		var neGain = 0.90f + (Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.25f);

		switch (_profile.StructureId)
		{
		case StructureId.Thalamus:
			relay = mean * achGain;
			break;
		case StructureId.MotorThalamus:
			relay = mean * 0.60f;
			break;
		case StructureId.Trn:
			trnGate = mean * neGain;
			break;
		case StructureId.Pulvinar:
			pulvinar = mean * achGain;
			relay = mean * 0.25f;
			break;
		case StructureId.MediodorsalThalamus:
			mediodorsal = mean;
			relay = mean * 0.25f;
			break;
		case StructureId.IntralaminarThalamus:
			intralaminar = mean * neGain;
			relay = mean * 0.35f;
			break;
		case StructureId.Pfc:
			corticalContext = mean * 0.30f;
			mediodorsal = mean * 0.12f;
			break;
		case StructureId.Ppc:
			corticalContext = mean * 0.25f;
			pulvinar = mean * 0.15f;
			break;
		case StructureId.V1:
		case StructureId.A1:
		case StructureId.S1:
			corticalContext = mean * 0.20f;
			break;
		case StructureId.BasalForebrain:
			relay = mean * 0.20f;
			break;
		case StructureId.LocusCoeruleus:
			intralaminar = mean * 0.20f;
			break;
		}

		var sensoryGain = Math.Max(0f, (relay * 0.55f) + (pulvinar * 0.30f) + (corticalContext * 0.10f) - (trnGate * 0.25f));
		var corticalAccess = Math.Max(0f, (relay * 0.35f) + (mediodorsal * 0.25f) + (intralaminar * 0.25f) + (pulvinar * 0.20f) + (corticalContext * 0.20f) - (trnGate * 0.20f));
		var selectionBias = Math.Clamp(Math.Max(sensoryGain, corticalAccess) + (pulvinar * 0.18f) + (mediodorsal * 0.12f) - (trnGate * 0.10f), 0f, 120f);
		return new ThalamicAttentionGateDiagnostics(
			SelectThalamicAttentionGateMode(relay, trnGate, pulvinar, intralaminar, corticalAccess),
			relay,
			trnGate,
			pulvinar,
			mediodorsal,
			intralaminar,
			sensoryGain,
			corticalAccess,
			selectionBias);
	}

	private static bool IsThalamicAttentionGateDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.Thalamus
			or StructureId.Trn
			or StructureId.Pulvinar
			or StructureId.MediodorsalThalamus
			or StructureId.IntralaminarThalamus
			or StructureId.MotorThalamus
			or StructureId.Pfc
			or StructureId.Ppc
			or StructureId.V1
			or StructureId.A1
			or StructureId.S1
			or StructureId.BasalForebrain
			or StructureId.LocusCoeruleus;

	private static string SelectThalamicAttentionGateMode(float relay, float trnGate, float pulvinar, float intralaminar, float corticalAccess)
	{
		if (trnGate > Math.Max(relay + pulvinar, corticalAccess) * 0.95f && trnGate > 0.10f)
		{
			return "Suppressed";
		}

		if (pulvinar > Math.Max(0.10f, relay * 0.35f))
		{
			return "Selecting";
		}

		if (intralaminar > Math.Max(0.10f, trnGate * 0.80f))
		{
			return "Broadcasting";
		}

		if (relay > 0.08f || corticalAccess > 0.08f)
		{
			return "Relaying";
		}

		return "Idle";
	}

	private HypothalamicHomeostasisDiagnostics? BuildHypothalamicHomeostasisDiagnostics(NeuromodState neuromod)
	{
		if (!IsHypothalamicHomeostasisDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var visceral = 0f;
		var setpoint = 0f;
		var insula = 0f;
		var limbic = 0f;
		var autonomic = 0f;
		var arousal = 0f;
		var comfortDeficit = 0f;
		var defensive = 0f;
		var neGain = 0.85f + (Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.35f);
		var achGain = 0.85f + (Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.25f);
		var serotoninBuffer = 1.05f - (Math.Clamp(neuromod.SerotoninLevel, 0f, 1f) * 0.20f);

		switch (_profile.StructureId)
		{
		case StructureId.NucleusTractusSolitarius:
			visceral = mean;
			autonomic = mean * 0.20f;
			break;
		case StructureId.Hypothalamus:
			setpoint = mean * serotoninBuffer;
			comfortDeficit = mean * 0.35f;
			break;
		case StructureId.Insula:
			insula = mean;
			comfortDeficit = mean * 0.20f;
			break;
		case StructureId.Amygdala:
			limbic = mean * neGain;
			defensive = mean * 0.35f;
			break;
		case StructureId.LocusCoeruleus:
			arousal = mean * neGain;
			break;
		case StructureId.RapheNuclei:
			comfortDeficit = Math.Max(0f, mean * 0.18f * serotoninBuffer);
			break;
		case StructureId.BasalForebrain:
			arousal = mean * achGain * 0.35f;
			break;
		case StructureId.Pons:
		case StructureId.Medulla:
			autonomic = mean;
			break;
		case StructureId.ReticularFormation:
			autonomic = mean * 0.45f;
			arousal = mean * 0.35f;
			break;
		case StructureId.PeriaqueductalGray:
			defensive = mean;
			comfortDeficit = mean * 0.25f;
			break;
		}

		var totalError = Math.Max(0f, (visceral * 0.35f) + (setpoint * 0.40f) + (insula * 0.25f) + (limbic * 0.20f) - (comfortDeficit * 0.05f));
		var brainstemDrive = Math.Max(autonomic, (setpoint * 0.35f) + (visceral * 0.25f));
		var arousalPressure = Math.Max(arousal, (setpoint * 0.25f) + (limbic * 0.25f));
		var defenseCommand = Math.Max(defensive, (limbic * 0.35f) + (setpoint * 0.20f));
		return new HypothalamicHomeostasisDiagnostics(
			SelectHypothalamicHomeostasisMode(totalError, brainstemDrive, arousalPressure, defenseCommand),
			visceral,
			totalError,
			insula,
			limbic,
			brainstemDrive,
			arousalPressure,
			Math.Max(comfortDeficit, totalError * 0.25f),
			defenseCommand);
	}

	private static bool IsHypothalamicHomeostasisDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.NucleusTractusSolitarius
			or StructureId.Hypothalamus
			or StructureId.Insula
			or StructureId.Amygdala
			or StructureId.LocusCoeruleus
			or StructureId.RapheNuclei
			or StructureId.BasalForebrain
			or StructureId.Pons
			or StructureId.Medulla
			or StructureId.ReticularFormation
			or StructureId.PeriaqueductalGray;

	private static string SelectHypothalamicHomeostasisMode(float error, float autonomic, float arousal, float defensive)
	{
		if (defensive > Math.Max(error, autonomic) * 0.90f && defensive > 0.10f)
		{
			return "Defensive";
		}

		if (autonomic > Math.Max(0.12f, arousal * 1.05f))
		{
			return "Regulating";
		}

		if (arousal > Math.Max(0.10f, error * 0.80f))
		{
			return "Arousing";
		}

		if (error > 0.08f)
		{
			return "Seeking";
		}

		return "Balanced";
	}

	private SleepWakeArousalDiagnostics? BuildSleepWakeArousalDiagnostics(NeuromodState neuromod)
	{
		if (!IsSleepWakeArousalDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var sleepPressure = 0f;
		var reticularDrive = 0f;
		var pontomedullaryTone = 0f;
		var lcWake = 0f;
		var rapheTone = 0f;
		var basalWake = 0f;
		var intralaminar = 0f;
		var neGain = 0.85f + (Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.40f);
		var achGain = 0.85f + (Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.35f);
		var serotoninGain = 0.85f + (Math.Clamp(neuromod.SerotoninLevel, 0f, 1f) * 0.30f);

		switch (_profile.StructureId)
		{
		case StructureId.Hypothalamus:
			sleepPressure = mean * (1.05f - (Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.20f));
			break;
		case StructureId.ReticularFormation:
			reticularDrive = mean * neGain;
			break;
		case StructureId.Pons:
		case StructureId.Medulla:
			pontomedullaryTone = mean;
			break;
		case StructureId.LocusCoeruleus:
			lcWake = mean * neGain;
			break;
		case StructureId.RapheNuclei:
			rapheTone = mean * serotoninGain;
			break;
		case StructureId.BasalForebrain:
			basalWake = mean * achGain;
			break;
		case StructureId.IntralaminarThalamus:
			intralaminar = mean * neGain;
			break;
		}

		var corticalReadiness = Math.Max(0f,
			(reticularDrive * 0.24f) +
			(lcWake * 0.24f) +
			(basalWake * 0.22f) +
			(intralaminar * 0.20f) +
			(pontomedullaryTone * 0.12f) +
			(rapheTone * 0.08f) -
			(sleepPressure * 0.18f));

		return new SleepWakeArousalDiagnostics(
			SelectSleepWakeArousalMode(sleepPressure, reticularDrive, lcWake, basalWake, intralaminar, corticalReadiness),
			sleepPressure,
			reticularDrive,
			pontomedullaryTone,
			lcWake,
			rapheTone,
			basalWake,
			intralaminar,
			corticalReadiness);
	}

	private static bool IsSleepWakeArousalDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.Hypothalamus
			or StructureId.ReticularFormation
			or StructureId.Pons
			or StructureId.Medulla
			or StructureId.LocusCoeruleus
			or StructureId.RapheNuclei
			or StructureId.BasalForebrain
			or StructureId.IntralaminarThalamus;

	private static string SelectSleepWakeArousalMode(float sleepPressure, float reticularDrive, float lcWake, float basalWake, float intralaminar, float corticalReadiness)
	{
		var wakeDrive = reticularDrive + lcWake + basalWake + intralaminar;
		if (sleepPressure > Math.Max(0.16f, wakeDrive * 0.95f) && corticalReadiness < sleepPressure * 0.55f)
		{
			return "SleepPressure";
		}

		if (corticalReadiness > Math.Max(0.12f, sleepPressure * 1.20f))
		{
			return "Awake";
		}

		if (Math.Abs(corticalReadiness - sleepPressure) <= Math.Max(0.08f, Math.Max(corticalReadiness, sleepPressure) * 0.20f))
		{
			return "Transition";
		}

		if (wakeDrive > 0.10f)
		{
			return "Drowsy";
		}

		return "Quiescent";
	}

	private DescendingDefenseDiagnostics? BuildDescendingDefenseDiagnostics(NeuromodState neuromod)
	{
		if (!IsDescendingDefenseDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var amygdala = 0f;
		var hypothalamus = 0f;
		var pag = 0f;
		var raphe = 0f;
		var medulla = 0f;
		var reticular = 0f;
		var spinal = 0f;
		var neGain = 0.85f + (Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.40f);
		var serotoninModulation = 0.85f + (Math.Clamp(neuromod.SerotoninLevel, 0f, 1f) * 0.30f);

		switch (_profile.StructureId)
		{
		case StructureId.Amygdala:
			amygdala = mean * neGain;
			break;
		case StructureId.Hypothalamus:
			hypothalamus = mean;
			break;
		case StructureId.PeriaqueductalGray:
			pag = mean * neGain;
			break;
		case StructureId.RapheNuclei:
			raphe = mean * serotoninModulation;
			break;
		case StructureId.Medulla:
			medulla = mean;
			break;
		case StructureId.ReticularFormation:
			reticular = mean;
			break;
		case StructureId.SpinalCordMotor:
			spinal = mean;
			break;
		}

		var protection = Math.Max(0f,
			(amygdala * 0.25f) +
			(hypothalamus * 0.18f) +
			(pag * 0.30f) +
			(reticular * 0.18f) +
			(spinal * 0.22f) +
			(medulla * 0.10f) -
			(raphe * 0.06f));

		return new DescendingDefenseDiagnostics(
			SelectDescendingDefenseMode(pag, reticular, spinal, raphe, protection),
			amygdala,
			hypothalamus,
			pag,
			raphe,
			medulla,
			reticular,
			spinal,
			protection);
	}

	private static bool IsDescendingDefenseDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.Amygdala
			or StructureId.Hypothalamus
			or StructureId.PeriaqueductalGray
			or StructureId.RapheNuclei
			or StructureId.Medulla
			or StructureId.ReticularFormation
			or StructureId.SpinalCordMotor;

	private static string SelectDescendingDefenseMode(float pag, float reticular, float spinal, float raphe, float protection)
	{
		if (spinal > Math.Max(0.10f, protection * 0.45f))
		{
			return "Withdrawal";
		}

		if (pag > Math.Max(0.12f, raphe * 1.20f))
		{
			return "Defensive";
		}

		if (reticular > Math.Max(0.10f, spinal * 0.70f))
		{
			return "Patterning";
		}

		if (raphe > Math.Max(0.10f, pag * 0.70f))
		{
			return "Modulating";
		}

		if (protection > 0.08f)
		{
			return "Guarding";
		}

		return "Quiet";
	}

	private DopamineRewardDiagnostics? BuildDopamineRewardDiagnostics(NeuromodState neuromod, float rewardPredictionError)
	{
		if (!IsDopamineRewardDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var rpe = Math.Clamp(rewardPredictionError, -1f, 1f);
		var dopamine = Math.Clamp(neuromod.DopamineLevel, 0f, 1f);
		var positiveRpe = Math.Max(0f, rpe);
		var negativeRpe = Math.Max(0f, -rpe);
		var vta = 0f;
		var snc = 0f;
		var accumbens = 0f;
		var striatum = 0f;
		var habenula = 0f;
		var ofc = 0f;
		var pfc = 0f;

		switch (_profile.StructureId)
		{
		case StructureId.Vta:
			vta = mean * (0.90f + (dopamine * 0.35f) + (positiveRpe * 0.25f));
			break;
		case StructureId.Snc:
			snc = mean * (0.90f + (dopamine * 0.30f) + (Math.Abs(rpe) * 0.15f));
			break;
		case StructureId.NucleusAccumbens:
			accumbens = mean * (0.85f + (dopamine * 0.35f) + (positiveRpe * 0.20f));
			break;
		case StructureId.Striatum:
			striatum = mean * (0.85f + (dopamine * 0.35f));
			break;
		case StructureId.Habenula:
			habenula = mean * (0.85f + (negativeRpe * 0.45f));
			break;
		case StructureId.OrbitofrontalCortex:
			ofc = mean * (0.85f + (dopamine * 0.15f) + (Math.Abs(rpe) * 0.20f));
			break;
		case StructureId.Pfc:
			pfc = mean * (0.85f + (dopamine * 0.20f));
			break;
		}

		var learning = Math.Max(0f,
			(vta * 0.24f) +
			(snc * 0.22f) +
			(accumbens * 0.20f) +
			(striatum * 0.18f) +
			(ofc * 0.18f) +
			(pfc * 0.12f) +
			(positiveRpe * 0.25f) -
			(habenula * 0.16f));

		return new DopamineRewardDiagnostics(
			SelectDopamineRewardMode(vta, snc, accumbens, striatum, habenula, ofc, pfc, rpe, learning),
			vta,
			snc,
			accumbens,
			striatum,
			habenula,
			ofc,
			pfc,
			rpe,
			learning);
	}

	private static bool IsDopamineRewardDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.Vta
			or StructureId.Snc
			or StructureId.NucleusAccumbens
			or StructureId.Striatum
			or StructureId.Habenula
			or StructureId.OrbitofrontalCortex
			or StructureId.Pfc;

	private static string SelectDopamineRewardMode(float vta, float snc, float accumbens, float striatum, float habenula, float ofc, float pfc, float rpe, float learning)
	{
		if (habenula > Math.Max(0.08f, Math.Max(vta, accumbens) * 0.70f) && rpe < -0.05f)
		{
			return "NegativeTeaching";
		}

		if ((vta + accumbens) > Math.Max(0.12f, habenula * 1.25f) && rpe > 0.05f)
		{
			return "PhasicReward";
		}

		if ((snc + striatum) > Math.Max(0.12f, ofc + pfc))
		{
			return "ActionTeaching";
		}

		if (ofc > Math.Max(0.10f, pfc * 0.85f))
		{
			return "Valuation";
		}

		if (pfc > Math.Max(0.10f, ofc * 0.85f))
		{
			return "GoalBias";
		}

		if (learning > 0.08f)
		{
			return "TonicLearning";
		}

		return "Quiet";
	}

	private SeptohippocampalThetaDiagnostics? BuildSeptohippocampalThetaDiagnostics(NeuromodState neuromod)
	{
		if (!IsSeptohippocampalThetaDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var septal = 0f;
		var entorhinal = 0f;
		var dentate = 0f;
		var ca3 = 0f;
		var ca1 = 0f;
		var subicular = 0f;
		var headDirection = 0f;
		var retrosplenial = 0f;
		var vestibular = 0f;
		var achGain = 0.85f + (Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.40f);

		switch (_profile.StructureId)
		{
		case StructureId.BasalForebrain:
			septal = mean * achGain;
			break;
		case StructureId.EntorhinalCortex:
			entorhinal = mean * achGain;
			break;
		case StructureId.DentateGyrus:
			dentate = mean * achGain;
			break;
		case StructureId.CA3:
			ca3 = mean;
			break;
		case StructureId.CA2:
			ca3 = mean * 0.35f;
			ca1 = mean * 0.25f;
			headDirection = mean * 0.20f;
			break;
		case StructureId.CA1:
			ca1 = mean * achGain;
			break;
		case StructureId.Subiculum:
			subicular = mean;
			headDirection = mean * 0.20f;
			break;
		case StructureId.Presubiculum:
		case StructureId.Parasubiculum:
			headDirection = mean;
			break;
		case StructureId.RetrosplenialCortex:
			retrosplenial = mean;
			headDirection = mean * 0.30f;
			break;
		case StructureId.VestibularNuclei:
			vestibular = mean;
			headDirection = mean * 0.35f;
			break;
		}

		var coherence = Math.Max(0f,
			(septal * 0.22f) +
			(entorhinal * 0.18f) +
			(dentate * 0.12f) +
			(ca3 * 0.14f) +
			(ca1 * 0.18f) +
			(subicular * 0.16f) +
			(headDirection * 0.18f) +
			(retrosplenial * 0.14f) +
			(vestibular * 0.12f));

		return new SeptohippocampalThetaDiagnostics(
			SelectSeptohippocampalThetaMode(septal, entorhinal, ca3, ca1, headDirection, retrosplenial, vestibular, coherence),
			septal,
			entorhinal,
			dentate,
			ca3,
			ca1,
			subicular,
			headDirection,
			retrosplenial,
			vestibular,
			coherence);
	}

	private static bool IsSeptohippocampalThetaDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.BasalForebrain
			or StructureId.EntorhinalCortex
			or StructureId.DentateGyrus
			or StructureId.CA3
			or StructureId.CA2
			or StructureId.CA1
			or StructureId.Subiculum
			or StructureId.Presubiculum
			or StructureId.Parasubiculum
			or StructureId.RetrosplenialCortex
			or StructureId.VestibularNuclei;

	private static string SelectSeptohippocampalThetaMode(float septal, float entorhinal, float ca3, float ca1, float headDirection, float retrosplenial, float vestibular, float coherence)
	{
		if (septal > Math.Max(0.10f, coherence * 0.35f) && entorhinal > 0.05f)
		{
			return "ThetaPacing";
		}

		if ((headDirection + vestibular + retrosplenial) > Math.Max(0.12f, ca1 + ca3))
		{
			return "PathIntegrating";
		}

		if (ca3 > Math.Max(0.10f, entorhinal * 0.80f))
		{
			return "Sequencing";
		}

		if (ca1 > Math.Max(0.10f, ca3 * 0.80f))
		{
			return "PlaceTiming";
		}

		if (coherence > 0.08f)
		{
			return "Synchronized";
		}

		return "Quiet";
	}

	private SpinalProprioceptiveDiagnostics? BuildSpinalProprioceptiveDiagnostics(NeuromodState neuromod)
	{
		if (!IsSpinalProprioceptiveDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var spinal = 0f;
		var s1 = 0f;
		var m1 = 0f;
		var cerebellar = 0f;
		var vestibular = 0f;
		var reticular = 0f;
		var thalamic = 0f;
		var achGain = 0.90f + (Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.30f);
		var neGain = 0.85f + (Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.35f);

		switch (_profile.StructureId)
		{
		case StructureId.SpinalCordMotor:
			spinal = mean;
			break;
		case StructureId.S1:
			s1 = mean * achGain;
			break;
		case StructureId.M1:
			m1 = mean;
			break;
		case StructureId.CerebellarGranule:
			cerebellar = mean;
			break;
		case StructureId.VestibularNuclei:
			vestibular = mean;
			break;
		case StructureId.ReticularFormation:
			reticular = mean * neGain;
			break;
		case StructureId.Thalamus:
			thalamic = mean * achGain;
			break;
		case StructureId.MotorThalamus:
			thalamic = mean * achGain * 0.80f;
			break;
		}

		var readiness = Math.Max(0f,
			(spinal * 0.24f) +
			(s1 * 0.18f) +
			(m1 * 0.18f) +
			(cerebellar * 0.18f) +
			(vestibular * 0.14f) +
			(reticular * 0.16f) +
			(thalamic * 0.12f));
		var coherence = Math.Clamp(
			(s1 * 0.22f) +
			(cerebellar * 0.20f) +
			(vestibular * 0.16f) +
			(thalamic * 0.16f) +
			(spinal * 0.12f) +
			(m1 * 0.10f) +
			(reticular * 0.10f),
			0f,
			120f);

		return new SpinalProprioceptiveDiagnostics(
			SelectSpinalProprioceptiveMode(spinal, s1, m1, cerebellar, vestibular, reticular, thalamic, readiness, coherence),
			spinal,
			s1,
			m1,
			cerebellar,
			vestibular,
			reticular,
			thalamic,
			readiness,
			coherence);
	}

	private static bool IsSpinalProprioceptiveDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.SpinalCordMotor
			or StructureId.S1
			or StructureId.M1
			or StructureId.CerebellarGranule
			or StructureId.VestibularNuclei
			or StructureId.ReticularFormation
			or StructureId.Thalamus
			or StructureId.MotorThalamus;

	private static string SelectSpinalProprioceptiveMode(float spinal, float s1, float m1, float cerebellar, float vestibular, float reticular, float thalamic, float readiness, float coherence)
	{
		if (spinal > Math.Max(0.10f, m1 * 0.75f) && readiness > 0.12f)
		{
			return "Reflexive";
		}

		if ((cerebellar + s1) > Math.Max(0.12f, vestibular + reticular))
		{
			return "Proprioceptive";
		}

		if ((vestibular + reticular) > Math.Max(0.12f, cerebellar))
		{
			return "Postural";
		}

		if (m1 > Math.Max(0.10f, spinal * 0.85f))
		{
			return "Descending";
		}

		if (thalamic > 0.08f)
		{
			return "Relaying";
		}

		if (coherence > 0.08f)
		{
			return "Integrated";
		}

		return "Quiet";
	}

	private OlfactoryLimbicMemoryDiagnostics? BuildOlfactoryLimbicMemoryDiagnostics(NeuromodState neuromod)
	{
		if (!IsOlfactoryLimbicMemoryDiagnosticsStructure(_profile.StructureId))
		{
			return null;
		}

		var mean = _meanFiringRateHz;
		var olfactory = 0f;
		var temporal = 0f;
		var amygdala = 0f;
		var entorhinal = 0f;
		var hippocampal = 0f;
		var ofc = 0f;
		var pfc = 0f;
		var familiarity = 0f;
		var achGain = 0.85f + (Math.Clamp(neuromod.AcetylcholineLevel, 0f, 1f) * 0.35f);
		var neGain = 0.85f + (Math.Clamp(neuromod.NorepinephrineLevel, 0f, 1f) * 0.35f);
		var dopamineGain = 0.85f + (Math.Clamp(neuromod.DopamineLevel, 0f, 1f) * 0.25f);

		switch (_profile.StructureId)
		{
		case StructureId.OlfactoryBulb:
			olfactory = mean * achGain;
			break;
		case StructureId.TemporalAssociation:
			temporal = mean;
			familiarity = mean * 0.20f;
			break;
		case StructureId.PerirhinalCortex:
			temporal = mean * 0.35f;
			familiarity = mean;
			break;
		case StructureId.ParahippocampalCortex:
			entorhinal = mean * 0.25f;
			hippocampal = mean * 0.45f;
			break;
		case StructureId.Amygdala:
			amygdala = mean * neGain;
			break;
		case StructureId.EntorhinalCortex:
			entorhinal = mean * achGain;
			break;
		case StructureId.DentateGyrus:
			hippocampal = mean * 0.35f;
			break;
		case StructureId.CA3:
			hippocampal = mean * 0.55f;
			break;
		case StructureId.CA2:
			hippocampal = mean * 0.25f;
			break;
		case StructureId.CA1:
			hippocampal = mean * 0.70f;
			break;
		case StructureId.Subiculum:
			hippocampal = mean * 0.45f;
			pfc = mean * 0.15f;
			break;
		case StructureId.OrbitofrontalCortex:
			ofc = mean * dopamineGain;
			break;
		case StructureId.Pfc:
			pfc = mean;
			break;
		}

		var coherence = Math.Clamp(
			(olfactory * 0.18f) +
			(temporal * 0.15f) +
			(amygdala * 0.16f) +
			(entorhinal * 0.17f) +
			(hippocampal * 0.20f) +
			(ofc * 0.12f) +
			(pfc * 0.16f) +
			(familiarity * 0.10f),
			0f,
			120f);

		return new OlfactoryLimbicMemoryDiagnostics(
			SelectOlfactoryLimbicMemoryMode(olfactory, temporal, amygdala, entorhinal, hippocampal, ofc, pfc, familiarity, coherence),
			olfactory,
			temporal,
			amygdala,
			entorhinal,
			hippocampal,
			ofc,
			pfc,
			familiarity,
			coherence);
	}

	private static bool IsOlfactoryLimbicMemoryDiagnosticsStructure(StructureId structureId)
		=> structureId is StructureId.OlfactoryBulb
			or StructureId.TemporalAssociation
			or StructureId.PerirhinalCortex
			or StructureId.ParahippocampalCortex
			or StructureId.Amygdala
			or StructureId.EntorhinalCortex
			or StructureId.DentateGyrus
			or StructureId.CA3
			or StructureId.CA2
			or StructureId.CA1
			or StructureId.Subiculum
			or StructureId.OrbitofrontalCortex
			or StructureId.Pfc;

	private static string SelectOlfactoryLimbicMemoryMode(float olfactory, float temporal, float amygdala, float entorhinal, float hippocampal, float ofc, float pfc, float familiarity, float coherence)
	{
		if (olfactory > Math.Max(0.10f, temporal * 0.75f) && (amygdala + entorhinal) > 0.08f)
		{
			return "OdorCueing";
		}

		if (amygdala > Math.Max(0.10f, ofc * 0.80f))
		{
			return "AffectiveTagging";
		}

		if (entorhinal > Math.Max(0.10f, hippocampal * 0.70f))
		{
			return "Encoding";
		}

		if (hippocampal > Math.Max(0.10f, entorhinal * 0.80f))
		{
			return "Recalling";
		}

		if (ofc > Math.Max(0.10f, pfc * 0.70f))
		{
			return "Valuating";
		}

		if (pfc > Math.Max(0.10f, hippocampal * 0.70f))
		{
			return "NarrativeControl";
		}

		if (familiarity > 0.08f)
		{
			return "Familiarity";
		}

		if (coherence > 0.08f)
		{
			return "Integrated";
		}

		return "Quiet";
	}

	private int ComputeConductionDelayMs(SpikeMessage message)
	{
		DelayWindow tractDelayWindow = GetTractDelayWindow(message.SourceStructure, message.TargetStructure);
		int num = SampleDelayWindow(tractDelayWindow, message, 19);
		num += ComputeBiologicalTimingAdjustment(message, tractDelayWindow);
		if (!message.IsFeedback)
		{
			return ClampDelay(num, tractDelayWindow);
		}
		int num2 = SampleDelayWindow(_feedbackDelayWindow, message, 37);
		num2 += ComputeBiologicalTimingAdjustment(message, _feedbackDelayWindow);
		return ClampDelay(Math.Max(num, num2), tractDelayWindow, _feedbackDelayWindow);
	}

	private static int ComputeBiologicalTimingAdjustment(SpikeMessage message, DelayWindow tractDelayWindow)
	{
		float distance = EstimateTopographicDistance(message);
		int adjustment = (int)MathF.Round(distance * EstimateDistanceWeight(message.SourceStructure, message.TargetStructure));
		adjustment += EstimateSpikeTimingOffset(message.SpikeType);
		adjustment += EstimateTransmitterTimingOffset(message.Neurotransmitter);
		if (message.IsFeedback)
		{
			adjustment += 2;
		}
		if (message.SourceStructure == message.TargetStructure)
		{
			adjustment -= 2;
		}
		if (IsCorticalLike(message.SourceStructure) && IsCorticalLike(message.TargetStructure))
		{
			adjustment -= 1;
		}
		if (IsBrainstemOrModulatory(message.SourceStructure) || IsBrainstemOrModulatory(message.TargetStructure))
		{
			adjustment += 2;
		}
		int jitter = (int)((uint)HashCode.Combine(message.MessageId, message.SourceNeuronId ?? string.Empty, message.TargetNeuronId ?? string.Empty, 101) % 3u) - 1;
		return Math.Clamp(adjustment + jitter, -3, Math.Max(3, tractDelayWindow.MaxMs));
	}

	private static float EstimateTopographicDistance(SpikeMessage message)
	{
		int source = TryParseNeuronIndex(message.SourceNeuronId, out int sourceIndex) ? sourceIndex : StableNeuronIndex(message.SourceNeuronId, message.SynapseId, 113);
		int target = TryParseNeuronIndex(message.TargetNeuronId, out int targetIndex) ? targetIndex : StableNeuronIndex(message.TargetNeuronId, message.SynapseId, 127);
		int sourceX = source & 31;
		int sourceY = (source >> 5) & 31;
		int targetX = target & 31;
		int targetY = (target >> 5) & 31;
		float dx = (sourceX - targetX) / 31f;
		float dy = (sourceY - targetY) / 31f;
		return Math.Clamp(MathF.Sqrt(dx * dx + dy * dy), 0f, 1.42f);
	}

	private static int EstimateDistanceWeight(StructureId source, StructureId target)
	{
		if (source == target)
		{
			return 2;
		}
		if ((IsThalamic(source) && IsCorticalLike(target)) || (IsThalamic(target) && IsCorticalLike(source)))
		{
			return 4;
		}
		if (IsBrainstemOrModulatory(source) || IsBrainstemOrModulatory(target))
		{
			return 7;
		}
		if (IsCerebellar(source) || IsCerebellar(target))
		{
			return 6;
		}
		if (IsHippocampal(source) || IsHippocampal(target))
		{
			return 5;
		}
		return 3;
	}

	private static int EstimateSpikeTimingOffset(SpikeTypeEnum spikeType)
	{
		return spikeType switch
		{
			SpikeTypeEnum.BURST => 1,
			SpikeTypeEnum.COMPLEX => 3,
			SpikeTypeEnum.GRADED => 4,
			_ => 0,
		};
	}

	private static int EstimateTransmitterTimingOffset(NTEnum neurotransmitter)
	{
		return neurotransmitter switch
		{
			NTEnum.GABA => -1,
			NTEnum.ACETYLCHOLINE => 1,
			NTEnum.DOPAMINE => 5,
			NTEnum.SEROTONIN => 5,
			NTEnum.NOREPINEPHRINE => 4,
			_ => 0,
		};
	}

	private static int ClampDelay(int delay, DelayWindow primary, DelayWindow? secondary = null)
	{
		int min = Math.Max(0, Math.Min(primary.MinMs, secondary?.MinMs ?? primary.MinMs));
		int max = Math.Max(primary.MaxMs, secondary?.MaxMs ?? primary.MaxMs) + 12;
		return Math.Clamp(delay, min, max);
	}

	private static int StableNeuronIndex(string neuronId, Guid synapseId, int salt)
	{
		return (int)((uint)HashCode.Combine(neuronId ?? string.Empty, synapseId, salt) % 1024u);
	}

	private static bool TryParseNeuronIndex(string neuronId, out int value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(neuronId))
		{
			return false;
		}
		ReadOnlySpan<char> text = neuronId.AsSpan().Trim();
		int start = text.Length - 1;
		while (start >= 0 && char.IsDigit(text[start]))
		{
			start--;
		}
		return start < text.Length - 1 && int.TryParse(text.Slice(start + 1), out value);
	}

	private static int SampleDelayWindow(DelayWindow window, SpikeMessage message, int salt)
	{
		int num = Math.Max(0, Math.Min(window.MinMs, window.MaxMs));
		int num2 = Math.Max(num, Math.Max(window.MinMs, window.MaxMs));
		if (num == num2)
		{
			return num;
		}
		int num3 = num2 - num + 1;
		int value = HashCode.Combine(message.MessageId, (int)message.SourceStructure, (int)message.TargetStructure, message.SourceNeuronId ?? string.Empty, message.TargetNeuronId ?? string.Empty, salt);
		return num + (int)((uint)value % (uint)num3);
	}

	private static DelayWindow GetTractDelayWindow(StructureId source, StructureId target)
	{
		if (source == target)
		{
			return new DelayWindow(2, 5);
		}
		if ((IsThalamic(source) && IsCorticalLike(target)) || (IsThalamic(target) && IsCorticalLike(source)))
		{
			return new DelayWindow(4, 12);
		}
		if ((IsCerebellar(source) && (IsThalamic(target) || IsMotor(target))) || (IsCerebellar(target) && (IsThalamic(source) || IsMotor(source))))
		{
			return new DelayWindow(8, 18);
		}
		if (IsBrainstemOrModulatory(source) || IsBrainstemOrModulatory(target))
		{
			return new DelayWindow(6, 20);
		}
		if (IsCorticalLike(source) && IsCorticalLike(target))
		{
			return new DelayWindow(2, 8);
		}
		if (IsHippocampal(source) || IsHippocampal(target))
		{
			return new DelayWindow(3, 12);
		}
		if (IsBasalGanglia(source) || IsBasalGanglia(target))
		{
			return new DelayWindow(4, 14);
		}
		return new DelayWindow(3, 10);
	}

	// Category flags table replacing six per-spike switch helpers. The hot tract-delay
	// and distance-weight paths used to call up to four `IsX` switches per spike; a
	// single byte-array indexed bit test is dramatically faster and branch-free.
	[Flags]
	private enum StructureCategory : byte
	{
		None = 0,
		Cortical = 1 << 0,
		Thalamic = 1 << 1,
		Hippocampal = 1 << 2,
		BasalGanglia = 1 << 3,
		Cerebellar = 1 << 4,
		BrainstemModulatory = 1 << 5,
		Motor = 1 << 6,
	}

	private static readonly StructureCategory[] _categoryFlags = BuildCategoryFlags();

	private static StructureCategory[] BuildCategoryFlags()
	{
		var max = 0;
		foreach (var id in Enum.GetValues<StructureId>())
		{
			var i = (int)id;
			if (i > max) max = i;
		}
		var table = new StructureCategory[max + 1];

		void Add(StructureId id, StructureCategory cat) => table[(int)id] |= cat;

		// Cortical-like (includes corpus callosum and motor cortices that the original
		// IsCorticalLike also returned true for).
		Add(StructureId.V1, StructureCategory.Cortical);
		Add(StructureId.V2, StructureCategory.Cortical);
		Add(StructureId.V4, StructureCategory.Cortical);
		Add(StructureId.Mt, StructureCategory.Cortical);
		Add(StructureId.A1, StructureCategory.Cortical);
		Add(StructureId.S1, StructureCategory.Cortical);
		Add(StructureId.Pfc, StructureCategory.Cortical);
		Add(StructureId.BrocaBa44Ba45, StructureCategory.Cortical);
		Add(StructureId.WernickePstgPsts, StructureCategory.Cortical);
		Add(StructureId.ArcuateFasciculus, StructureCategory.Cortical);
		Add(StructureId.SupramarginalAngular, StructureCategory.Cortical);
		Add(StructureId.OrbitofrontalCortex, StructureCategory.Cortical);
		Add(StructureId.Insula, StructureCategory.Cortical);
		Add(StructureId.Ppc, StructureCategory.Cortical);
		Add(StructureId.TemporalAssociation, StructureCategory.Cortical);
		Add(StructureId.PremotorCortex, StructureCategory.Cortical);
		Add(StructureId.M1, StructureCategory.Cortical);
		Add(StructureId.Sma, StructureCategory.Cortical);
		Add(StructureId.Acc, StructureCategory.Cortical);
		Add(StructureId.PosteriorCingulate, StructureCategory.Cortical);
		Add(StructureId.RetrosplenialCortex, StructureCategory.Cortical);
		Add(StructureId.CorpusCallosum, StructureCategory.Cortical);

		// Thalamic.
		Add(StructureId.Thalamus, StructureCategory.Thalamic);
		Add(StructureId.Trn, StructureCategory.Thalamic);
		Add(StructureId.Pulvinar, StructureCategory.Thalamic);
		Add(StructureId.MediodorsalThalamus, StructureCategory.Thalamic);
		Add(StructureId.IntralaminarThalamus, StructureCategory.Thalamic);
		Add(StructureId.MotorThalamus, StructureCategory.Thalamic);

		// Hippocampal.
		Add(StructureId.EntorhinalCortex, StructureCategory.Hippocampal);
		Add(StructureId.DentateGyrus, StructureCategory.Hippocampal);
		Add(StructureId.CA3, StructureCategory.Hippocampal);
		Add(StructureId.CA2, StructureCategory.Hippocampal);
		Add(StructureId.CA1, StructureCategory.Hippocampal);
		Add(StructureId.Subiculum, StructureCategory.Hippocampal);
		Add(StructureId.Presubiculum, StructureCategory.Hippocampal);
		Add(StructureId.Parasubiculum, StructureCategory.Hippocampal);
		Add(StructureId.ParahippocampalCortex, StructureCategory.Hippocampal);
		Add(StructureId.PerirhinalCortex, StructureCategory.Hippocampal);

		// Basal ganglia.
		Add(StructureId.Striatum, StructureCategory.BasalGanglia);
		Add(StructureId.GlobusPallidus, StructureCategory.BasalGanglia);
		Add(StructureId.GPe, StructureCategory.BasalGanglia);
		Add(StructureId.GPi, StructureCategory.BasalGanglia);
		Add(StructureId.Stn, StructureCategory.BasalGanglia);
		Add(StructureId.Snr, StructureCategory.BasalGanglia);
		Add(StructureId.Snc, StructureCategory.BasalGanglia);
		Add(StructureId.NucleusAccumbens, StructureCategory.BasalGanglia);
		Add(StructureId.VentralPallidum, StructureCategory.BasalGanglia);
		Add(StructureId.Habenula, StructureCategory.BasalGanglia);

		// Cerebellar.
		Add(StructureId.CerebellarGranule, StructureCategory.Cerebellar);
		Add(StructureId.CerebellarVermis, StructureCategory.Cerebellar);
		Add(StructureId.CerebellarLobules, StructureCategory.Cerebellar);
		Add(StructureId.PurkinjeCellLayer, StructureCategory.Cerebellar);
		Add(StructureId.DeepCerebellarNuclei, StructureCategory.Cerebellar);
		Add(StructureId.InferiorOlive, StructureCategory.Cerebellar);

		// Brainstem / modulatory (Snc is also basal ganglia per the original switches).
		Add(StructureId.Pons, StructureCategory.BrainstemModulatory);
		Add(StructureId.Medulla, StructureCategory.BrainstemModulatory);
		Add(StructureId.SuperiorColliculus, StructureCategory.BrainstemModulatory);
		Add(StructureId.Hypothalamus, StructureCategory.BrainstemModulatory);
		Add(StructureId.LocusCoeruleus, StructureCategory.BrainstemModulatory);
		Add(StructureId.RapheNuclei, StructureCategory.BrainstemModulatory);
		Add(StructureId.BasalForebrain, StructureCategory.BrainstemModulatory);
		Add(StructureId.Vta, StructureCategory.BrainstemModulatory);
		Add(StructureId.Snc, StructureCategory.BrainstemModulatory);

		// Motor cortex subset (orthogonal flag preserved by the original IsMotor helper).
		Add(StructureId.M1, StructureCategory.Motor);
		Add(StructureId.Sma, StructureCategory.Motor);
		Add(StructureId.PremotorCortex, StructureCategory.Motor);

		return table;
	}

	private static StructureCategory CategoryOf(StructureId id)
	{
		var i = (int)id;
		var table = _categoryFlags;
		return (uint)i < (uint)table.Length ? table[i] : StructureCategory.None;
	}

	private static bool IsMotor(StructureId id) => (CategoryOf(id) & StructureCategory.Motor) != 0;
	private static bool IsCorticalLike(StructureId id) => (CategoryOf(id) & StructureCategory.Cortical) != 0;
	private static bool IsThalamic(StructureId id) => (CategoryOf(id) & StructureCategory.Thalamic) != 0;
	private static bool IsHippocampal(StructureId id) => (CategoryOf(id) & StructureCategory.Hippocampal) != 0;
	private static bool IsBasalGanglia(StructureId id) => (CategoryOf(id) & StructureCategory.BasalGanglia) != 0;
	private static bool IsCerebellar(StructureId id) => (CategoryOf(id) & StructureCategory.Cerebellar) != 0;
	private static bool IsBrainstemOrModulatory(StructureId id) => (CategoryOf(id) & StructureCategory.BrainstemModulatory) != 0;
}
