using System.Diagnostics.Contracts;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace UnityMemorySnapshotLib.Utils;

/// <summary>
/// Class to wrap span-based file access to a memory mapped file. 
/// </summary>
/// <typeparam name="T">The type of data you want to read from the file</typeparam>
public unsafe class MemoryMappedFileSpanHelper<T> : IDisposable  where T : struct
{ 
    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte* _ptr;
    private int _length;

    public MemoryMappedFileSpanHelper(string filePath)
    {
        _length = (int)new FileInfo(filePath).Length;

        _file = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        _accessor = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
    }

    public Span<T> Span => MemoryMarshal.Cast<byte, T>(new(_ptr, _length));
    
    public Span<T> this[Range range] => Span[range];
    
    [Pure]
    public TDest As<TDest>(Range range) where TDest : struct => MemoryMarshal.Read<TDest>(MemoryMarshal.Cast<T, byte>(Span[range]));  
    
    [Pure]
    public TDest As<TDest>(ulong start) where TDest : struct => MemoryMarshal.Read<TDest>(MemoryMarshal.Cast<T, byte>(Span[(int) start..]));
    [Pure]
    public TDest As<TDest>(long start) where TDest : struct => MemoryMarshal.Read<TDest>(MemoryMarshal.Cast<T, byte>(Span[(int) start..]));
    [Pure]
    public TDest As<TDest>(int start) where TDest : struct => MemoryMarshal.Read<TDest>(MemoryMarshal.Cast<T, byte>(Span[start..]));
    
    [Pure]
    public Span<TDest> AsSpan<TDest>(Range range) where TDest : struct => MemoryMarshal.Cast<T, TDest>(Span[range]);

    public void Dispose()
    {
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _file.Dispose();
        _ptr = null;
        _length = 0;
    }
}