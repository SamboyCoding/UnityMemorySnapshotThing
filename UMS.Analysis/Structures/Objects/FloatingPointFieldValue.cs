namespace UMS.Analysis.Structures.Objects;

public struct FloatingPointFieldValue : IFieldValue
{
    public bool IsNull => false;
    public bool FailedToParse => false;
    public ulong FailedParseFromPtr => 0;
    
    public double Value { get; }

    public FloatingPointFieldValue(double value)
    {
        Value = value;
    }
}