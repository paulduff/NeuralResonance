param([string]$Root)

$defs = @(
@{Folder='V1'; Enum='V1'; Model='Izhikevich'; Plasticity='BCM'; Just='Orientation selectivity in V1 emerges from nonlinear pyramidal responses with interneuron-mediated inhibition; BCM captures activity-dependent receptive-field stabilization.'; FeedbackMin=5; FeedbackMax=20},
@{Folder='A1'; Enum='A1'; Model='Izhikevich'; Plasticity='STDP'; Just='A1 onset/sustained channels require adaptable spike-frequency response; Izhikevich neurons represent temporal precision and adaptation.'; FeedbackMin=5; FeedbackMax=20},
@{Folder='S1'; Enum='S1'; Model='LIF'; Plasticity='STDP'; Just='S1 tactile relay benefits from efficient LIF conductance integration for RA/SA streams with somatotopic inhibition.'; FeedbackMin=5; FeedbackMax=20},
@{Folder='OlfactoryBulb'; Enum='OlfactoryBulb'; Model='Izhikevich'; Plasticity='STDP'; Just='Bulbar gamma and lateral inhibition are reproduced with resonant Izhikevich-like excitatory/inhibitory dynamics.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='Thalamus'; Enum='Thalamus'; Model='Izhikevich'; Plasticity='STDP'; Just='Thalamic tonic-burst transitions are naturally represented with Izhikevich parameters and relay/matrix subpopulations.'; FeedbackMin=8; FeedbackMax=12},
@{Folder='TRN'; Enum='Trn'; Model='LIF'; Plasticity='STDP'; Just='TRN is predominantly inhibitory and fast-gating; LIF captures efficient GABAergic gating and spotlight competition.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='EntorhinalCortex'; Enum='EntorhinalCortex'; Model='Izhikevich'; Plasticity='STDP'; Just='Grid-like phase responses and mixed stellate/pyramidal dynamics are approximated with Izhikevich conductances.'; FeedbackMin=3; FeedbackMax=8},
@{Folder='Hippocampus.DG'; Enum='DentateGyrus'; Model='LIF'; Plasticity='MossyFiberLTP'; Just='DG sparse coding and pattern separation are well captured by high-threshold LIF granule populations with strong inhibition.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='Hippocampus.CA3'; Enum='CA3'; Model='Izhikevich'; Plasticity='MossyFiberLTP'; Just='CA3 autoassociation and recurrent bursting require nonlinear spiking with recurrent collateral support.'; FeedbackMin=2; FeedbackMax=5},
@{Folder='Hippocampus.CA1'; Enum='CA1'; Model='Izhikevich'; Plasticity='SynapticTaggingCapture'; Just='CA1 comparator behavior uses temporally sensitive pyramidal firing and longer-timescale consolidation mechanisms.'; FeedbackMin=3; FeedbackMax=8},
@{Folder='Subiculum'; Enum='Subiculum'; Model='LIF'; Plasticity='STDP'; Just='Subiculum relay and place-related burst output can be abstracted as thresholded relay populations with adaptive firing.'; FeedbackMin=3; FeedbackMax=8},
@{Folder='PFC'; Enum='Pfc'; Model='Izhikevich'; Plasticity='DopamineModulatedSTDP+SynapticTaggingCapture'; Just='Persistent activity, D1/D2 gating, and flexible control are captured with recurrent Izhikevich cells plus dopamine-modulated learning.'; FeedbackMin=5; FeedbackMax=20},
@{Folder='PPC'; Enum='Ppc'; Model='LIF'; Plasticity='STDP'; Just='PPC multimodal integration and attention maps are efficiently modeled with LIF fields and competitive inhibition.'; FeedbackMin=5; FeedbackMax=20},
@{Folder='TemporalAssociation'; Enum='TemporalAssociation'; Model='Izhikevich'; Plasticity='STDP'; Just='Semantic/object representations require mixed regular/chattering cortical firing phenotypes.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='Striatum'; Enum='Striatum'; Model='LIF'; Plasticity='DopamineModulatedSTDP'; Just='MSN up/down state dynamics and action gating are represented with slowly integrating inhibitory LIF populations under dopamine control.'; FeedbackMin=8; FeedbackMax=12},
@{Folder='GlobusPallidus'; Enum='GlobusPallidus'; Model='LIF'; Plasticity='STDP'; Just='Tonic inhibitory firing and disinhibitory pauses in pallidal nuclei are well matched by LIF tonic cells.'; FeedbackMin=8; FeedbackMax=12},
@{Folder='STN'; Enum='Stn'; Model='Izhikevich'; Plasticity='STDP'; Just='STN rebound bursts and hyperdirect stopping signals need burst-capable nonlinear dynamics.'; FeedbackMin=8; FeedbackMax=12},
@{Folder='SNr'; Enum='Snr'; Model='LIF'; Plasticity='STDP'; Just='SNr output neurons are tonically active inhibitory gates suited to LIF tonic firing.'; FeedbackMin=8; FeedbackMax=12},
@{Folder='SNc'; Enum='Snc'; Model='Izhikevich'; Plasticity='DopamineHomeostasis'; Just='SNc pacemaking and phasic reward prediction error bursts require dopaminergic-capable nonlinear spiking.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='Amygdala'; Enum='Amygdala'; Model='Izhikevich'; Plasticity='STDP'; Just='Fear conditioning and salience pathways require excitatory-inhibitory motif with rapid associative plasticity.'; FeedbackMin=5; FeedbackMax=10},
@{Folder='ACC'; Enum='Acc'; Model='Izhikevich'; Plasticity='STDP'; Just='Conflict monitoring relies on rapid error-related burst coding and recurrent control signals.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='Cerebellum.GranuleCellLayer'; Enum='CerebellarGranule'; Model='LIF'; Plasticity='MossyFiberLTP'; Just='Granule expansion coding is sparse and high-dimensional, matching efficient LIF granule abstractions.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='Cerebellum.PurkinjeCellLayer'; Enum='PurkinjeCellLayer'; Model='HH'; Plasticity='CerebellarLTD'; Just='Purkinje dendritic integration and complex-spike learning require HH-like membrane dynamics with climbing-fiber coupling.'; FeedbackMin=10; FeedbackMax=15},
@{Folder='Cerebellum.DCN'; Enum='DeepCerebellarNuclei'; Model='Izhikevich'; Plasticity='STDP'; Just='DCN rebound output after Purkinje inhibition is naturally represented by rebound-capable Izhikevich neurons.'; FeedbackMin=10; FeedbackMax=15},
@{Folder='InferiorOlive'; Enum='InferiorOlive'; Model='HH'; Plasticity='STDP'; Just='Inferior olive subthreshold oscillation and synchronized complex spikes require conductance-based dynamics.'; FeedbackMin=10; FeedbackMax=15},
@{Folder='LocusCoeruleus'; Enum='LocusCoeruleus'; Model='LIF'; Plasticity='HomeostaticGain'; Just='LC tonic/phasic gain modulation can be modeled with LIF populations projecting neuromodulatory state changes.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='RapheNuclei'; Enum='RapheNuclei'; Model='LIF'; Plasticity='HomeostaticGain'; Just='Raphe slow serotonergic rhythm maps to low-frequency LIF pacemaker populations.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='BasalForebrainCholinergic'; Enum='BasalForebrain'; Model='LIF'; Plasticity='HomeostaticGain'; Just='Cholinergic transients and attention gating are modeled as burst-capable modulatory LIF outputs.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='VTA'; Enum='Vta'; Model='Izhikevich'; Plasticity='DopamineHomeostasis'; Just='VTA novelty/reward responses require dopaminergic burst coding and tonic baseline regulation.'; FeedbackMin=0; FeedbackMax=0},
@{Folder='M1'; Enum='M1'; Model='Izhikevich'; Plasticity='STDP'; Just='Corticospinal output and beta-rhythm motor coding require recurrent excitatory dynamics and inhibitory balance.'; FeedbackMin=10; FeedbackMax=15},
@{Folder='SMA'; Enum='Sma'; Model='LIF'; Plasticity='STDP'; Just='SMA sequence preparation can be represented with chained LIF assemblies for premotor timing.'; FeedbackMin=8; FeedbackMax=12}
)

$template = Get-Content -Raw (Join-Path $PSScriptRoot 'structure-template.txt')
foreach ($d in $defs) {
  $dir = Join-Path $Root $d.Folder
  New-Item -ItemType Directory -Force -Path $dir | Out-Null

  $csproj = @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <AssemblyName>NeuralResonanceEngine.Structures.$($d.Folder.Replace('.','_'))</AssemblyName>
    <RootNamespace>NeuralResonanceEngine.Structures.$($d.Folder.Replace('.','_'))</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Protocol\NeuralResonanceEngine.Protocol.csproj" />
    <ProjectReference Include="..\..\Shared.Contracts\NeuralResonanceEngine.Shared.Contracts.csproj" />
  </ItemGroup>
</Project>
"@
  Set-Content -Path (Join-Path $dir "NeuralResonanceEngine.Structures.$($d.Folder.Replace('.','_')).csproj") -Value $csproj -Encoding UTF8

  $spec = @"
# $($d.Folder) Service Specification

Biological justification: $($d.Just)

- Interface: HTTP/2 minimal API with protobuf SpikeMessage payloads to approximate low-latency spike transfer while preserving explicit conduction delays in message metadata.
- Inbound queue split: feed-forward queue for default hierarchy and feedback queue for `is_feedback=true` messages.
- Neuron model: $($d.Model)
- Plasticity: $($d.Plasticity)
- Feedback delay window: $($d.FeedbackMin)-$($d.FeedbackMax) ms (when this structure participates in feedback pathways).
"@
  Set-Content -Path (Join-Path $dir 'SERVICE_SPEC.md') -Value $spec -Encoding UTF8

  $program = $template.Replace('__STRUCTURE_ENUM__',$d.Enum).Replace('__MODEL__',$d.Model).Replace('__PLASTICITY__',$d.Plasticity).Replace('__JUST__',$d.Just.Replace('"','\"')).Replace('__DELAY_MIN__',$d.FeedbackMin).Replace('__DELAY_MAX__',$d.FeedbackMax)
  Set-Content -Path (Join-Path $dir 'Program.cs') -Value $program -Encoding UTF8
}
