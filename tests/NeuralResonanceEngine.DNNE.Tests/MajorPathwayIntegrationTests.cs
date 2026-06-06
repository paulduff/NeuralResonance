using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using Xunit;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MajorPathwayIntegrationTests
{
    private static readonly string ConnectivityPath = ResolveConnectivityPath();
    private static readonly Lazy<Task<Dictionary<StructureId, List<StructureId>>>> ConnectivityGraphLazy =
        new(LoadGraphCoreAsync);
    private static readonly Lazy<Task<List<ConnectivityRuleJson>>> ConnectivityRulesLazy =
        new(LoadRulesCoreAsync);

    [Fact]
    public async Task Visual_Pathway_V1_To_Pfc_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.V1, StructureId.Pfc));
    }

    [Fact]
    public async Task Auditory_Pathway_A1_To_Pfc_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.A1, StructureId.Pfc));
    }

    [Fact]
    public async Task Language_Pathway_A1_To_Broca_To_M1_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.A1, StructureId.WernickePstgPsts));
        Assert.True(HasPath(graph, StructureId.WernickePstgPsts, StructureId.BrocaBa44Ba45));
        Assert.True(HasPath(graph, StructureId.BrocaBa44Ba45, StructureId.M1));
    }

    [Fact]
    public async Task Somatosensory_Pathway_S1_To_M1_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.S1, StructureId.M1));
    }

    [Fact]
    public async Task Homuncular_Sensorimotor_Connectome_Uses_Biological_Relay_Order()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.S1, StructureId.Ppc, "somatosensory_integration"));
        Assert.True(HasDirectConnection(rules, StructureId.S1, StructureId.M1, "sensorimotor_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.PremotorCortex, StructureId.M1));
        Assert.True(HasDirectConnection(rules, StructureId.Sma, StructureId.M1));
        Assert.True(HasDirectConnection(rules, StructureId.MotorThalamus, StructureId.M1));
        Assert.True(HasDirectConnection(rules, StructureId.M1, StructureId.SpinalCordMotor, "corticospinal_output"));
    }

    [Fact]
    public async Task Ppc_BodySchema_Connectome_Receives_Multimodal_Body_And_Spatial_Inputs()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.S1, StructureId.Ppc, "somatosensory_integration"));
        Assert.True(HasDirectConnection(rules, StructureId.Mt, StructureId.Ppc, "motion_spatial_attention"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.Ppc, "vestibulo_parietal_spatial"));
        Assert.True(HasDirectConnection(rules, StructureId.Pulvinar, StructureId.Ppc, "pulvinar_parietal_attention"));
        Assert.True(HasDirectConnection(rules, StructureId.RetrosplenialCortex, StructureId.Ppc, "spatial_reference_transform"));
        Assert.True(HasDirectConnection(rules, StructureId.Ppc, StructureId.Sma, "spatial_to_motor"));
    }

    [Fact]
    public async Task Hippocampal_Loop_Ec_Dg_Ca3_Ca1_Subiculum_Ec_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.EntorhinalCortex, StructureId.Subiculum));
        Assert.True(HasPath(graph, StructureId.Subiculum, StructureId.EntorhinalCortex));
    }

    [Fact]
    public async Task BasalGanglia_Loop_Snr_To_Thalamus_To_Pfc_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.Snr, StructureId.Thalamus));
        Assert.True(HasPath(graph, StructureId.Thalamus, StructureId.Pfc));
    }

    [Fact]
    public async Task BasalGanglia_ActionSelection_Connectome_Uses_Direct_Indirect_And_Hyperdirect_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.Striatum, StructureId.GPi, "direct_pathway_gpi"));
        Assert.True(HasDirectConnection(rules, StructureId.Striatum, StructureId.Snr, "direct_pathway"));
        Assert.True(HasDirectConnection(rules, StructureId.Striatum, StructureId.GPe, "indirect_pathway_gpe"));
        Assert.True(HasDirectConnection(rules, StructureId.GPe, StructureId.Stn));
        Assert.True(HasDirectConnection(rules, StructureId.Stn, StructureId.GPi, "stn_gpi_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Sma, StructureId.Stn, "hyperdirect_pathway"));
        Assert.True(HasDirectConnection(rules, StructureId.GPi, StructureId.MotorThalamus));
        Assert.True(HasDirectConnection(rules, StructureId.Snr, StructureId.MotorThalamus));
        Assert.True(HasDirectConnection(rules, StructureId.Snc, StructureId.Striatum, "nigrostriatal_reward_prediction"));
    }

    [Fact]
    public async Task Cerebellar_Error_Correction_Dcn_To_M1_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.DeepCerebellarNuclei, StructureId.M1));
    }

    [Fact]
    public async Task Cerebellar_Correction_Connectome_Uses_Mossy_Climbing_Purkinje_And_Dcn_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarGranule, StructureId.PurkinjeCellLayer, "parallel_fiber"));
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarLobules, StructureId.PurkinjeCellLayer, "lobular_parallel_fiber"));
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarVermis, StructureId.PurkinjeCellLayer, "vermis_purkinje_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.InferiorOlive, StructureId.PurkinjeCellLayer, "climbing_fiber_teaching"));
        Assert.True(HasDirectConnection(rules, StructureId.PurkinjeCellLayer, StructureId.DeepCerebellarNuclei, "purkinje_inhibition"));
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarVermis, StructureId.DeepCerebellarNuclei, "vermis_dcn_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarLobules, StructureId.DeepCerebellarNuclei, "lobular_dcn_output"));
        Assert.True(HasDirectConnection(rules, StructureId.DeepCerebellarNuclei, StructureId.MotorThalamus, "cerebellothalamic_motor"));
    }

    [Fact]
    public async Task VestibuloReticular_Posture_Connectome_Uses_Balance_Arousal_And_SpinalTone_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.CerebellarVermis, "vestibulo_cerebellar_alignment"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.ReticularFormation, "vestibulo_reticular_posture"));
        Assert.True(HasDirectConnection(rules, StructureId.DeepCerebellarNuclei, StructureId.VestibularNuclei, "cerebellovestibular_balance_correction"));
        Assert.True(HasDirectConnection(rules, StructureId.DeepCerebellarNuclei, StructureId.ReticularFormation, "cerebelloreticular_posture_correction"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.SpinalCordMotor, "reticulospinal_posture"));
        Assert.True(HasDirectConnection(rules, StructureId.SpinalCordMotor, StructureId.CerebellarGranule, "spinocerebellar_mossy_input"));
        Assert.True(HasDirectConnection(rules, StructureId.SpinalCordMotor, StructureId.ReticularFormation, "spinal_state_feedback"));
    }

    [Fact]
    public async Task SuperiorColliculus_Orienting_Connectome_Uses_Visual_Auditory_Nigrotectal_And_HeadEye_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.Retina, StructureId.SuperiorColliculus, "retinotectal_orienting"));
        Assert.True(HasDirectConnection(rules, StructureId.InferiorColliculus, StructureId.SuperiorColliculus, "orienting_multisensory_link"));
        Assert.True(HasDirectConnection(rules, StructureId.Snr, StructureId.SuperiorColliculus, "nigrotectal_inhibition"));
        Assert.True(HasDirectConnection(rules, StructureId.DeepCerebellarNuclei, StructureId.SuperiorColliculus, "cerebellotectal_orienting"));
        Assert.True(HasDirectConnection(rules, StructureId.SuperiorColliculus, StructureId.Pulvinar, "tectopulvinar_orienting"));
        Assert.True(HasDirectConnection(rules, StructureId.SuperiorColliculus, StructureId.PremotorCortex, "orienting_to_premotor"));
        Assert.True(HasDirectConnection(rules, StructureId.PremotorCortex, StructureId.Pons, "premotor_corticopontine"));
        Assert.True(HasDirectConnection(rules, StructureId.SuperiorColliculus, StructureId.Retina, "tectoretinal_feedback"));
    }

    [Fact]
    public async Task HippocampalEntorhinal_SpatialMemory_Connectome_Uses_Place_Grid_And_HeadDirection_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.EntorhinalCortex, StructureId.DentateGyrus, "perforant_path_l2"));
        Assert.True(HasDirectConnection(rules, StructureId.EntorhinalCortex, StructureId.CA3, "perforant_path_l2_l3"));
        Assert.True(HasDirectConnection(rules, StructureId.EntorhinalCortex, StructureId.CA1, "temporoammonic_path"));
        Assert.True(HasDirectConnection(rules, StructureId.DentateGyrus, StructureId.CA3, "mossy_fiber_ltp"));
        Assert.True(HasDirectConnection(rules, StructureId.CA3, StructureId.CA1, "schaffer_collateral"));
        Assert.True(HasDirectConnection(rules, StructureId.CA1, StructureId.Subiculum, "hippocampal_output"));
        Assert.True(HasDirectConnection(rules, StructureId.CA1, StructureId.EntorhinalCortex, "hippocampal_index_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.Subiculum, StructureId.Presubiculum, "subicular_head_direction"));
        Assert.True(HasDirectConnection(rules, StructureId.Subiculum, StructureId.Parasubiculum, "subicular_grid_interface"));
        Assert.True(HasDirectConnection(rules, StructureId.Presubiculum, StructureId.EntorhinalCortex, "presubiculum_ec_projection"));
        Assert.True(HasDirectConnection(rules, StructureId.Parasubiculum, StructureId.EntorhinalCortex, "parasubiculum_ec_projection"));
    }

    [Fact]
    public async Task AmygdalaInsulaAcc_Salience_Connectome_Uses_Threat_Interoception_Conflict_And_Arousal_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.Amygdala, StructureId.Acc, "salience_to_conflict"));
        Assert.True(HasDirectConnection(rules, StructureId.Amygdala, StructureId.Insula, "interoceptive_salience"));
        Assert.True(HasDirectConnection(rules, StructureId.Amygdala, StructureId.Hypothalamus, "limbic_homeostatic_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Amygdala, StructureId.NucleusAccumbens, "salience_to_ventral_striatum"));
        Assert.True(HasDirectConnection(rules, StructureId.Amygdala, StructureId.PeriaqueductalGray, "fear_defense_output"));
        Assert.True(HasDirectConnection(rules, StructureId.Insula, StructureId.Acc, "insula_conflict_affect"));
        Assert.True(HasDirectConnection(rules, StructureId.Insula, StructureId.Amygdala, "insula_salience"));
        Assert.True(HasDirectConnection(rules, StructureId.Insula, StructureId.Hypothalamus, "insula_homeostatic"));
        Assert.True(HasDirectConnection(rules, StructureId.Acc, StructureId.Pfc, "error_to_control"));
        Assert.True(HasDirectConnection(rules, StructureId.Acc, StructureId.LocusCoeruleus, "arousal_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Acc, StructureId.BasalForebrain, "attention_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.LocusCoeruleus, StructureId.Amygdala, "lc_salience_arousal_bias"));
    }

    [Fact]
    public async Task PrefrontalWorkingMemory_Connectome_Uses_MD_Frontoparietal_Context_And_BasalGanglia_Gating_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.MediodorsalThalamus, StructureId.Pfc, "md_pfc_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.Ppc, StructureId.Pfc, "frontoparietal_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.TemporalAssociation, StructureId.Pfc, "semantic_to_control"));
        Assert.True(HasDirectConnection(rules, StructureId.OrbitofrontalCortex, StructureId.Pfc, "value_to_control"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.Ppc, "frontoparietal_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.Striatum, "corticostriatal_direct"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.Acc, "conflict_control"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.OrbitofrontalCortex, "orbitofrontal_value_update"));
        Assert.True(HasDirectConnection(rules, StructureId.BasalForebrain, StructureId.Pfc, "cholinergic_prefrontal_attention"));
        Assert.True(HasDirectConnection(rules, StructureId.LocusCoeruleus, StructureId.Pfc, "lc_cortical_gain"));
    }

    [Fact]
    public async Task ThalamicTrn_AttentionGate_Connectome_Uses_Relay_Reticular_And_Corticothalamic_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.Thalamus, StructureId.V1, "thalamocortical_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.Thalamus, StructureId.A1, "thalamocortical_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.Thalamus, StructureId.S1, "thalamocortical_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.Thalamus, StructureId.Pulvinar, "thalamic_attention_hub"));
        Assert.True(HasDirectConnection(rules, StructureId.Trn, StructureId.Thalamus, "reticular_thalamic_gating"));
        Assert.True(HasDirectConnection(rules, StructureId.Trn, StructureId.Pulvinar, "reticular_pulvinar_gating"));
        Assert.True(HasDirectConnection(rules, StructureId.Trn, StructureId.MediodorsalThalamus, "reticular_association_gating"));
        Assert.True(HasDirectConnection(rules, StructureId.Trn, StructureId.IntralaminarThalamus, "reticular_matrix_gating"));
        Assert.True(HasDirectConnection(rules, StructureId.Pulvinar, StructureId.Ppc, "pulvinar_parietal_attention"));
        Assert.True(HasDirectConnection(rules, StructureId.Ppc, StructureId.Pulvinar, "pulvinar_attention_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.Thalamus, "corticothalamic_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.BasalForebrain, StructureId.Thalamus, "cholinergic_thalamic_gate"));
        Assert.True(HasDirectConnection(rules, StructureId.BasalForebrain, StructureId.Trn, "cholinergic_trn_attention"));
    }

    [Fact]
    public async Task HypothalamicHomeostasis_Connectome_Uses_Visceral_Interoceptive_Autonomic_And_Defensive_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.NucleusTractusSolitarius, StructureId.Hypothalamus, "visceral_homeostasis_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.NucleusTractusSolitarius, StructureId.LocusCoeruleus, "autonomic_arousal_gate"));
        Assert.True(HasDirectConnection(rules, StructureId.NucleusTractusSolitarius, StructureId.ReticularFormation, "cardiorespiratory_patterning"));
        Assert.True(HasDirectConnection(rules, StructureId.Insula, StructureId.Hypothalamus, "insula_homeostatic"));
        Assert.True(HasDirectConnection(rules, StructureId.Amygdala, StructureId.Hypothalamus, "limbic_homeostatic_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.LocusCoeruleus, "arousal_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.RapheNuclei, "serotonergic_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.BasalForebrain, "cholinergic_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.Pons, "autonomic_brainstem"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.Medulla, "autonomic_brainstem"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.PeriaqueductalGray, "homeostatic_defense_command"));
        Assert.True(HasDirectConnection(rules, StructureId.PeriaqueductalGray, StructureId.ReticularFormation, "defensive_pattern_release"));
    }

    [Fact]
    public async Task SleepWakeArousal_Connectome_Uses_Hypothalamic_Brainstem_Monoaminergic_And_Intralaminar_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.LocusCoeruleus, "arousal_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.RapheNuclei, "serotonergic_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.BasalForebrain, "cholinergic_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.Pons, "autonomic_brainstem"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.Medulla, "autonomic_brainstem"));
        Assert.True(HasDirectConnection(rules, StructureId.Medulla, StructureId.Pons, "reticular_ascending"));
        Assert.True(HasDirectConnection(rules, StructureId.Medulla, StructureId.LocusCoeruleus, "reticular_ne"));
        Assert.True(HasDirectConnection(rules, StructureId.Medulla, StructureId.RapheNuclei, "reticular_5ht"));
        Assert.True(HasDirectConnection(rules, StructureId.Pons, StructureId.IntralaminarThalamus, "ascending_arousal"));
        Assert.True(HasDirectConnection(rules, StructureId.IntralaminarThalamus, StructureId.Pfc, "intralaminar_prefrontal_arousal"));
        Assert.True(HasDirectConnection(rules, StructureId.LocusCoeruleus, StructureId.Pfc, "lc_cortical_gain"));
        Assert.True(HasDirectConnection(rules, StructureId.RapheNuclei, StructureId.Pfc, "raphe_prefrontal_modulation"));
        Assert.True(HasDirectConnection(rules, StructureId.BasalForebrain, StructureId.Pfc, "cholinergic_prefrontal_attention"));
    }

    [Fact]
    public async Task DescendingDefense_Connectome_Uses_Amygdala_Hypothalamus_Pag_Raphe_Reticular_And_Spinal_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.Amygdala, StructureId.PeriaqueductalGray, "fear_defense_output"));
        Assert.True(HasDirectConnection(rules, StructureId.Amygdala, StructureId.Hypothalamus, "limbic_homeostatic_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Hypothalamus, StructureId.PeriaqueductalGray, "homeostatic_defense_command"));
        Assert.True(HasDirectConnection(rules, StructureId.PeriaqueductalGray, StructureId.ReticularFormation, "defensive_pattern_release"));
        Assert.True(HasDirectConnection(rules, StructureId.PeriaqueductalGray, StructureId.RapheNuclei, "descending_pain_modulation"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.Medulla, "reticulo_bulbar_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.SpinalCordMotor, "reticulospinal_posture"));
        Assert.True(HasDirectConnection(rules, StructureId.SpinalCordMotor, StructureId.ReticularFormation, "spinal_state_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.M1, StructureId.SpinalCordMotor, "corticospinal_output"));
    }

    [Fact]
    public async Task Limbic_Modulation_Amygdala_To_Pfc_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.Amygdala, StructureId.Pfc));
    }

    [Fact]
    public void SpikeMessage_Protocol_Validation_Uses_Neurotransmitter_Quanta_Not_Raw_Weights()
    {
        var spike = new SpikeMessage
        {
            MessageId = Guid.NewGuid(),
            TimestampMs = 1,
            SourceStructure = StructureId.V1,
            TargetStructure = StructureId.Ppc,
            SourceNeuronId = "v1-0",
            TargetNeuronId = "ppc-0",
            SynapseId = Guid.NewGuid(),
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = 1.25f,
            ReuptakeRate = 8f,
            SpikeType = SpikeTypeEnum.ACTION_POTENTIAL,
            IsFeedback = false,
            ModulationContext = new NeuromodState()
        };

        var valid = SpikeProtocol.validate_spike(spike, out var error);
        Assert.True(valid, error);
    }

    private static async Task<Dictionary<StructureId, List<StructureId>>> LoadGraphAsync()
        => await ConnectivityGraphLazy.Value;

    private static async Task<List<ConnectivityRuleJson>> LoadRulesAsync()
        => await ConnectivityRulesLazy.Value;

    private static async Task<List<ConnectivityRuleJson>> LoadRulesCoreAsync()
    {
        var json = await File.ReadAllTextAsync(ConnectivityPath);
        return JsonSerializer.Deserialize<List<ConnectivityRuleJson>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }

    private static async Task<Dictionary<StructureId, List<StructureId>>> LoadGraphCoreAsync()
    {
        var rules = await LoadRulesAsync();

        var graph = new Dictionary<StructureId, HashSet<StructureId>>();
        foreach (var rule in rules)
        {
            if (!Enum.TryParse<StructureId>(rule.Source, ignoreCase: true, out var source))
            {
                continue;
            }

            if (!graph.TryGetValue(source, out var targets))
            {
                targets = new HashSet<StructureId>();
                graph[source] = targets;
            }

            foreach (var connection in rule.Connections ?? [])
            {
                if (!Enum.TryParse<StructureId>(connection.Target, ignoreCase: true, out var target))
                {
                    continue;
                }

                targets.Add(target);
            }
        }

        return graph.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
    }

    private static bool HasDirectConnection(
        IEnumerable<ConnectivityRuleJson> rules,
        StructureId source,
        StructureId target,
        string? projectionType = null)
    {
        foreach (var rule in rules)
        {
            if (!Enum.TryParse<StructureId>(rule.Source, ignoreCase: true, out var parsedSource) || parsedSource != source)
            {
                continue;
            }

            foreach (var connection in rule.Connections ?? [])
            {
                if (!Enum.TryParse<StructureId>(connection.Target, ignoreCase: true, out var parsedTarget) || parsedTarget != target)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(projectionType) ||
                    string.Equals(connection.ProjectionType, projectionType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasPath(Dictionary<StructureId, List<StructureId>> graph, StructureId source, StructureId target)
    {
        var queue = new Queue<StructureId>();
        var visited = new HashSet<StructureId>();
        queue.Enqueue(source);
        visited.Add(source);

        while (queue.TryDequeue(out var current))
        {
            if (current == target)
            {
                return true;
            }

            if (!graph.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var next in neighbors.Where(n => visited.Add(n)))
            {
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static string ResolveConnectivityPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "connectivity", "dnne-connectivity.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve connectivity/dnne-connectivity.json from test base directory.");
    }

    private sealed record ConnectivityRuleJson(string? Source, List<SynapticConnectionJson>? Connections);
    private sealed record SynapticConnectionJson(string? Target, string? SynapseId, string? Neurotransmitter, string? ProjectionType);
}
