# Entry 079 - Neuronal Attention And Workspace Deletion

Date: 2026-08-06

## Purpose

This rung removes the controller-owned attention and global-workspace authority
surfaces. Attention selection, bounded maintenance, distractor suppression, and
broadcast are now observable only through distributed neuronal population
diagnostics.

## Authoritative Path

NeuronalAttentionWorkspaceRuntime decodes measured activity from sensory,
pulvinar, thalamic relay, TRN, mediodorsal, recurrent PFC, and intralaminar
populations. Its endpoint remains:

GET /api/v1/neuronal-attention-workspace

The decoder is read-only. The only live gain derived from attention is the
numeric sensory-noise weighting produced from the current neuronal lane scores.
Missing neuronal evidence produces a neutral sensory vector; it never restores
a named scalar winner.

## Removed Paths

The following legacy routes are deleted:

- GET /api/v1/prefrontal-working-memory;
- GET /api/v1/consciousness-rhythm;
- GET /api/v1/global-workspace.

The following controller methods are deleted:

- SimulationState.UpdateAttentionState;
- SimulationState.GetAttentionRuntime;
- SimulationState.GetAttentionSnapshot;
- SimulationState.GetPrefrontalWorkingMemorySnapshot;
- SimulationState.GetConsciousnessRhythmSnapshot;
- SimulationState.GetGlobalWorkspaceSnapshot;
- the scalar attention computation helper;
- the scalar prefrontal-working-memory updater;
- the scalar consciousness-rhythm updater;
- the scalar global-workspace competition and broadcast updater.

UpdateNeuromod no longer accepts or stores an attention vector. Conventional
checkpoint documents no longer export or import global attention bias,
attention state, prefrontal working memory, consciousness rhythm, or global
workspace state. Main diagnostics no longer publish those records.

Old checkpoint JSON may still contain these properties. The current serializer
ignores them and cannot restore them.

## Causal Tests

Tests pin the absence of every deleted route, writer, snapshot method, updater,
diagnostic property, and checkpoint property. Neuronal attention tests continue
to prove lane preservation, competition, TRN suppression, pulvinar and PFC
dependence, broadcast loss after intralaminar ablation, and neutral behavior
when neuronal evidence is absent.

## Remaining Deletion Work

Some later symbolic compatibility records still contain read-only references
to their old default attention and working-memory objects. They no longer have
an update root, public API, diagnostic surface, checkpoint state, or route into
the live neuronal loop. The next compile-driven deletion rung removes those
dependent goal, intentional-action, narration, and self-model records rather
than substituting another scalar controller.
