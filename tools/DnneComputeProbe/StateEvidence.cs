using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Protocol;

// Offline evidence only, outside measured intervals. Include private neuronal
// numerical state so equal population averages cannot conceal different cells.
internal static class StateEvidence
{
    internal static object? Field(object value, string name) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);

    internal static object? Capture(object? value, int depth = 0)
    {
        if (value is null) return null;
        if (depth > 24) throw new InvalidOperationException("State evidence nesting exceeded; do not omit unknown state.");
        var type = value.GetType();
        // Store actual IEEE bits, including signed zero; no decimal rounding.
        if (value is double d) return new { type = "f64", bits = BitConverter.DoubleToUInt64Bits(d).ToString("X16") };
        if (value is float f) return new { type = "f32", bits = BitConverter.SingleToUInt32Bits(f).ToString("X8") };
        // A queue comparer is executable identity plus any captured state, not
        // a process-specific native entry-point address. Preserve invocation
        // order and closure fields; never silently discard an unknown pointer.
        if (value is Delegate callback)
            return new { type = "delegate", invocations = callback.GetInvocationList().Select(entry => new {
                declaringType = entry.Method.DeclaringType?.AssemblyQualifiedName,
                module = entry.Method.Module.ModuleVersionId, token = entry.Method.MetadataToken,
                target = Capture(entry.Target, depth + 1) }).ToArray() };
        if (value is IntPtr or UIntPtr)
            throw new NotSupportedException("Unmodelled native pointer in diagnostic state; declare its semantic identity explicitly.");
        if (value is string or bool or Guid || type.IsEnum || type.IsPrimitive) return value;
        if (value is SpikeMessage spike) return NormalizeGeneratedEventIds(JsonSerializer.SerializeToNode(spike));
        if (value is IDictionary dictionary)
            return dictionary.Keys.Cast<object>().OrderBy(k => Convert.ToString(k, CultureInfo.InvariantCulture), StringComparer.Ordinal)
                .Select(k => new { key = Capture(k, depth + 1), value = Capture(dictionary[k], depth + 1) }).ToArray();
        if (value is IEnumerable sequence) return sequence.Cast<object?>().Select(x => Capture(x, depth + 1)).ToArray();
        return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(fld => fld.Name, StringComparer.Ordinal)
            .ToDictionary(fld => fld.Name, fld => Capture(fld.GetValue(value), depth + 1), StringComparer.Ordinal);
    }

    internal static object Engine(StructureEngine engine)
    {
        string[] fields = ["_neurons", "_inboundSynapses", "_outboundSynapses", "_cellPostTrace",
            "_cellPostTimestamp", "_lastBiologicalTimestampMs", "_lastProcessedTick"];
        var result = fields.ToDictionary(name => name, name => Capture(Field(engine, name)));
        foreach (string name in new[] { "_feedForward", "_feedback" })
        {
            var queue = Field(engine, name)!;
            result[name] = Capture(queue.GetType().GetProperty("UnorderedItems")!.GetValue(queue));
        }
        return result;
    }

    // Existing generated message IDs are random per run; they are retained in
    // raw output. Only those IDs are excluded from cross-run semantic comparison.
    // Source/target cells, projections, contacts, times and ordering stay present.
    internal static JsonNode? NormalizeGeneratedEventIds(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            obj.Remove("MessageId");
            foreach (var item in obj.ToArray()) NormalizeGeneratedEventIds(item.Value);
        }
        else if (node is JsonArray array) foreach (var item in array) NormalizeGeneratedEventIds(item);
        return node;
    }

    internal static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
