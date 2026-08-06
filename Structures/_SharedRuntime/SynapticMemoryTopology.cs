using NeuralResonanceEngine.Protocol;

internal static class SynapticMemoryTopology
{
	public const int EnsembleCount = PerceptEnsembleTopology.EnsembleCount;

	public static bool IsMemoryCircuitStructure(StructureId structure)
		=> structure is StructureId.InferotemporalCortex
			or StructureId.PerirhinalCortex
			or StructureId.ParahippocampalCortex
			or StructureId.EntorhinalCortex
			or StructureId.DentateGyrus
			or StructureId.CA3
			or StructureId.CA2
			or StructureId.CA1
			or StructureId.Subiculum
			or StructureId.Presubiculum
			or StructureId.Parasubiculum
			or StructureId.RetrosplenialCortex
			or StructureId.Ppc
			or StructureId.TemporalAssociation
			or StructureId.TemporalPole
			or StructureId.Insula
			or StructureId.Pfc
			or StructureId.Striatum
			or StructureId.PremotorCortex
			or StructureId.Sma;

	public static bool IsHippocampal(StructureId structure)
		=> structure is StructureId.EntorhinalCortex
			or StructureId.DentateGyrus
			or StructureId.CA3
			or StructureId.CA2
			or StructureId.CA1
			or StructureId.Subiculum
			or StructureId.Presubiculum
			or StructureId.Parasubiculum;

	public static bool IsCorticalConsolidation(StructureId structure)
		=> structure is StructureId.InferotemporalCortex
			or StructureId.PerirhinalCortex
			or StructureId.ParahippocampalCortex
			or StructureId.RetrosplenialCortex
			or StructureId.Ppc
			or StructureId.TemporalAssociation
			or StructureId.TemporalPole
			or StructureId.Insula
			or StructureId.Pfc
			or StructureId.PremotorCortex
			or StructureId.Sma;

	public static string RoleFor(StructureId structure) => structure switch
	{
		StructureId.InferotemporalCortex or StructureId.PerirhinalCortex => "object",
		StructureId.ParahippocampalCortex or StructureId.RetrosplenialCortex or StructureId.Ppc => "spatial",
		StructureId.EntorhinalCortex or StructureId.DentateGyrus or StructureId.CA3 or
			StructureId.CA2 or StructureId.CA1 or StructureId.Subiculum or
			StructureId.Presubiculum or StructureId.Parasubiculum => "episodic",
		StructureId.TemporalAssociation => "semantic",
		StructureId.TemporalPole or StructureId.Insula or StructureId.Pfc => "autobiographical",
		StructureId.Striatum or StructureId.PremotorCortex or StructureId.Sma => "action",
		_ => "associative"
	};
}
