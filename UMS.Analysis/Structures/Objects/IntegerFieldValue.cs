namespace UMS.Analysis.Structures.Objects;

public struct IntegerFieldValue : IFieldValue
{
    public bool IsNull => false;
    public bool FailedToParse => false;
    public ulong FailedParseFromPtr => 0;
    
    public long Value { get; }

    public IntegerFieldValue(long value)
    {
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}