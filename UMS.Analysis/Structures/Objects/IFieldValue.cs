namespace UMS.Analysis.Structures.Objects;

public interface IFieldValue
{
    public bool IsNull { get; }
    public bool FailedToParse { get; }
    public ulong FailedParseFromPtr { get; }
}