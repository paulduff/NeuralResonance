using System.Collections.Concurrent;

namespace NRE.Core.Engine;

/// <summary>
/// PeerBridge: Inter-Instance Neural Communication
///
/// BIOLOGICAL ANALOGY:
/// When two people converse, information flows through shared sensory channels:
///   1. SPEECH CHANNEL: Speaker's Broca's → M1 → vocal tract → sound →
///      Listener's A1 → Wernicke's (comprehension)
///   2. ATTENTION CHANNEL: Social cognition — each person has a model of
///      the other's attentional and emotional state (Theory of Mind, STS/TPJ)
///
/// IMPLEMENTATION:
/// Two NRE engine instances connect through a shared PeerBridge. Each instance:
///   - Publishes speech output (phoneme sequences from DiphthongVocalTract)
///   - Receives the other's speech as auditory input
///   - Publishes a compact neural state vector (arousal, dominant region, valence)
///   - Receives the other's state for social awareness
///
/// The bridge uses lock-free concurrent queues — no shared mutable state.
/// Each instance runs its own simulation loop independently.
///
/// PROTOCOL:
///   Instance A                          Instance B
///   ──────────                          ──────────
///   Broca's plans utterance             
///   VocalTract produces formants  ───→  Auditory cortex receives formants
///   State vector published        ───→  Social cognition module reads state
///                                       Wernicke's comprehends
///                                       Broca's plans response
///   Auditory cortex receives  ←───      VocalTract produces formants
///   Social cognition reads    ←───      State vector published
/// </summary>
public sealed class PeerBridge : IDisposable
{
    /// <summary>
    /// Speech event: a segment of articulatory output from one instance.
    /// Contains formant data that the receiving instance's auditory cortex processes.
    /// </summary>
    public readonly record struct SpeechSegment(
        string SourceId,
        long StepIndex,
        string[] Phonemes,         // IPA phoneme sequence
        float Rate,                // Speech rate multiplier
        float Pitch,               // Base pitch Hz
        float Volume,              // 0..1
        string Text,               // Plain text (for logging / fallback)
        DiphthongVocalTract.FormantState[] Formants); // Raw formant trajectory

    /// <summary>
    /// Compact neural state vector for social awareness.
    /// This is what one brain "perceives" about the other's internal state —
    /// analogous to reading facial expressions, body language, vocal prosody.
    /// </summary>
    public readonly record struct NeuralStateVector(
        string SourceId,
        long StepIndex,
        float Arousal01,           // General arousal/alertness
        float Valence11,           // Emotional valence (-1 negative, +1 positive)
        float DominantFreqHz,      // Dominant oscillation frequency (alpha/beta/gamma)
        byte MostActiveRegion,     // Region with highest firing rate
        float FiringRate,          // Global mean firing rate
        float AttentionFocus01,    // How focused vs diffuse attention is
        bool IsSpeaking,           // Currently producing speech
        bool IsListening);         // Currently processing incoming speech

    /// <summary>
    /// Peer info: metadata about a connected instance.
    /// </summary>
    public sealed class PeerInfo
    {
        public string Id { get; }
        public string? Name { get; set; }
        public DateTime ConnectedAt { get; }
        public DateTime LastHeardAt { get; set; }
        public NeuralStateVector LastState { get; set; }
        public int SpeechSegmentsReceived { get; set; }
        public int SpeechSegmentsSent { get; set; }

        public PeerInfo(string id)
        {
            Id = id;
            ConnectedAt = DateTime.UtcNow;
            LastHeardAt = DateTime.UtcNow;
        }
    }

    // === INSTANCE IDENTITY ===
    public string InstanceId { get; }
    public string? InstanceName { get; set; }

    // === CONNECTED PEERS ===
    private readonly ConcurrentDictionary<string, PeerInfo> _peers = new();

    // === RELIABLE DELIVERY ENVELOPES ===
    // Internal wire format adds a monotonic per-peer sequence number so the
    // receiver can dedupe retried messages and detect gaps from silent drops.
    // The public records (SpeechSegment, NeuralStateVector) stay unchanged.
    private readonly record struct SpeechEnvelope(long Seq, SpeechSegment Segment);
    private readonly record struct StateEnvelope(long Seq, NeuralStateVector State);

    // === RECEIVE QUEUES (bounded, lock-protected) ===
    // Old design used ConcurrentQueue + TrimQueue which silently dropped the
    // OLDEST on overflow — messages just disappeared with no signal back to the
    // sender. New design uses lock-protected bounded queues with TryEnqueue
    // returning false on overflow. Sender keeps the message in its outbox and
    // retries when the peer drains.
    private readonly object _incomingSpeechLock = new();
    private readonly Queue<SpeechEnvelope> _incomingSpeech = new();
    private readonly object _incomingStateLock = new();
    private readonly Queue<StateEnvelope> _incomingState = new();

    // === RECEIVE PROGRESS ===
    // For each remote sender, the highest sequence we've successfully processed
    // on each channel. Senders poll their peer's progress to prune their outbox.
    // Concurrent because senders read from another bridge instance's map.
    private readonly ConcurrentDictionary<string, long> _speechProgress = new();
    private readonly ConcurrentDictionary<string, long> _stateProgress = new();

    // === RELIABILITY DIAGNOSTICS ===
    private long _speechDuplicatesDropped;
    private long _stateDuplicatesDropped;
    private long _speechGapsDetected;
    private long _stateGapsDetected;
    private long _speechOverflowRejected;
    private long _stateOverflowRejected;
    private long _speechRetryAttempts;
    private long _stateRetryAttempts;
    private long _outboxOverflowDropped;

    // === PER-PEER OUTBOX (sender-side durable storage until ack) ===
    private sealed class Outbox<T>
    {
        public readonly object Lock = new();
        public long NextSeq = 1;
        public long PeerAckedSeq;
        // FIFO of unacked messages. We keep them ordered for retry.
        public readonly Queue<OutboxEntry<T>> Pending = new();
        public DateTime NextRetryUtc = DateTime.MinValue;
        public TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);
        public int OverflowDropCount;
    }

    private readonly record struct OutboxEntry<T>(long Seq, T Payload, DateTime EnqueuedUtc, int AttemptCount);

    private readonly ConcurrentDictionary<string, Outbox<SpeechSegment>> _speechOutbox = new();
    private readonly ConcurrentDictionary<string, Outbox<NeuralStateVector>> _stateOutbox = new();

    // === SHARED HUB (static — connects all instances in the same process) ===
    private static readonly ConcurrentDictionary<string, PeerBridge> _hub = new();

    // === CONFIGURATION ===
    /// <summary>Max messages in receive queue. Senders push past this fail and retry from their outbox.</summary>
    public int MaxQueueDepth { get; set; } = 64;
    /// <summary>Max unacked messages held per peer per channel. Beyond this the OLDEST in outbox is dropped (sender starvation guard).</summary>
    public int MaxOutboxDepth { get; set; } = 256;
    /// <summary>Cap on retry backoff (delay doubles after each failed send up to this).</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    public float StatePublishIntervalSec { get; set; } = 0.05f; // 10Hz state updates
    private float _statePublishTimer;

    public PeerBridge(string? instanceId = null)
    {
        InstanceId = instanceId ?? Guid.NewGuid().ToString("N")[..8];
        _hub[InstanceId] = this;
    }

    // === CONNECTION MANAGEMENT ===

    /// <summary>Connect to another instance by ID.</summary>
    public bool ConnectTo(string peerId)
    {
        if (peerId == InstanceId) return false;
        if (!_hub.TryGetValue(peerId, out var peer)) return false;

        _peers[peerId] = new PeerInfo(peerId) { Name = peer.InstanceName };
        peer._peers[InstanceId] = new PeerInfo(InstanceId) { Name = InstanceName };

        return true;
    }

    /// <summary>Connect to all other instances in the hub.</summary>
    public int ConnectToAll()
    {
        int count = 0;
        foreach (var kvp in _hub)
        {
            if (kvp.Key != InstanceId && !_peers.ContainsKey(kvp.Key))
            {
                if (ConnectTo(kvp.Key)) count++;
            }
        }
        return count;
    }

    /// <summary>Disconnect from a peer. Drops any pending outbox state for it.</summary>
    public void Disconnect(string peerId)
    {
        _peers.TryRemove(peerId, out _);
        _speechOutbox.TryRemove(peerId, out _);
        _stateOutbox.TryRemove(peerId, out _);
        _speechProgress.TryRemove(peerId, out _);
        _stateProgress.TryRemove(peerId, out _);
        if (_hub.TryGetValue(peerId, out var peer))
            peer._peers.TryRemove(InstanceId, out _);
    }

    /// <summary>Get all connected peers.</summary>
    public IReadOnlyCollection<PeerInfo> GetPeers() => _peers.Values.ToArray();

    /// <summary>Get a specific peer's info.</summary>
    public PeerInfo? GetPeer(string peerId) =>
        _peers.TryGetValue(peerId, out var p) ? p : null;

    // === SPEECH ===

    /// <summary>
    /// Send speech to all connected peers. Each peer gets its own monotonic
    /// sequence number; the envelope is stored in a per-peer outbox until the
    /// peer's progress dictionary confirms it has been processed.
    ///
    /// First attempt is made synchronously here; if the peer's receive queue
    /// is full the message stays in the outbox and is retried by
    /// <see cref="ProcessAcksAndRetry"/> on each tick.
    /// </summary>
    public void SendSpeech(SpeechSegment segment)
    {
        foreach (var kvp in _peers)
        {
            var peerId = kvp.Key;
            if (!_hub.TryGetValue(peerId, out var peer))
            {
                continue;
            }

            var outbox = _speechOutbox.GetOrAdd(peerId, _ => new Outbox<SpeechSegment>());
            long seq;
            lock (outbox.Lock)
            {
                seq = outbox.NextSeq++;
                outbox.Pending.Enqueue(new OutboxEntry<SpeechSegment>(seq, segment, DateTime.UtcNow, AttemptCount: 1));
                TrimOutbox(outbox);
            }

            if (TryDeliverSpeech(peer, new SpeechEnvelope(seq, segment)))
            {
                lock (outbox.Lock)
                {
                    outbox.NextRetryUtc = DateTime.MinValue;
                    outbox.RetryDelay = TimeSpan.FromMilliseconds(50);
                }
            }
            else
            {
                Interlocked.Increment(ref _speechOverflowRejected);
                ArmRetry(outbox);
            }

            kvp.Value.SpeechSegmentsSent++;
            if (peer._peers.TryGetValue(InstanceId, out var theirRecordOfUs))
            {
                // Counter incremented on actual receipt below in ReceiveSpeech;
                // here we keep send-side bookkeeping in sync with the prior API.
                _ = theirRecordOfUs; // intentionally no-op; receive-side updates it.
            }
        }
    }

    private static bool TryDeliverSpeech(PeerBridge peer, SpeechEnvelope env)
    {
        lock (peer._incomingSpeechLock)
        {
            if (peer._incomingSpeech.Count >= peer.MaxQueueDepth)
            {
                return false; // bounded — sender must retry
            }
            peer._incomingSpeech.Enqueue(env);
            return true;
        }
    }

    /// <summary>
    /// Send speech from the DiphthongVocalTract.
    /// Convenience method that packages phonemes + formants.
    /// </summary>
    public void SendSpeechFromTract(
        DiphthongVocalTract tract,
        string[] phonemes,
        string text,
        long stepIndex,
        float rate = 1.0f,
        float volume = 0.8f)
    {
        // Collect formant trajectory by simulating the tract
        var formants = new List<DiphthongVocalTract.FormantState>();
        float dt = 1f / 60f; // simulate at 60Hz
        
        // Clone phonemes into the tract and step through
        tract.EnqueuePhonemes(phonemes, rate);
        while (tract.IsSpeaking)
        {
            formants.Add(tract.Step(dt));
        }

        var segment = new SpeechSegment(
            SourceId: InstanceId,
            StepIndex: stepIndex,
            Phonemes: phonemes,
            Rate: rate,
            Pitch: tract.BasePitchHz,
            Volume: volume,
            Text: text,
            Formants: formants.ToArray());

        SendSpeech(segment);
    }

    /// <summary>
    /// Receive incoming speech from peers. Sequence numbers are used internally
    /// to drop duplicates (which a retry path can produce) and detect gaps (which
    /// indicate silent receive-queue overflow on the path). The public output
    /// shape is unchanged: just <see cref="SpeechSegment"/>s.
    /// </summary>
    public SpeechSegment[] ReceiveSpeech(int maxCount = 8)
    {
        var list = new List<SpeechSegment>(maxCount);
        var envelopes = new List<SpeechEnvelope>(maxCount);

        lock (_incomingSpeechLock)
        {
            while (envelopes.Count < maxCount && _incomingSpeech.Count > 0)
            {
                envelopes.Add(_incomingSpeech.Dequeue());
            }
        }

        foreach (var env in envelopes)
        {
            var src = env.Segment.SourceId;
            // Atomic compare-and-update of the per-sender progress watermark.
            // Two cases we silently drop:
            //   - seq <= lastSeen: duplicate from retry path → safely ignore
            //   - seq <  lastSeen + 1: same as above
            // A gap (seq > lastSeen + 1) means the path dropped messages between;
            // we accept the new one and advance the watermark, but tally the gap
            // so it shows in diagnostics.
            while (true)
            {
                var lastSeen = _speechProgress.TryGetValue(src, out var v) ? v : 0L;
                if (env.Seq <= lastSeen)
                {
                    Interlocked.Increment(ref _speechDuplicatesDropped);
                    break;
                }
                if (env.Seq > lastSeen + 1)
                {
                    Interlocked.Increment(ref _speechGapsDetected);
                }
                if (_speechProgress.TryUpdate(src, env.Seq, lastSeen))
                {
                    list.Add(env.Segment);
                    if (_peers.TryGetValue(src, out var peer))
                    {
                        peer.LastHeardAt = DateTime.UtcNow;
                        peer.SpeechSegmentsReceived++;
                    }
                    break;
                }
                if (lastSeen == 0 && _speechProgress.TryAdd(src, env.Seq))
                {
                    list.Add(env.Segment);
                    if (_peers.TryGetValue(src, out var peer))
                    {
                        peer.LastHeardAt = DateTime.UtcNow;
                        peer.SpeechSegmentsReceived++;
                    }
                    break;
                }
                // Lost CAS race; retry the loop with refreshed lastSeen.
            }
        }

        return list.ToArray();
    }

    // === NEURAL STATE ===

    /// <summary>
    /// Publish this instance's neural state to all peers. Sequenced + outbox-
    /// retried just like speech. State updates are higher-volume (~10 Hz default)
    /// than speech, so the overflow path matters more here.
    /// </summary>
    public void PublishState(NeuralStateVector state)
    {
        foreach (var kvp in _peers)
        {
            var peerId = kvp.Key;
            if (!_hub.TryGetValue(peerId, out var peer))
            {
                continue;
            }

            var outbox = _stateOutbox.GetOrAdd(peerId, _ => new Outbox<NeuralStateVector>());
            long seq;
            lock (outbox.Lock)
            {
                seq = outbox.NextSeq++;
                outbox.Pending.Enqueue(new OutboxEntry<NeuralStateVector>(seq, state, DateTime.UtcNow, AttemptCount: 1));
                TrimOutbox(outbox);
            }

            if (TryDeliverState(peer, new StateEnvelope(seq, state)))
            {
                lock (outbox.Lock)
                {
                    outbox.NextRetryUtc = DateTime.MinValue;
                    outbox.RetryDelay = TimeSpan.FromMilliseconds(50);
                }
            }
            else
            {
                Interlocked.Increment(ref _stateOverflowRejected);
                ArmRetry(outbox);
            }
        }
    }

    private static bool TryDeliverState(PeerBridge peer, StateEnvelope env)
    {
        lock (peer._incomingStateLock)
        {
            if (peer._incomingState.Count >= peer.MaxQueueDepth)
            {
                return false;
            }
            peer._incomingState.Enqueue(env);
            return true;
        }
    }

    /// <summary>
    /// Publish state if enough time has elapsed.
    /// Call this every tick with the simulation dt.
    /// </summary>
    public void MaybePublishState(NeuralStateVector state, float dt)
    {
        _statePublishTimer += dt;
        if (_statePublishTimer >= StatePublishIntervalSec)
        {
            _statePublishTimer = 0f;
            PublishState(state);
        }
    }

    /// <summary>
    /// Receive the latest state from each peer. State is high-rate (~10 Hz) so
    /// older state from the same peer is naturally superseded — we keep only the
    /// highest-sequence state per source. Duplicate / out-of-order detection
    /// still applies and is counted in diagnostics.
    /// </summary>
    public Dictionary<string, NeuralStateVector> ReceivePeerStates()
    {
        var envelopes = new List<StateEnvelope>(16);
        lock (_incomingStateLock)
        {
            while (_incomingState.Count > 0)
            {
                envelopes.Add(_incomingState.Dequeue());
            }
        }

        var states = new Dictionary<string, NeuralStateVector>();
        foreach (var env in envelopes)
        {
            var src = env.State.SourceId;
            while (true)
            {
                var lastSeen = _stateProgress.TryGetValue(src, out var v) ? v : 0L;
                if (env.Seq <= lastSeen)
                {
                    Interlocked.Increment(ref _stateDuplicatesDropped);
                    break;
                }
                if (env.Seq > lastSeen + 1)
                {
                    Interlocked.Increment(ref _stateGapsDetected);
                }
                bool advanced = lastSeen == 0
                    ? _stateProgress.TryAdd(src, env.Seq)
                    : _stateProgress.TryUpdate(src, env.Seq, lastSeen);
                if (!advanced) continue; // lost CAS, retry with refreshed lastSeen

                states[src] = env.State;
                if (_peers.TryGetValue(src, out var peer))
                {
                    peer.LastState = env.State;
                    peer.LastHeardAt = DateTime.UtcNow;
                }
                break;
            }
        }

        return states;
    }

    // === DIAGNOSTICS ===

    /// <summary>Summary of bridge state for telemetry/UI.</summary>
    public BridgeSnapshot GetSnapshot()
    {
        var arr = new PeerSnapshot[_peers.Count];
        int i = 0;
        foreach (var p in _peers.Values)
        {
            arr[i++] = new PeerSnapshot(
                Id: p.Id,
                Name: p.Name,
                LastHeardAt: p.LastHeardAt,
                SpeechSegmentsReceived: p.SpeechSegmentsReceived,
                SpeechSegmentsSent: p.SpeechSegmentsSent,
                LastState: p.LastState);
        }

        int pendingSpeech, pendingState;
        lock (_incomingSpeechLock) pendingSpeech = _incomingSpeech.Count;
        lock (_incomingStateLock)  pendingState = _incomingState.Count;

        return new BridgeSnapshot(
            InstanceId: InstanceId,
            InstanceName: InstanceName,
            PeerCount: _peers.Count,
            PendingIncomingSpeech: pendingSpeech,
            PendingIncomingState: pendingState,
            Peers: arr);
    }

    // === CLEANUP ===

    /// <summary>Remove this instance from the hub.</summary>
    public void Dispose()
    {
        // Disconnect from all peers
        // Copy keys first to avoid modifying collection while iterating.
        var keys = new string[_peers.Count];
        int i = 0;
        foreach (var k in _peers.Keys) keys[i++] = k;
        for (int j = 0; j < i; j++) Disconnect(keys[j]);

        _hub.TryRemove(InstanceId, out _);
    }

    /// <summary>Get all instances in the hub (for discovery).</summary>
    public static string[] GetAllInstances()
    {
        // Snapshot keys without LINQ.
        var arr = new string[_hub.Count];
        int i = 0;
        foreach (var k in _hub.Keys) arr[i++] = k;
        if (i != arr.Length) Array.Resize(ref arr, i);
        return arr;
    }


    // ============= RELIABLE-DELIVERY HELPERS =============

    /// <summary>
    /// Periodic retry pass: poll each peer's progress to prune acked entries
    /// from this instance's outboxes, then re-attempt the oldest pending entry
    /// for each peer whose retry timer has expired. Call once per simulation
    /// tick. Cheap when there is nothing to retry (most ticks).
    /// </summary>
    public void ProcessAcksAndRetry(float dt)
    {
        var now = DateTime.UtcNow;

        foreach (var kvp in _peers)
        {
            var peerId = kvp.Key;
            if (!_hub.TryGetValue(peerId, out var peer)) continue;

            // Speech channel ack + retry
            if (_speechOutbox.TryGetValue(peerId, out var sOut))
            {
                long peerAck = peer._speechProgress.TryGetValue(InstanceId, out var sa) ? sa : 0L;
                PruneAcked(sOut, peerAck);

                lock (sOut.Lock)
                {
                    if (sOut.Pending.Count > 0 && now >= sOut.NextRetryUtc)
                    {
                        var head = sOut.Pending.Peek();
                        if (TryDeliverSpeech(peer, new SpeechEnvelope(head.Seq, head.Payload)))
                        {
                            sOut.NextRetryUtc = DateTime.MinValue;
                            sOut.RetryDelay = TimeSpan.FromMilliseconds(50);
                            Interlocked.Increment(ref _speechRetryAttempts);
                            // Replace head with attempt-incremented copy for diagnostics.
                            sOut.Pending.Dequeue();
                            sOut.Pending.Enqueue(head with { AttemptCount = head.AttemptCount + 1 });
                        }
                        else
                        {
                            Interlocked.Increment(ref _speechRetryAttempts);
                            // Exponential backoff, capped.
                            sOut.RetryDelay = TimeSpan.FromMilliseconds(Math.Min(MaxRetryDelay.TotalMilliseconds, sOut.RetryDelay.TotalMilliseconds * 2));
                            sOut.NextRetryUtc = now + sOut.RetryDelay;
                        }
                    }
                }
            }

            // State channel ack + retry
            if (_stateOutbox.TryGetValue(peerId, out var tOut))
            {
                long peerAck = peer._stateProgress.TryGetValue(InstanceId, out var ta) ? ta : 0L;
                PruneAcked(tOut, peerAck);

                lock (tOut.Lock)
                {
                    if (tOut.Pending.Count > 0 && now >= tOut.NextRetryUtc)
                    {
                        var head = tOut.Pending.Peek();
                        if (TryDeliverState(peer, new StateEnvelope(head.Seq, head.Payload)))
                        {
                            tOut.NextRetryUtc = DateTime.MinValue;
                            tOut.RetryDelay = TimeSpan.FromMilliseconds(50);
                            Interlocked.Increment(ref _stateRetryAttempts);
                            tOut.Pending.Dequeue();
                            tOut.Pending.Enqueue(head with { AttemptCount = head.AttemptCount + 1 });
                        }
                        else
                        {
                            Interlocked.Increment(ref _stateRetryAttempts);
                            tOut.RetryDelay = TimeSpan.FromMilliseconds(Math.Min(MaxRetryDelay.TotalMilliseconds, tOut.RetryDelay.TotalMilliseconds * 2));
                            tOut.NextRetryUtc = now + tOut.RetryDelay;
                        }
                    }
                }
            }
        }
    }

    private void PruneAcked<T>(Outbox<T> outbox, long peerAckedSeq)
    {
        lock (outbox.Lock)
        {
            outbox.PeerAckedSeq = Math.Max(outbox.PeerAckedSeq, peerAckedSeq);
            while (outbox.Pending.Count > 0 && outbox.Pending.Peek().Seq <= outbox.PeerAckedSeq)
            {
                outbox.Pending.Dequeue();
            }
        }
    }

    private void ArmRetry<T>(Outbox<T> outbox)
    {
        lock (outbox.Lock)
        {
            if (outbox.NextRetryUtc == DateTime.MinValue)
            {
                outbox.NextRetryUtc = DateTime.UtcNow + outbox.RetryDelay;
            }
        }
    }

    private void TrimOutbox<T>(Outbox<T> outbox)
    {
        // Caller holds outbox.Lock.
        while (outbox.Pending.Count > MaxOutboxDepth)
        {
            outbox.Pending.Dequeue();
            outbox.OverflowDropCount++;
            Interlocked.Increment(ref _outboxOverflowDropped);
        }
    }

    /// <summary>
    /// Reliability counters snapshot for diagnostics/UI.
    /// </summary>
    public ReliabilitySnapshot GetReliabilitySnapshot()
    {
        int pendingSpeech = 0, pendingState = 0;
        foreach (var ob in _speechOutbox.Values)
        {
            lock (ob.Lock) pendingSpeech += ob.Pending.Count;
        }
        foreach (var ob in _stateOutbox.Values)
        {
            lock (ob.Lock) pendingState += ob.Pending.Count;
        }

        return new ReliabilitySnapshot(
            SpeechDuplicatesDropped: Interlocked.Read(ref _speechDuplicatesDropped),
            StateDuplicatesDropped: Interlocked.Read(ref _stateDuplicatesDropped),
            SpeechGapsDetected: Interlocked.Read(ref _speechGapsDetected),
            StateGapsDetected: Interlocked.Read(ref _stateGapsDetected),
            SpeechOverflowRejected: Interlocked.Read(ref _speechOverflowRejected),
            StateOverflowRejected: Interlocked.Read(ref _stateOverflowRejected),
            SpeechRetryAttempts: Interlocked.Read(ref _speechRetryAttempts),
            StateRetryAttempts: Interlocked.Read(ref _stateRetryAttempts),
            OutboxOverflowDropped: Interlocked.Read(ref _outboxOverflowDropped),
            PendingSpeechMessages: pendingSpeech,
            PendingStateMessages: pendingState);
    }
}

public readonly record struct ReliabilitySnapshot(
    long SpeechDuplicatesDropped,
    long StateDuplicatesDropped,
    long SpeechGapsDetected,
    long StateGapsDetected,
    long SpeechOverflowRejected,
    long StateOverflowRejected,
    long SpeechRetryAttempts,
    long StateRetryAttempts,
    long OutboxOverflowDropped,
    int PendingSpeechMessages,
    int PendingStateMessages);

// === SNAPSHOT TYPES ===

public readonly record struct BridgeSnapshot(
    string InstanceId,
    string? InstanceName,
    int PeerCount,
    int PendingIncomingSpeech,
    int PendingIncomingState,
    PeerSnapshot[] Peers);

public readonly record struct PeerSnapshot(
    string Id,
    string? Name,
    DateTime LastHeardAt,
    int SpeechSegmentsReceived,
    int SpeechSegmentsSent,
    PeerBridge.NeuralStateVector LastState);
