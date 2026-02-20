using System;

namespace PdfPixel.Wpf.DirectXExperiments.Interop
{
    /// <summary>
    /// Lightweight disposable wrapper around a native COM interface pointer.
    /// Calls <c>IUnknown::Release</c> on disposal.
    /// </summary>
    internal sealed class DirectXHandle : IDisposable
    {
        private IntPtr _ptr;

        /// <summary>
        /// Initializes a new <see cref="DirectXHandle"/> that takes ownership of <paramref name="ptr"/>.
        /// </summary>
        internal DirectXHandle(IntPtr ptr)
        {
            _ptr = ptr;
        }

        /// <summary>The raw COM interface pointer.</summary>
        internal IntPtr Ptr => _ptr;

        /// <inheritdoc />
        public void Dispose()
        {
            if (_ptr != IntPtr.Zero)
            {
                DirectXInterop.ComRelease(_ptr);
                _ptr = IntPtr.Zero;
            }
        }
    }
}
