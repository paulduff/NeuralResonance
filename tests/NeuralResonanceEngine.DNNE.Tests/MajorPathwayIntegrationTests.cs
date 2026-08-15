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
        Assert.True(HasPath(graph, StructureId.Snr, StructureId.IntralaminarThalamus));
        Assert.True(HasPath(graph, StructureId.IntralaminarThalamus, StructureId.Pfc));
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
        Assert.True(HasPath(graph, StructureId.DentateNucleus, StructureId.M1));
    }

    [Fact]
    public async Task Cerebellar_Correction_Connectome_Uses_Mossy_Climbing_Purkinje_And_Dcn_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarGranule, StructureId.PurkinjeCellLayer, "parallel_fiber"));
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarLobules, StructureId.PurkinjeCellLayer, "lobular_parallel_fiber"));
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarVermis, StructureId.PurkinjeCellLayer, "vermis_purkinje_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.InferiorOlive, StructureId.PurkinjeCellLayer, "climbing_fiber_teaching"));
        Assert.True(HasDirectConnection(rules, StructureId.PurkinjeCellLayer, StructureId.DentateNucleus, "purkinje_dentate_inhibition"));
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarVermis, StructureId.FastigialNucleus, "vermal_lobule_fastigial_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.CerebellarLobules, StructureId.DentateNucleus, "lateral_lobule_dentate_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.DentateNucleus, StructureId.MotorThalamus, "dentatothalamic_motor_planning"));
    }

    [Fact]
    public async Task VestibuloReticular_Posture_Connectome_Uses_Balance_Arousal_And_SpinalTone_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.CerebellarVermis, "vestibulo_cerebellar_alignment"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.ReticularFormation, "vestibulo_reticular_posture"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.SpinalCordMotor, "lateral_vestibulospinal_posture"));
        Assert.True(HasDirectConnection(rules, StructureId.FastigialNucleus, StructureId.VestibularNuclei, "fastigiovestibular_balance_correction"));
        Assert.True(HasDirectConnection(rules, StructureId.FastigialNucleus, StructureId.ReticularFormation, "fastigioreticular_posture"));
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
        Assert.True(HasDirectConnection(rules, StructureId.DentateNucleus, StructureId.SuperiorColliculus, "dentatotectal_orienting"));
        Assert.True(HasDirectConnection(rules, StructureId.SuperiorColliculus, StructureId.Pulvinar, "tectopulvinar_orienting"));
        Assert.True(HasDirectConnection(rules, StructureId.SuperiorColliculus, StructureId.PremotorCortex, "orienting_to_premotor"));
        Assert.True(HasDirectConnection(rules, StructureId.PremotorCortex, StructureId.PontineNuclei, "premotor_corticopontine"));
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
        Assert.True(HasDirectConnection(rules, StructureId.BasolateralAmygdala, StructureId.Acc, "basolateral_cingulate_salience"));
        Assert.True(HasDirectConnection(rules, StructureId.BasolateralAmygdala, StructureId.Insula, "basolateral_insular_salience"));
        Assert.True(HasDirectConnection(rules, StructureId.BasolateralAmygdala, StructureId.CentralAmygdala, "basolateral_central_conditioned_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.BasolateralAmygdala, StructureId.NucleusAccumbens, "basolateral_ventral_striatal_salience"));
        Assert.True(HasDirectConnection(rules, StructureId.CentralAmygdala, StructureId.PeriaqueductalGray, "central_amygdala_pag_inhibitory_pattern"));
        Assert.True(HasDirectConnection(rules, StructureId.Insula, StructureId.Acc, "insula_conflict_affect"));
        Assert.True(HasDirectConnection(rules, StructureId.Insula, StructureId.BasolateralAmygdala, "insula_basolateral_salience"));
        Assert.True(HasDirectConnection(rules, StructureId.Insula, StructureId.ParaventricularHypothalamicNucleus, "insula_pvn_interoception"));
        Assert.True(HasDirectConnection(rules, StructureId.Acc, StructureId.Pfc, "error_to_control"));
        Assert.True(HasDirectConnection(rules, StructureId.Acc, StructureId.LocusCoeruleus, "arousal_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Acc, StructureId.NucleusBasalis, "attention_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.LocusCoeruleus, StructureId.BasolateralAmygdala, "lc_basolateral_arousal_bias"));
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
        Assert.True(HasDirectConnection(rules, StructureId.NucleusBasalis, StructureId.Pfc, "cholinergic_prefrontal_attention"));
        Assert.True(HasDirectConnection(rules, StructureId.LocusCoeruleus, StructureId.Pfc, "lc_cortical_gain"));
    }

    [Fact]
    public async Task ThalamicTrn_AttentionGate_Connectome_Uses_Relay_Reticular_And_Corticothalamic_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.LateralGeniculateNucleus, StructureId.V1, "lgn_v1_retinotopic_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.MedialGeniculateNucleus, StructureId.A1, "mgn_a1_tonotopic_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.VentralPosterolateralThalamus, StructureId.S1, "vpl_s1_somatotopic_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.SuperiorColliculus, StructureId.Pulvinar, "tectopulvinar_orienting"));
        Assert.True(HasDirectConnection(rules, StructureId.Trn, StructureId.Pulvinar, "reticular_pulvinar_gating"));
        Assert.True(HasDirectConnection(rules, StructureId.Trn, StructureId.MediodorsalThalamus, "reticular_association_gating"));
        Assert.True(HasDirectConnection(rules, StructureId.Trn, StructureId.IntralaminarThalamus, "reticular_matrix_gating"));
        Assert.True(HasDirectConnection(rules, StructureId.Pulvinar, StructureId.Ppc, "pulvinar_parietal_attention"));
        Assert.True(HasDirectConnection(rules, StructureId.Ppc, StructureId.Pulvinar, "pulvinar_attention_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.MediodorsalThalamus, "prefrontal_mediodorsal_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.LocusCoeruleus, StructureId.IntralaminarThalamus, "lc_intralaminar_gain"));
        Assert.True(HasDirectConnection(rules, StructureId.NucleusBasalis, StructureId.Trn, "cholinergic_trn_attention"));
    }

    [Fact]
    public async Task HypothalamicHomeostasis_Connectome_Uses_Visceral_Interoceptive_Autonomic_And_Defensive_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.NucleusTractusSolitarius, StructureId.ParaventricularHypothalamicNucleus, "nts_pvn_autonomic_input"));
        Assert.True(HasDirectConnection(rules, StructureId.NucleusTractusSolitarius, StructureId.LocusCoeruleus, "autonomic_arousal_gate"));
        Assert.True(HasDirectConnection(rules, StructureId.NucleusTractusSolitarius, StructureId.ReticularFormation, "cardiorespiratory_patterning"));
        Assert.True(HasDirectConnection(rules, StructureId.Insula, StructureId.ParaventricularHypothalamicNucleus, "insula_pvn_interoception"));
        Assert.True(HasDirectConnection(rules, StructureId.CentralAmygdala, StructureId.LateralHypothalamicArea, "central_amygdala_lha_defensive_pattern"));
        Assert.True(HasDirectConnection(rules, StructureId.DorsomedialHypothalamicNucleus, StructureId.LocusCoeruleus, "dmh_locus_coeruleus_arousal"));
        Assert.True(HasDirectConnection(rules, StructureId.DorsomedialHypothalamicNucleus, StructureId.RapheNuclei, "dmh_raphe_state_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.LateralHypothalamicArea, StructureId.NucleusBasalis, "lha_basal_forebrain_wake_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.PedunculopontineNucleus, "reticular_pedunculopontine_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.DorsomedialHypothalamicNucleus, StructureId.ReticularFormation, "dmh_reticular_arousal"));
        Assert.True(HasDirectConnection(rules, StructureId.ParaventricularHypothalamicNucleus, StructureId.PeriaqueductalGray, "pvn_pag_stress_defense"));
        Assert.True(HasDirectConnection(rules, StructureId.PeriaqueductalGray, StructureId.ReticularFormation, "defensive_pattern_release"));
    }

    [Fact]
    public async Task SleepWakeArousal_Connectome_Uses_Hypothalamic_Brainstem_Monoaminergic_And_Intralaminar_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.DorsomedialHypothalamicNucleus, StructureId.LocusCoeruleus, "dmh_locus_coeruleus_arousal"));
        Assert.True(HasDirectConnection(rules, StructureId.DorsomedialHypothalamicNucleus, StructureId.RapheNuclei, "dmh_raphe_state_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.LateralHypothalamicArea, StructureId.NucleusBasalis, "lha_basal_forebrain_wake_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.DorsomedialHypothalamicNucleus, StructureId.ReticularFormation, "dmh_reticular_arousal"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.PedunculopontineNucleus, "reticular_pedunculopontine_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.LocusCoeruleus, "reticular_locus_coeruleus_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.RapheNuclei, "reticular_raphe_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.PedunculopontineNucleus, StructureId.IntralaminarThalamus, "ppn_intralaminar_arousal"));
        Assert.True(HasDirectConnection(rules, StructureId.IntralaminarThalamus, StructureId.Pfc, "intralaminar_prefrontal_arousal"));
        Assert.True(HasDirectConnection(rules, StructureId.LocusCoeruleus, StructureId.Pfc, "lc_cortical_gain"));
        Assert.True(HasDirectConnection(rules, StructureId.RapheNuclei, StructureId.Pfc, "raphe_prefrontal_modulation"));
        Assert.True(HasDirectConnection(rules, StructureId.NucleusBasalis, StructureId.Pfc, "cholinergic_prefrontal_attention"));
    }

    [Fact]
    public async Task DescendingDefense_Connectome_Uses_Amygdala_Hypothalamus_Pag_Raphe_Reticular_And_Spinal_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.BasolateralAmygdala, StructureId.CentralAmygdala, "basolateral_central_conditioned_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.CentralAmygdala, StructureId.LateralHypothalamicArea, "central_amygdala_lha_defensive_pattern"));
        Assert.True(HasDirectConnection(rules, StructureId.ParaventricularHypothalamicNucleus, StructureId.PeriaqueductalGray, "pvn_pag_stress_defense"));
        Assert.True(HasDirectConnection(rules, StructureId.PeriaqueductalGray, StructureId.ReticularFormation, "defensive_pattern_release"));
        Assert.True(HasDirectConnection(rules, StructureId.PeriaqueductalGray, StructureId.RapheNuclei, "descending_pain_modulation"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.HypoglossalNucleus, "reticular_hypoglossal_premotor"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.SpinalCordMotor, "reticulospinal_posture"));
        Assert.True(HasDirectConnection(rules, StructureId.SpinalCordMotor, StructureId.ReticularFormation, "spinal_state_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.M1, StructureId.SpinalCordMotor, "corticospinal_output"));
    }

    [Fact]
    public async Task DopamineRewardLearning_Connectome_Uses_Vta_Snc_Accumbens_Striatum_Habenula_Ofc_And_Pfc_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.Vta, StructureId.NucleusAccumbens, "mesolimbic_reward"));
        Assert.True(HasDirectConnection(rules, StructureId.Vta, StructureId.Pfc, "mesocortical_novelty"));
        Assert.True(HasDirectConnection(rules, StructureId.Snc, StructureId.Striatum, "nigrostriatal_reward_prediction"));
        Assert.True(HasDirectConnection(rules, StructureId.Snc, StructureId.NucleusAccumbens, "nigroaccumbal_reward_prediction"));
        Assert.True(HasDirectConnection(rules, StructureId.Snc, StructureId.OrbitofrontalCortex, "mesocortical_value_modulation"));
        Assert.True(HasDirectConnection(rules, StructureId.OrbitofrontalCortex, StructureId.NucleusAccumbens, "value_to_ventral_striatum"));
        Assert.True(HasDirectConnection(rules, StructureId.OrbitofrontalCortex, StructureId.Striatum, "orbitostriatal_value"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.NucleusAccumbens, "mesocorticolimbic_drive"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.OrbitofrontalCortex, "orbitofrontal_value_update"));
        Assert.True(HasDirectConnection(rules, StructureId.NucleusAccumbens, StructureId.Vta, "accumbens_vta_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.Habenula, StructureId.Vta, "aversive_dopamine_suppression"));
        Assert.True(HasDirectConnection(rules, StructureId.Habenula, StructureId.Snc, "habenulo_nigral_inhibition"));
        Assert.True(HasDirectConnection(rules, StructureId.Striatum, StructureId.Snc, "striosomal_dopamine_feedback"));
    }

    [Fact]
    public async Task SeptohippocampalThetaNavigation_Connectome_Uses_Septal_Entorhinal_Hippocampal_HeadDirection_And_Retrosplenial_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.NucleusBasalis, StructureId.MedialSeptalNucleus, "nucleus_basalis_medial_septal_coordination"));
        Assert.True(HasDirectConnection(rules, StructureId.MedialSeptalNucleus, StructureId.EntorhinalCortex, "medial_septal_entorhinal_theta"));
        Assert.True(HasDirectConnection(rules, StructureId.MedialSeptalNucleus, StructureId.DentateGyrus, "medial_septal_dentate_theta"));
        Assert.True(HasDirectConnection(rules, StructureId.MedialSeptalNucleus, StructureId.CA1, "medial_septal_ca1_theta"));
        Assert.True(HasDirectConnection(rules, StructureId.EntorhinalCortex, StructureId.DentateGyrus, "perforant_path_l2"));
        Assert.True(HasDirectConnection(rules, StructureId.DentateGyrus, StructureId.CA3, "mossy_fiber_ltp"));
        Assert.True(HasDirectConnection(rules, StructureId.CA3, StructureId.CA1, "schaffer_collateral"));
        Assert.True(HasDirectConnection(rules, StructureId.CA1, StructureId.Subiculum, "hippocampal_output"));
        Assert.True(HasDirectConnection(rules, StructureId.CA1, StructureId.Presubiculum, "hippocampal_spatial_index"));
        Assert.True(HasDirectConnection(rules, StructureId.Subiculum, StructureId.Presubiculum, "subicular_head_direction"));
        Assert.True(HasDirectConnection(rules, StructureId.Presubiculum, StructureId.EntorhinalCortex, "presubiculum_ec_projection"));
        Assert.True(HasDirectConnection(rules, StructureId.PosteriorCingulate, StructureId.RetrosplenialCortex, "default_navigation_loop"));
        Assert.True(HasDirectConnection(rules, StructureId.RetrosplenialCortex, StructureId.Ppc, "spatial_reference_transform"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.Ppc, "vestibulo_parietal_spatial"));
    }

    [Fact]
    public async Task SpinalProprioceptiveReflex_Connectome_Uses_Spinal_S1_M1_Cerebellar_Vestibular_Reticular_And_Thalamic_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.SomaticAfferents, StructureId.VentralPosterolateralThalamus, "somatic_vpl_lemniscal_afference"));
        Assert.True(HasDirectConnection(rules, StructureId.SomaticAfferents, StructureId.SpinalCordMotor, "cutaneous_reflex_afference"));
        Assert.True(HasDirectConnection(rules, StructureId.S1, StructureId.SomaticAfferents, "corticofugal_somatic_gain_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.ProprioceptiveAfferents, StructureId.VentralPosterolateralThalamus, "proprioceptive_vpl_afference"));
        Assert.True(HasDirectConnection(rules, StructureId.ProprioceptiveAfferents, StructureId.CerebellarGranule, "primary_spinocerebellar_afference"));
        Assert.True(HasDirectConnection(rules, StructureId.ProprioceptiveAfferents, StructureId.SpinalCordMotor, "proprioceptive_reflex_afference"));
        Assert.True(HasDirectConnection(rules, StructureId.S1, StructureId.ProprioceptiveAfferents, "corticofugal_proprioceptive_gain_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularAfferents, StructureId.VestibularNuclei, "eighth_nerve_vestibular_afference"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.VestibularAfferents, "vestibular_efferent_gain_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.VisceralAfferents, StructureId.NucleusTractusSolitarius, "vagal_visceral_afference"));
        Assert.True(HasDirectConnection(rules, StructureId.NucleusTractusSolitarius, StructureId.VisceralAfferents, "vagal_efferent_afferent_gain_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.M1, StructureId.SpinalCordMotor, "corticospinal_output"));
        Assert.True(HasDirectConnection(rules, StructureId.ReticularFormation, StructureId.SpinalCordMotor, "reticulospinal_posture"));
        Assert.True(HasDirectConnection(rules, StructureId.SpinalCordMotor, StructureId.VentralPosterolateralThalamus, "spinal_vpl_proprioceptive_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.SpinalCordMotor, StructureId.CerebellarGranule, "spinocerebellar_mossy_input"));
        Assert.True(HasDirectConnection(rules, StructureId.SpinalCordMotor, StructureId.ReticularFormation, "spinal_state_feedback"));
        Assert.True(HasDirectConnection(rules, StructureId.S1, StructureId.CerebellarGranule, "somatocerebellar_proprioceptive_mossy"));
        Assert.True(HasDirectConnection(rules, StructureId.VentralPosterolateralThalamus, StructureId.S1, "vpl_s1_somatotopic_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.MotorThalamus, StructureId.M1, "motor_thalamocortical_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.ReticularFormation, "vestibulo_reticular_posture"));
        Assert.True(HasDirectConnection(rules, StructureId.VestibularNuclei, StructureId.SpinalCordMotor, "lateral_vestibulospinal_posture"));
    }

    [Fact]
    public async Task OlfactoryLimbicAutobiographicalMemory_Connectome_Uses_Olfactory_Temporal_Amygdala_Hippocampal_Ofc_And_Pfc_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.OlfactoryBulb, StructureId.TemporalAssociation, "olfactory_cortical"));
        Assert.True(HasDirectConnection(rules, StructureId.OlfactoryBulb, StructureId.CorticalAmygdala, "olfactory_cortical_amygdala"));
        Assert.True(HasDirectConnection(rules, StructureId.CorticalAmygdala, StructureId.BasolateralAmygdala, "cortical_basolateral_olfactory_association"));
        Assert.True(HasDirectConnection(rules, StructureId.OlfactoryBulb, StructureId.EntorhinalCortex, "olfactory_hippocampal"));
        Assert.True(HasDirectConnection(rules, StructureId.TemporalAssociation, StructureId.EntorhinalCortex, "semantic_to_memory"));
        Assert.True(HasDirectConnection(rules, StructureId.TemporalAssociation, StructureId.BasolateralAmygdala, "temporal_basolateral_salience"));
        Assert.True(HasDirectConnection(rules, StructureId.TemporalAssociation, StructureId.PerirhinalCortex, "semantic_to_familiarity"));
        Assert.True(HasDirectConnection(rules, StructureId.PerirhinalCortex, StructureId.EntorhinalCortex, "perirhinal_entorhinal_object_memory"));
        Assert.True(HasDirectConnection(rules, StructureId.ParahippocampalCortex, StructureId.EntorhinalCortex, "context_to_hippocampal_index"));
        Assert.True(HasDirectConnection(rules, StructureId.EntorhinalCortex, StructureId.DentateGyrus, "perforant_path_l2"));
        Assert.True(HasDirectConnection(rules, StructureId.CA3, StructureId.CA1, "schaffer_collateral"));
        Assert.True(HasDirectConnection(rules, StructureId.CA1, StructureId.Pfc, "memory_to_control"));
        Assert.True(HasDirectConnection(rules, StructureId.OrbitofrontalCortex, StructureId.Pfc, "value_to_control"));
    }

    [Fact]
    public async Task AuditoryLanguageMotorIntegration_Connectome_Uses_A1_Wernicke_Arcuate_Broca_Motor_BasalGanglia_And_Thalamic_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.MedialGeniculateNucleus, StructureId.A1, "mgn_a1_tonotopic_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.A1, StructureId.WernickePstgPsts, "auditory_language_comprehension_feedforward"));
        Assert.True(HasDirectConnection(rules, StructureId.WernickePstgPsts, StructureId.ArcuateFasciculus, "phonological_dorsal_stream"));
        Assert.True(HasDirectConnection(rules, StructureId.ArcuateFasciculus, StructureId.BrocaBa44Ba45, "dorsal_language_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.Pfc, StructureId.BrocaBa44Ba45, "prefrontal_broca_speech_control"));
        Assert.True(HasDirectConnection(rules, StructureId.BrocaBa44Ba45, StructureId.Sma, "speech_sequence_to_sma"));
        Assert.True(HasDirectConnection(rules, StructureId.BrocaBa44Ba45, StructureId.M1, "speech_motor_output"));
        Assert.True(HasDirectConnection(rules, StructureId.PremotorCortex, StructureId.M1, "premotor_motor_output"));
        Assert.True(HasDirectConnection(rules, StructureId.PremotorCortex, StructureId.Striatum, "premotor_action_selection"));
        Assert.True(HasDirectConnection(rules, StructureId.GPi, StructureId.MotorThalamus, "pallidothalamic_motor_inhibition"));
        Assert.True(HasDirectConnection(rules, StructureId.MotorThalamus, StructureId.PremotorCortex, "motor_thalamocortical_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.MotorThalamus, StructureId.M1, "motor_thalamocortical_relay"));
    }

    [Fact]
    public async Task VisualTemporalObjectRecognition_Connectome_Uses_V1_V2_V4_Mt_Temporal_Perirhinal_Pfc_And_Pulvinar_Routes()
    {
        var rules = await LoadRulesAsync();
        Assert.True(HasDirectConnection(rules, StructureId.LateralGeniculateNucleus, StructureId.V1, "lgn_v1_retinotopic_relay"));
        Assert.True(HasDirectConnection(rules, StructureId.V1, StructureId.V2, "visual_secondary_feedforward"));
        Assert.True(HasDirectConnection(rules, StructureId.V2, StructureId.V4, "visual_ventral_progression"));
        Assert.True(HasDirectConnection(rules, StructureId.V2, StructureId.Mt, "visual_dorsal_motion"));
        Assert.True(HasDirectConnection(rules, StructureId.V4, StructureId.TemporalAssociation, "object_feature_integration"));
        Assert.True(HasDirectConnection(rules, StructureId.V4, StructureId.PerirhinalCortex, "object_familiarity_stream"));
        Assert.True(HasDirectConnection(rules, StructureId.TemporalAssociation, StructureId.Pfc, "semantic_to_control"));
        Assert.True(HasDirectConnection(rules, StructureId.TemporalAssociation, StructureId.PerirhinalCortex, "semantic_to_familiarity"));
        Assert.True(HasDirectConnection(rules, StructureId.Pulvinar, StructureId.TemporalAssociation, "pulvinar_temporal_integration"));
        Assert.True(HasDirectConnection(rules, StructureId.Pulvinar, StructureId.V1, "pulvinar_visual_feedback"));
    }

    [Fact]
    public async Task Limbic_Modulation_Amygdala_To_Pfc_Exists()
    {
        var graph = await LoadGraphAsync();
        Assert.True(HasPath(graph, StructureId.BasolateralAmygdala, StructureId.Pfc));
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
