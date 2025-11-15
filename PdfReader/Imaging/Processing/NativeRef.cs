using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Processing
{
    /// <summary>
    /// Provides a reference-like wrapper around a native pointer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public unsafe struct NativeRef<T> where T : unmanaged
    {
        private readonly IntPtr _ptr;

        public NativeRef(IntPtr ptr) => _ptr = ptr;

        public ref T Value => ref Unsafe.AsRef<T>(_ptr.ToPointer());
    }
}
