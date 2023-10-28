using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Commons
{
    /// <summary>
    /// A MemoryManager over a raw pointer
    /// </summary>
    /// <remarks>The pointer is assumed to be fully unmanaged, or externally pinned - no attempt will be made to pin this data</remarks>
    public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        internal T* Pointer;
        internal int Length;

        public UnmanagedMemoryManager()
        {

        }

        /// <summary>
        /// Create a new UnmanagedMemoryManager instance at the given pointer and size
        /// </summary>
        /// <remarks>It is assumed that the span provided is already unmanaged or externally pinned</remarks>
        public UnmanagedMemoryManager(Span<T> span)
        {
            fixed (T* ptr = &MemoryMarshal.GetReference(span))
            {
                Pointer = ptr;
                Length = span.Length;
            }
        }
        /// <summary>
        /// Create a new UnmanagedMemoryManager instance at the given pointer and size
        /// </summary>
        public UnmanagedMemoryManager(T* pointer, int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            Pointer = pointer;
            Length = length;
        }
        /// <summary>
        /// Obtains a span that represents the region
        /// </summary>
        public override Span<T> GetSpan() => new Span<T>(Pointer, Length);

        /// <summary>
        /// Provides access to a pointer that represents the data (note: no actual pin occurs)
        /// </summary>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= Length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return new MemoryHandle(Pointer + elementIndex);
        }
        /// <summary>
        /// Has no effect
        /// </summary>
        public override void Unpin() { }

        /// <summary>
        /// Releases all resources associated with this object
        /// </summary>
        protected override void Dispose(bool disposing) { }
    }
}
