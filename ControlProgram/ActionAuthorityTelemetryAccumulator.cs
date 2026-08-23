using NeuralResonanceEngine.Shared.Contracts;

internal sealed class ActionAuthorityTelemetryAccumulator
{
    private readonly object _gate = new();
    private readonly Dictionary<int, MutableChannelTelemetry> _channels = [];
    private long _samples;
    private long _circuitObservedTicks;
    private long _authorityGrantedTicks;
    private long _authorityGrantEpisodes;
    private long _firstAuthorityGrantTick;
    private long _lastAuthorityGrantTick;
    private long _lastObservedTick = long.MinValue;
    private bool _authorityWasGranted;

    public void Observe(NeuronalMotorRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var traces = runtime.ActionChannelTraces ?? [];
        var grantedTrace = runtime.SelectedActionChannel >= 0
            ? traces.FirstOrDefault(trace =>
                trace.ChannelIndex == runtime.SelectedActionChannel &&
                trace.AuthorityGranted)
            : null;
        Observe(
            runtime.Tick,
            runtime.ActionCircuitObserved,
            grantedTrace is not null,
            grantedTrace?.ChannelIndex ?? runtime.SelectedActionChannel,
            traces);
    }

    internal void Observe(
        long tick,
        bool circuitObserved,
        bool authorityGranted,
        int selectedChannel,
        IReadOnlyList<ActionAuthorityChannelTrace> traces)
    {
        ArgumentNullException.ThrowIfNull(traces);
        lock (_gate)
        {
            if (tick <= _lastObservedTick)
            {
                return;
            }

            _lastObservedTick = tick;
            _samples++;
            if (circuitObserved)
            {
                _circuitObservedTicks++;
            }

            var granted = authorityGranted && selectedChannel >= 0;
            if (granted)
            {
                _authorityGrantedTicks++;
                _lastAuthorityGrantTick = tick;
                if (!_authorityWasGranted)
                {
                    _authorityGrantEpisodes++;
                    if (_firstAuthorityGrantTick == 0)
                    {
                        _firstAuthorityGrantTick = tick;
                    }
                }
            }

            _authorityWasGranted = granted;
            foreach (var trace in traces)
            {
                if (!_channels.TryGetValue(trace.ChannelIndex, out var channel))
                {
                    channel = new MutableChannelTelemetry(trace.ChannelIndex);
                    _channels.Add(trace.ChannelIndex, channel);
                }

                var outputSelected = selectedChannel >= 0 && trace.ChannelIndex == selectedChannel;
                channel.Observe(
                    trace,
                    selected: outputSelected,
                    authorityGranted: granted && outputSelected);
            }
        }
    }

    public ActionAuthorityCumulativeTelemetry Capture()
    {
        lock (_gate)
        {
            return new ActionAuthorityCumulativeTelemetry(
                _samples,
                _circuitObservedTicks,
                _authorityGrantedTicks,
                _authorityGrantEpisodes,
                _firstAuthorityGrantTick,
                _lastAuthorityGrantTick,
                _channels.Values
                    .OrderBy(static channel => channel.ChannelIndex)
                    .Select(static channel => channel.Capture())
                    .ToArray());
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _channels.Clear();
            _samples = 0;
            _circuitObservedTicks = 0;
            _authorityGrantedTicks = 0;
            _authorityGrantEpisodes = 0;
            _firstAuthorityGrantTick = 0;
            _lastAuthorityGrantTick = 0;
            _lastObservedTick = long.MinValue;
            _authorityWasGranted = false;
        }
    }

    private sealed class MutableChannelTelemetry(int channelIndex)
    {
        private float _minimumOutputNucleusInhibition = float.PositiveInfinity;

        public int ChannelIndex { get; } = channelIndex;
        public long Samples { get; private set; }
        public long SelectedTicks { get; private set; }
        public long AuthorityGrantedTicks { get; private set; }
        public float PeakProposalDrive { get; private set; }
        public float PeakDirectPathwayActivation { get; private set; }
        public float PeakIndirectPathwayActivation { get; private set; }
        public float PeakHyperdirectSuppression { get; private set; }
        public float PeakThalamicRelayActivation { get; private set; }
        public float PeakSelectionScore { get; private set; } = float.NegativeInfinity;
        public int PeakDirectActiveNeurons { get; private set; }
        public int PeakIndirectActiveNeurons { get; private set; }
        public float PeakDirectMeanUpState { get; private set; }
        public float PeakIndirectMeanUpState { get; private set; }

        public void Observe(
            ActionAuthorityChannelTrace trace,
            bool selected,
            bool authorityGranted)
        {
            Samples++;
            if (selected)
            {
                SelectedTicks++;
            }
            if (authorityGranted)
            {
                AuthorityGrantedTicks++;
            }

            PeakProposalDrive = Math.Max(PeakProposalDrive, trace.ProposalDrive);
            PeakDirectPathwayActivation = Math.Max(PeakDirectPathwayActivation, trace.DirectPathwayActivation);
            PeakIndirectPathwayActivation = Math.Max(PeakIndirectPathwayActivation, trace.IndirectPathwayActivation);
            PeakHyperdirectSuppression = Math.Max(PeakHyperdirectSuppression, trace.HyperdirectSuppression);
            _minimumOutputNucleusInhibition = Math.Min(
                _minimumOutputNucleusInhibition,
                trace.OutputNucleusInhibition);
            PeakThalamicRelayActivation = Math.Max(PeakThalamicRelayActivation, trace.ThalamicRelayActivation);
            PeakSelectionScore = Math.Max(PeakSelectionScore, trace.SelectionScore);
            PeakDirectActiveNeurons = Math.Max(PeakDirectActiveNeurons, trace.DirectActiveNeurons);
            PeakIndirectActiveNeurons = Math.Max(PeakIndirectActiveNeurons, trace.IndirectActiveNeurons);
            PeakDirectMeanUpState = Math.Max(PeakDirectMeanUpState, trace.DirectMeanUpState);
            PeakIndirectMeanUpState = Math.Max(PeakIndirectMeanUpState, trace.IndirectMeanUpState);
        }

        public ActionAuthorityChannelCumulativeTelemetry Capture()
            => new(
                ChannelIndex,
                Samples,
                SelectedTicks,
                AuthorityGrantedTicks,
                PeakProposalDrive,
                PeakDirectPathwayActivation,
                PeakIndirectPathwayActivation,
                PeakHyperdirectSuppression,
                float.IsPositiveInfinity(_minimumOutputNucleusInhibition)
                    ? 0f
                    : _minimumOutputNucleusInhibition,
                PeakThalamicRelayActivation,
                float.IsNegativeInfinity(PeakSelectionScore) ? 0f : PeakSelectionScore,
                PeakDirectActiveNeurons,
                PeakIndirectActiveNeurons,
                PeakDirectMeanUpState,
                PeakIndirectMeanUpState);
    }
}
