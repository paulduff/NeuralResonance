namespace NRE.Core.Engine;

/// <summary>
/// Cortical Microcircuit: Realistic E/I populations with interneuron subtypes.
/// 
/// Biology:
/// - ~80% excitatory pyramidal cells, ~20% inhibitory interneurons
/// - Interneuron subtypes: PV+ (fast-spiking, perisomatic), SOM+ (dendrite-targeting), 
///   VIP+ (interneuron-targeting, disinhibition)
/// - Canonical circuit: Feedforward → L4 → L2/3 → L5/6 (simplified to E/I dynamics)
/// 
/// References:
/// - Markram et al. 2004 (canonical microcircuit)
/// - Tremblay et al. 2016 (interneuron diversity)
/// - Pfeffer et al. 2013 (VIP disinhibition)
/// </summary>
public sealed class CorticalMicrocircuit
{
    private readonly object _gate = new();
    
    /// <summary>Neuron type enumeration for per-voxel assignment.</summary>
    public enum NeuronType : byte
    {
        Pyramidal = 0,      // Excitatory, ~80% of cortical neurons
        PVInterneuron = 1,  // Fast-spiking, perisomatic inhibition
        SOMInterneuron = 2, // Low-threshold spiking, dendritic inhibition
        VIPInterneuron = 3, // Disinhibitory (inhibits other interneurons)
        Subcortical = 4     // Non-cortical regions use default dynamics
    }
    
    // Population ratios (biologically justified)
    public float PyramidalRatio { get; set; } = 0.80f;
    public float PVRatio { get; set; } = 0.10f;        // ~50% of interneurons
    public float SOMRatio { get; set; } = 0.07f;       // ~35% of interneurons
    public float VIPRatio { get; set; } = 0.03f;       // ~15% of interneurons
    
    // Type-specific parameters
    private readonly Dictionary<NeuronType, NeuronParameters> _params = new()
    {
        [NeuronType.Pyramidal] = new NeuronParameters
        {
            ThresholdMod = 1.0f,
            LeakMod = 1.0f,
            RefractoryMs = 2.0f,
            SynapticSign = 1.0f,      // Excitatory
            AdaptationStrength = 0.15f,
            BurstPropensity = 0.3f
        },
        [NeuronType.PVInterneuron] = new NeuronParameters
        {
            ThresholdMod = 0.85f,      // Lower threshold (faster to spike)
            LeakMod = 1.3f,            // Faster membrane dynamics
            RefractoryMs = 0.5f,       // Very short refractory (fast-spiking)
            SynapticSign = -1.0f,      // Inhibitory
            AdaptationStrength = 0.02f,// Little adaptation
            BurstPropensity = 0.0f     // No bursting
        },
        [NeuronType.SOMInterneuron] = new NeuronParameters
        {
            ThresholdMod = 0.90f,
            LeakMod = 0.95f,           // Slightly slower
            RefractoryMs = 1.5f,
            SynapticSign = -1.0f,      // Inhibitory
            AdaptationStrength = 0.25f,// Strong adaptation
            BurstPropensity = 0.4f     // Can burst at low threshold
        },
        [NeuronType.VIPInterneuron] = new NeuronParameters
        {
            ThresholdMod = 0.95f,
            LeakMod = 1.1f,
            RefractoryMs = 1.0f,
            SynapticSign = -1.0f,      // Inhibitory (but targets interneurons)
            AdaptationStrength = 0.10f,
            BurstPropensity = 0.1f,
            TargetsInterneurons = true // Special: disinhibitory
        },
        [NeuronType.Subcortical] = new NeuronParameters
        {
            ThresholdMod = 1.0f,
            LeakMod = 1.0f,
            RefractoryMs = 2.0f,
            SynapticSign = 1.0f,
            AdaptationStrength = 0.10f,
            BurstPropensity = 0.2f
        }
    };
    
    // Per-voxel type assignment (populated during initialization)
    private byte[]? _neuronTypes;
    private float[]? _adaptation;     // Spike-frequency adaptation state
    private float[]? _refractoryTimer;// Refractory period remaining
    
    private int _w, _h, _d;
    private bool _initialized;
    
    /// <summary>
    /// Initialize neuron type assignments for a volume.
    /// Should be called once after NeuralVolume creation.
    /// </summary>
    public void Initialize(int w, int h, int d, byte[] regionIds, Random rng)
    {
        // lock removed for performance
        {
            _w = w; _h = h; _d = d;
            _neuronTypes = new byte[w * h * d];
            _adaptation = new float[w * h * d];
            _refractoryTimer = new float[w * h * d];
            
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            for (int z = 0; z < d; z++)
            {
                byte region = regionIds[(z * h + y) * w + x];
                
                // Only cortical regions (9-13) get E/I diversity
                if (region >= 9 && region <= 13)
                {
                    float r = (float)rng.NextDouble();
                    
                    if (r < PyramidalRatio)
                        _neuronTypes[(z * _h + y) * _w + x] = (byte)NeuronType.Pyramidal;
                    else if (r < PyramidalRatio + PVRatio)
                        _neuronTypes[(z * _h + y) * _w + x] = (byte)NeuronType.PVInterneuron;
                    else if (r < PyramidalRatio + PVRatio + SOMRatio)
                        _neuronTypes[(z * _h + y) * _w + x] = (byte)NeuronType.SOMInterneuron;
                    else
                        _neuronTypes[(z * _h + y) * _w + x] = (byte)NeuronType.VIPInterneuron;
                }
                else
                {
                    _neuronTypes[(z * _h + y) * _w + x] = (byte)NeuronType.Subcortical;
                }
                
                _adaptation[(z * _h + y) * _w + x] = 0f;
                _refractoryTimer[(z * _h + y) * _w + x] = 0f;
            }
            
            _initialized = true;
        }
    }
    
    /// <summary>Get neuron type at voxel.</summary>
    public NeuronType GetNeuronType(int x, int y, int z)
    {
        if (!_initialized || _neuronTypes == null) return NeuronType.Pyramidal;
        if (x < 0 || x >= _w || y < 0 || y >= _h || z < 0 || z >= _d) return NeuronType.Pyramidal;
        return (NeuronType)_neuronTypes[(z * _h + y) * _w + x];
    }
    
    /// <summary>Get parameters for neuron type.</summary>
    public NeuronParameters GetParameters(NeuronType type)
    {
        if (_params.TryGetValue(type, out var p)) return p;
        return _params[NeuronType.Pyramidal];
    }
    
    /// <summary>Get parameters at voxel location.</summary>
    public NeuronParameters GetParametersAt(int x, int y, int z)
        => GetParameters(GetNeuronType(x, y, z));
    
    /// <summary>
    /// Apply spike-frequency adaptation.
    /// Called when a neuron fires.
    /// </summary>
    public void OnSpike(int x, int y, int z, float dt)
    {
        if (!_initialized || _adaptation == null || _refractoryTimer == null) return;
        
        var p = GetParametersAt(x, y, z);
        
        // lock removed for performance
        {
            // Increase adaptation (reduces excitability)
            _adaptation[(z * _h + y) * _w + x] += p.AdaptationStrength;
            _adaptation[(z * _h + y) * _w + x] = MathF.Min(1f, _adaptation[(z * _h + y) * _w + x]);
            
            // Start refractory period
            _refractoryTimer[(z * _h + y) * _w + x] = p.RefractoryMs / 1000f;
        }
    }
    
    /// <summary>
    /// Update adaptation and refractory states.
    /// Called each simulation step.
    /// </summary>
    public void Step(float dt)
    {
        if (!_initialized || _adaptation == null || _refractoryTimer == null) return;
        
        // lock removed for performance
        {
            float adaptDecay = 1f - dt * 2f; // ~500ms time constant
            
            for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            for (int z = 0; z < _d; z++)
            {
                // Decay adaptation
                _adaptation[(z * _h + y) * _w + x] *= adaptDecay;
                
                // Decay refractory
                if (_refractoryTimer[(z * _h + y) * _w + x] > 0)
                    _refractoryTimer[(z * _h + y) * _w + x] = MathF.Max(0, _refractoryTimer[(z * _h + y) * _w + x] - dt);
            }
        }
    }
    
    /// <summary>
    /// Get effective threshold modifier including adaptation and refractory.
    /// </summary>
    public float GetEffectiveThresholdMod(int x, int y, int z)
    {
        if (!_initialized || _adaptation == null || _refractoryTimer == null)
            return 1f;
            
        var p = GetParametersAt(x, y, z);
        float adapt = _adaptation[(z * _h + y) * _w + x];
        float refrac = _refractoryTimer[(z * _h + y) * _w + x];
        
        // Base threshold modifier from neuron type
        float mod = p.ThresholdMod;
        
        // Adaptation increases effective threshold
        mod += adapt * 0.3f;
        
        // Refractory period greatly increases threshold
        if (refrac > 0)
            mod += 10f; // Effectively prevents firing
            
        return mod;
    }
    
    /// <summary>
    /// Get synaptic sign (+1 excitatory, -1 inhibitory).
    /// </summary>
    public float GetSynapticSign(int x, int y, int z)
        => GetParametersAt(x, y, z).SynapticSign;
    
    /// <summary>
    /// Check if this neuron targets interneurons (VIP disinhibition).
    /// </summary>
    public bool TargetsInterneurons(int x, int y, int z)
        => GetParametersAt(x, y, z).TargetsInterneurons;
    
    /// <summary>
    /// Get statistics for monitoring.
    /// </summary>
    public MicrocircuitStats GetStats()
    {
        if (!_initialized || _neuronTypes == null)
            return new MicrocircuitStats(0, 0, 0, 0, 0, 0);
            
        int pyr = 0, pv = 0, som = 0, vip = 0, sub = 0;
        float totalAdapt = 0;
        
        // lock removed for performance
        {
            for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            for (int z = 0; z < _d; z++)
            {
                switch ((NeuronType)_neuronTypes[(z * _h + y) * _w + x])
                {
                    case NeuronType.Pyramidal: pyr++; break;
                    case NeuronType.PVInterneuron: pv++; break;
                    case NeuronType.SOMInterneuron: som++; break;
                    case NeuronType.VIPInterneuron: vip++; break;
                    case NeuronType.Subcortical: sub++; break;
                }
                totalAdapt += _adaptation![(z * _h + y) * _w + x];
            }
        }
        
        int total = _w * _h * _d;
        return new MicrocircuitStats(pyr, pv, som, vip, sub, totalAdapt / total);
    }
}

/// <summary>Parameters for each neuron type.</summary>
public sealed class NeuronParameters
{
    public float ThresholdMod { get; init; } = 1.0f;
    public float LeakMod { get; init; } = 1.0f;
    public float RefractoryMs { get; init; } = 2.0f;
    public float SynapticSign { get; init; } = 1.0f;
    public float AdaptationStrength { get; init; } = 0.1f;
    public float BurstPropensity { get; init; } = 0.2f;
    public bool TargetsInterneurons { get; init; } = false;
}

/// <summary>Statistics snapshot for monitoring.</summary>
public readonly record struct MicrocircuitStats(
    int PyramidalCount,
    int PVCount,
    int SOMCount,
    int VIPCount,
    int SubcorticalCount,
    float MeanAdaptation);
