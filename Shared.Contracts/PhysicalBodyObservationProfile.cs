namespace NeuralResonanceEngine.Shared.Contracts;

// These declarations describe the body encoding, not physiological validity.
// A numeric placeholder in an unavailable member is not a measured zero.
// A reference member is an explicitly imposed value, not a dynamic body plant.
public sealed class PhysicalBodyObservationProfile
{
    public string Encoding { get; }
    public IReadOnlyList<string> UnavailableMembers { get; }
    public IReadOnlyList<string> ReferenceMembers { get; }

    public PhysicalBodyObservationProfile(string encoding, IReadOnlyList<string> unavailableMembers,
        IReadOnlyList<string> referenceMembers)
    {
        if (string.IsNullOrWhiteSpace(encoding) || encoding.Length > 128 || encoding != encoding.Trim() || encoding.Any(char.IsControl))
            throw new ArgumentException("Declare the body encoding.");
        Encoding = encoding;
        UnavailableMembers = Freeze(unavailableMembers);
        ReferenceMembers = Freeze(referenceMembers);
        if (UnavailableMembers.Intersect(ReferenceMembers, StringComparer.Ordinal).Any())
            throw new ArgumentException("A body member cannot be both unavailable and a declared reference.");
    }

    // Paths are case-sensitive, relative to the body JSON object. This limited
    // member-path syntax deliberately excludes array indices and JSON escapes.
    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var copy = members.ToArray();
        if (copy.Length > 128 || copy.Any(p => p is null || p.Length is < 2 or > 256 ||
            p[0] != '/' || p.Split('/').Skip(1).Any(part => part.Length == 0 ||
                !part.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))) ||
            copy.Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Body member paths must be distinct, explicit object-member paths.");
        Array.Sort(copy, StringComparer.Ordinal);
        return Array.AsReadOnly(copy);
    }
}
