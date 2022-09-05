namespace UnityMemorySnapshotThing;

public static class SpanExtensions
{
    public static string ToCommaSeparatedHexString(this Span<long> span)
        => string.Join(", ", span.ToArray().Select(l => l.ToString("X8")));
}