using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NeuralResonanceEngine.Protocol;

internal sealed class SynapsePersistenceStore : IDisposable
{
	internal const int CurrentSchemaVersion = 2;
	private const int DefaultSaveEveryMutationCount = 4096;
	private const double DefaultSaveEveryMs = 10000.0;
	private const int DefaultSensoryInboundSynapseLimit = 65_536;
	private const int DefaultGeneralInboundSynapseLimit = 262_144;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = false
	};

	private readonly StructureId _structureId;
	private readonly string _instanceKey;
	private readonly string _path;
	private readonly int _saveEveryMutationCount;
	private readonly double _saveEveryMs;
	private readonly int _maxInboundSynapseCount;
	private int _pendingMutations;
	private long _lastSaveTimestamp;
	private long _totalPrunedInboundSynapses;

	// Off-lock async writer. Snapshot construction happens on the tick-loop thread
	// (which holds the dictionary lock), and the resulting frozen snapshot is handed
	// to a single background writer task. Channel capacity is 1 with DropOldest so
	// rapid mutations coalesce into the latest snapshot instead of queueing up disk I/O.
	private readonly Channel<SynapseStoreSnapshot> _writeQueue = Channel.CreateBounded<SynapseStoreSnapshot>(
		new BoundedChannelOptions(1)
		{
			SingleReader = true,
			SingleWriter = false,
			FullMode = BoundedChannelFullMode.DropOldest
		});
	private readonly CancellationTokenSource _writerCts = new();
	private readonly Task _writerTask;
	private int _disposed;

	public SynapsePersistenceStore(StructureId structureId)
	{
		_structureId = structureId;
		_instanceKey = ResolveInstanceKey(structureId);
		_path = ResolvePath(_instanceKey);
		_saveEveryMutationCount = ResolvePositiveInt("NRE_SYNAPSE_SAVE_MUTATIONS", DefaultSaveEveryMutationCount, 64, 1_000_000);
		_saveEveryMs = ResolvePositiveDouble("NRE_SYNAPSE_SAVE_INTERVAL_MS", DefaultSaveEveryMs, 1000.0, 300_000.0);
		_maxInboundSynapseCount = ResolveInboundSynapseLimit(structureId);
		_lastSaveTimestamp = Stopwatch.GetTimestamp();
		_writerTask = Task.Run(WriterLoopAsync);
	}

	public void Load(Dictionary<Guid, SynapseState> inboundSynapses, Dictionary<string, SynapseState> outboundSynapses)
	{
		if (!File.Exists(_path))
		{
			return;
		}

		try
		{
			string json = File.ReadAllText(_path);
			SynapseStoreSnapshot? snapshot = JsonSerializer.Deserialize<SynapseStoreSnapshot>(json, JsonOptions);
			if (snapshot == null || snapshot.StructureId != _structureId ||
				(!string.IsNullOrWhiteSpace(snapshot.InstanceKey) && !string.Equals(snapshot.InstanceKey, _instanceKey, StringComparison.OrdinalIgnoreCase)))
			{
				return;
			}
			if (snapshot.SchemaVersion != CurrentSchemaVersion)
			{
				Console.Error.WriteLine(
					$"[{_structureId}] Synapse state generation {snapshot.SchemaVersion} ignored; generation {CurrentSchemaVersion} is required.");
				return;
			}

			foreach (SynapseStoreEntry entry in snapshot.Inbound)
			{
				if (entry.SynapseId == Guid.Empty)
				{
					continue;
				}

				inboundSynapses[entry.SynapseId] = entry.ToSynapseState();
			}

			foreach (SynapseStoreEntry entry in snapshot.Outbound)
			{
				if (string.IsNullOrWhiteSpace(entry.Key) || entry.SynapseId == Guid.Empty)
				{
					continue;
				}

				outboundSynapses[entry.Key] = entry.ToSynapseState();
			}

			_totalPrunedInboundSynapses += PruneInboundSynapses(inboundSynapses, _maxInboundSynapseCount);

			_lastSaveTimestamp = Stopwatch.GetTimestamp();
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
		{
			Console.Error.WriteLine($"[{_structureId}] Synapse state load skipped: {ex.Message}");
		}
	}

	public void MarkChanged(
		Dictionary<Guid, SynapseState> inboundSynapses,
		Dictionary<string, SynapseState> outboundSynapses,
		double timestampMs)
	{
		_pendingMutations++;
		// Hot path: called once per inbound and once per outbound spike. Skip the
		// CurrentMilliseconds() syscall and the snapshot copy until the mutation gate
		// fires or we periodically sample the time gate every 64 mutations.
		if (_pendingMutations < _saveEveryMutationCount && (_pendingMutations & 0xFF) != 0)
		{
			return;
		}

		long now = Stopwatch.GetTimestamp();
		if (_pendingMutations < _saveEveryMutationCount && Stopwatch.GetElapsedTime(_lastSaveTimestamp, now).TotalMilliseconds < _saveEveryMs)
		{
			return;
		}

		// Build the snapshot under the caller's lock, then hand it to the background
		// writer. The disk write happens off the tick-loop thread.
		var snapshot = BuildSnapshot(inboundSynapses, outboundSynapses);
		_writeQueue.Writer.TryWrite(snapshot);
		_pendingMutations = 0;
		_lastSaveTimestamp = now;
	}

	public void Save(Dictionary<Guid, SynapseState> inboundSynapses, Dictionary<string, SynapseState> outboundSynapses)
	{
		// Synchronous save path used at shutdown so the final state is on disk before
		// the process exits. Async-writer may still have a stale snapshot pending; the
		// disposer drains it.
		var snapshot = BuildSnapshot(inboundSynapses, outboundSynapses);
		WriteSnapshot(snapshot);
		_pendingMutations = 0;
		_lastSaveTimestamp = Stopwatch.GetTimestamp();
	}

	public void SaveAndDispose(Dictionary<Guid, SynapseState> inboundSynapses, Dictionary<string, SynapseState> outboundSynapses)
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		// Let the single writer finish its latest coalesced snapshot before writing
		// the final state. Writing first allowed a stale queued snapshot to overwrite
		// the last synchronous save during shutdown.
		_writeQueue.Writer.TryComplete();
		try
		{
			_writerTask.GetAwaiter().GetResult();
		}
		catch (OperationCanceledException)
		{
		}

		Save(inboundSynapses, outboundSynapses);
		_writerCts.Dispose();
	}

	private SynapseStoreSnapshot BuildSnapshot(
		Dictionary<Guid, SynapseState> inboundSynapses,
		Dictionary<string, SynapseState> outboundSynapses)
	{
		_totalPrunedInboundSynapses += PruneInboundSynapses(inboundSynapses, _maxInboundSynapseCount);
		return new SynapseStoreSnapshot
		{
			SchemaVersion = CurrentSchemaVersion,
			StructureId = _structureId,
			InstanceKey = _instanceKey,
			SavedAtUtc = DateTimeOffset.UtcNow,
			Inbound = CreateInboundEntries(inboundSynapses),
			Outbound = CreateOutboundEntries(outboundSynapses)
		};
	}

	internal int MaxInboundSynapseCount => _maxInboundSynapseCount;

	internal long TotalPrunedInboundSynapses => Interlocked.Read(ref _totalPrunedInboundSynapses);

	internal static int PruneInboundSynapses(Dictionary<Guid, SynapseState> synapses, int maximumCount)
	{
		ArgumentNullException.ThrowIfNull(synapses);
		maximumCount = Math.Max(1, maximumCount);
		var removeCount = synapses.Count - maximumCount;
		if (removeCount <= 0)
		{
			return 0;
		}

		// Synaptic homeostasis removes the least supported connections first.
		// Repetition is primary; Hebbian divergence, eligibility, and tagging break
		// ties before recency and stable identity make the result deterministic.
		var remove = synapses
			.OrderBy(static pair => pair.Value.UpdateCount)
			.ThenBy(static pair => HebbianRetentionEvidence(pair.Value))
			.ThenBy(static pair => pair.Value.LastUpdateTimestampMs)
			.ThenBy(static pair => pair.Key)
			.Take(removeCount)
			.Select(static pair => pair.Key)
			.ToArray();
		foreach (var synapseId in remove)
		{
			synapses.Remove(synapseId);
		}

		return remove.Length;
	}

	private static double HebbianRetentionEvidence(SynapseState synapse)
	{
		synapse.Stabilize();
		return Math.Abs(synapse.VesicleQuanta - synapse.BaselineVesicleQuanta) +
			Math.Abs(synapse.EligibilityTrace) +
			Math.Abs(synapse.SynapticTagTrace) +
			(synapse.PreTrace * 0.05) +
			(synapse.PostTrace * 0.05);
	}

	private async Task WriterLoopAsync()
	{
		try
		{
			await foreach (var snapshot in _writeQueue.Reader.ReadAllAsync(_writerCts.Token).ConfigureAwait(false))
			{
				WriteSnapshot(snapshot);
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private void WriteSnapshot(SynapseStoreSnapshot snapshot)
	{
		try
		{
			string? directory = Path.GetDirectoryName(_path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			string json = JsonSerializer.Serialize(snapshot, JsonOptions);
			string tempPath = _path + ".tmp";
			File.WriteAllText(tempPath, json);

			// Atomic replace: File.Replace is the atomic same-volume swap on
			// Windows and is implemented on top of `rename(2)` on POSIX. When the
			// target does not yet exist, fall back to File.Move (overwrite: true).
			if (File.Exists(_path))
			{
				File.Replace(tempPath, _path, destinationBackupFileName: null);
			}
			else
			{
				File.Move(tempPath, _path, overwrite: true);
			}
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			Console.Error.WriteLine($"[{_structureId}] Synapse state save skipped: {ex.Message}");
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		_writeQueue.Writer.TryComplete();
		try
		{
			_writerTask.GetAwaiter().GetResult();
		}
		catch (OperationCanceledException)
		{
		}

		_writerCts.Dispose();
	}

	private static List<SynapseStoreEntry> CreateInboundEntries(Dictionary<Guid, SynapseState> synapses)
	{
		var entries = new List<SynapseStoreEntry>(synapses.Count);
		foreach (SynapseState synapse in synapses.Values)
		{
			entries.Add(SynapseStoreEntry.FromSynapseState(null, synapse));
		}

		return entries;
	}

	private static List<SynapseStoreEntry> CreateOutboundEntries(Dictionary<string, SynapseState> synapses)
	{
		var entries = new List<SynapseStoreEntry>(synapses.Count);
		foreach (KeyValuePair<string, SynapseState> pair in synapses)
		{
			entries.Add(SynapseStoreEntry.FromSynapseState(pair.Key, pair.Value));
		}

		return entries;
	}

	private static string ResolveInstanceKey(StructureId structureId)
	{
		string? configured = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
		if (!string.IsNullOrWhiteSpace(configured))
		{
			return configured.Trim();
		}

		string? hemisphere = Environment.GetEnvironmentVariable("HEMISPHERE");
		return string.IsNullOrWhiteSpace(hemisphere)
			? structureId.ToString()
			: $"{structureId}_{hemisphere.Trim().ToUpperInvariant()}";
	}

	private static string ResolvePath(string instanceKey)
	{
		string? configuredDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
		string root = !string.IsNullOrWhiteSpace(configuredDirectory)
			? configuredDirectory
			: Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"NeuralResonanceEngine",
				"synapses");

		var invalid = Path.GetInvalidFileNameChars();
		var safeInstanceKey = string.Concat(instanceKey.Select(character => invalid.Contains(character) ? '_' : character));
		return Path.Combine(root, $"{safeInstanceKey}.synapses.json");
	}

	private static int ResolvePositiveInt(string name, int fallback, int minimum, int maximum) =>
		int.TryParse(Environment.GetEnvironmentVariable(name), out var parsed)
			? Math.Clamp(parsed, minimum, maximum)
			: fallback;

	private static double ResolvePositiveDouble(string name, double fallback, double minimum, double maximum) =>
		double.TryParse(Environment.GetEnvironmentVariable(name), out var parsed) && double.IsFinite(parsed)
			? Math.Clamp(parsed, minimum, maximum)
			: fallback;

	private static int ResolveInboundSynapseLimit(StructureId structureId)
	{
		var generalOverride = Environment.GetEnvironmentVariable("NRE_SYNAPSE_MAX_INBOUND");
		if (int.TryParse(generalOverride, out var configured))
		{
			return Math.Clamp(configured, 1_024, 10_000_000);
		}

		if (!IsHighVolumeSensoryStructure(structureId))
		{
			return DefaultGeneralInboundSynapseLimit;
		}

		return ResolvePositiveInt(
			"NRE_SENSORY_SYNAPSE_MAX_INBOUND",
			DefaultSensoryInboundSynapseLimit,
			1_024,
			10_000_000);
	}

	private static bool IsHighVolumeSensoryStructure(StructureId structureId) => structureId is
		StructureId.Retina or
		StructureId.V1 or
		StructureId.V2 or
		StructureId.V4 or
		StructureId.Mt or
		StructureId.Cochlea or
		StructureId.CochlearNucleus or
		StructureId.SuperiorOlive or
		StructureId.InferiorColliculus or
		StructureId.A1 or
		StructureId.SomaticAfferents or
		StructureId.S1 or
		StructureId.ProprioceptiveAfferents or
		StructureId.VestibularAfferents or
		StructureId.VisceralAfferents;

	private sealed class SynapseStoreSnapshot
	{
		public int SchemaVersion { get; set; }

		public StructureId StructureId { get; set; }

		public string InstanceKey { get; set; } = string.Empty;

		public DateTimeOffset SavedAtUtc { get; set; }

		public List<SynapseStoreEntry> Inbound { get; set; } = new List<SynapseStoreEntry>();

		public List<SynapseStoreEntry> Outbound { get; set; } = new List<SynapseStoreEntry>();
	}

	private sealed class SynapseStoreEntry
	{
		public string? Key { get; set; }

		public Guid SynapseId { get; set; }

		public NTEnum Neurotransmitter { get; set; }

		public float VesicleQuanta { get; set; }

		public float BaselineVesicleQuanta { get; set; }

		public float PreTrace { get; set; }

		public float PostTrace { get; set; }

		public float ThetaM { get; set; }

		public float EligibilityTrace { get; set; }

		public float SynapticTagTrace { get; set; }

		public double LastUpdateTimestampMs { get; set; }

		public int LastTargetNeuronIndex { get; set; }

		public int UpdateCount { get; set; }

		public static SynapseStoreEntry FromSynapseState(string? key, SynapseState synapse)
		{
			synapse.Stabilize();
			return new SynapseStoreEntry
			{
				Key = key,
				SynapseId = synapse.SynapseId,
				Neurotransmitter = synapse.Neurotransmitter,
				VesicleQuanta = synapse.VesicleQuanta,
				BaselineVesicleQuanta = synapse.BaselineVesicleQuanta,
				PreTrace = synapse.PreTrace,
				PostTrace = synapse.PostTrace,
				ThetaM = synapse.ThetaM,
				EligibilityTrace = synapse.EligibilityTrace,
				SynapticTagTrace = synapse.SynapticTagTrace,
				LastUpdateTimestampMs = synapse.LastUpdateTimestampMs,
				LastTargetNeuronIndex = synapse.LastTargetNeuronIndex,
				UpdateCount = synapse.UpdateCount
			};
		}

		public SynapseState ToSynapseState()
		{
			var baseline = BaselineVesicleQuanta > 0f ? BaselineVesicleQuanta : VesicleQuanta;
			var synapse = new SynapseState(
				SynapseId,
				Neurotransmitter,
				PlasticityRules.ClampQuanta(VesicleQuanta),
				PlasticityRules.ClampQuanta(baseline))
			{
				PreTrace = PreTrace,
				PostTrace = PostTrace,
				ThetaM = ThetaM,
				EligibilityTrace = EligibilityTrace,
				SynapticTagTrace = SynapticTagTrace,
				LastUpdateTimestampMs = LastUpdateTimestampMs,
				LastTargetNeuronIndex = LastTargetNeuronIndex,
				UpdateCount = UpdateCount
			};
			synapse.Stabilize();
			return synapse;
		}
	}
}
