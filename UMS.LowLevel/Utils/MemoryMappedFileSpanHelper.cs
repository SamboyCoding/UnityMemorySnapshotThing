using System.Diagnostics.Contracts;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace UMS.LowLevel.Utils;

/// <summary>
/// Class to wrap span-based file access to a memory mapped file. 
/// </summary>
/// <typeparam name="T">The type of data you want to read from the file</typeparam>
public unsafe class MemoryMappedFileSpanHelper<T> : IDisposable  where T : struct
{ 
    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte* _ptr;
    private long _length;

    public MemoryMappedFileSpanHelper(string filePath)
    {
        _length = new FileInfo(filePath).Length;

        _file = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        _accessor = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
    }

    public Span<T> Span => MemoryMarshal.Cast<byte, T>(new(_ptr, (int)_length));
    
    public Span<T> this[Range range] => InternalGetSpan(range);
    
    [Pure]
    public TDest As<TDest>(Range range) where TDest : struct => MemoryMarshal.Read<TDest>(MemoryMarshal.Cast<T, byte>(InternalGetSpan(range)));  
    
    [Pure]
    public TDest As<TDest>(long start) where TDest : struct => MemoryMarshal.Read<TDest>(MemoryMarshal.Cast<T, byte>(InternalGetSpan(start)));
    [Pure]
    public TDest As<TDest>(long start, int length) where TDest : struct => MemoryMarshal.Read<TDest>(MemoryMarshal.Cast<T, byte>(InternalGetSpan(start, length)));
    [Pure]
    public TDest As<TDest>(int start) where TDest : struct => MemoryMarshal.Read<TDest>(MemoryMarshal.Cast<T, byte>(InternalGetSpan(start..)));
    
    [Pure]
    public Span<TDest> AsSpan<TDest>(Range range) where TDest : struct => MemoryMarshal.Cast<T, TDest>(InternalGetSpan(range));
    
    [Pure]
    public Span<TDest> AsSpan<TDest>(long start, int length) where TDest : struct => MemoryMarshal.Cast<T, TDest>(InternalGetSpan(start, length));

    private Span<T> InternalGetSpan(Range range)
    {
        var (start, length) = GetOffsetAndLength(range);
        
        return InternalGetSpan(start, length);
    }
    
    private Span<T> InternalGetSpan(long start, int length)
    {
        if(start < 0 || length < 0 || start + length > _length)
            throw new ArgumentOutOfRangeException(nameof(start), $"Start {start} and length {length} out of bounds, file length is {_length}");
        
        return new(_ptr + start, length);
    }

    private Span<T> InternalGetSpan(long start)
    {
        var remainder = _length - start;

        return InternalGetSpan(start, (int)Math.Min(remainder, int.MaxValue));
    }
    
    private (long start, int length) GetOffsetAndLength(Range range)
    {
        long start;
        var startIndex = range.Start;
        if (startIndex.IsFromEnd)
            start = _length - startIndex.Value;
        else
            start = startIndex.Value;

        long end;
        var endIndex = range.End;
        if (endIndex.IsFromEnd)
            end = _length - endIndex.Value;
        else
            end = endIndex.Value;
        
        if((end - start) > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(range), $"Range {range} is too large - got start {start} and end {end}, file length is {_length}");

        return (start, (int) (end - start));
    }

    public void Dispose()
    {
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _file.Dispose();
        _ptr = null;
        _length = 0;
    }
}