using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace UnityMemorySnapshotThing;

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

    public void Dispose()
    {
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _file.Dispose();
        _ptr = null;
        _length = 0;
    }
}