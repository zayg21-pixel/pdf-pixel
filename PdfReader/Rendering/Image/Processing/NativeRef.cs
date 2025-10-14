using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    public unsafe struct NativeRef<T> where T : unmanaged
    {
        private readonly IntPtr _ptr;
        public NativeRef(IntPtr ptr) => _ptr = ptr;

        public ref T Value
        {
            get
            {
                unsafe { return ref Unsafe.AsRef<T>((void*)_ptr); }
            }
        }
    }
}
