using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using JetBrains.Annotations;

namespace CsWebIopi
{
    /// <summary>Base class for handling disposable resources</summary>
    /// <remarks>
    ///     This pattern follows the recommended best practice for handling IDisposable with a few enhancements. Presently it writes to debug when finalizer is called. To use, override
    ///     DisposeManagedResources or DisposeUnmanagedResources and, of course, call Dispose or wrap in using() to dispose properly. Or be evil and let the finalizer call dispose targetting only the
    ///     unmanaged resources.
    /// </remarks>
    public class ThreadSafeDisposableBase : IDisposable
    {
        private const int Allocated = 0, Disposing = 1, Disposed = 2;
        private int m_state;

        [NotNull, PublicAPI]
        [SuppressMessage("ReSharper", "VirtualMemberNeverOverriden.Global")]
        protected virtual string DerivedName => GetType().Name;

        /// <summary>Gets a value indicating whether this object has been disposed or is in the process of being disposed.</summary>
        protected bool IsAllocated => Interlocked.CompareExchange(ref m_state, Allocated, Allocated) == Allocated;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Throws ObjectDisposedException if object is disposed</summary>
        /// <exception cref="ObjectDisposedException">Thrown when object is disposed.</exception>
        [PublicAPI]
        public void ThrowIfDisposed()
        {
            if (!IsAllocated)
                throw new ObjectDisposedException(GetType().Name);
        }

        ~ThreadSafeDisposableBase()
        {
#if DEBUG
            Debug.WriteLine("Failed to proactively dispose of object, so it is being finalized: {0}.{1}Object creation stack trace:{1}{2}", DerivedName, Environment.NewLine, m_instantiationPoint);
#endif
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(location1: ref m_state, value: Disposing, comparand: Allocated) != Allocated)
                return;

            if (disposing)
                DisposeManagedResources();

            DisposeUnmanagedResources();

            GC.SuppressFinalize(this);
            Interlocked.Exchange(ref m_state, Disposed);
        }

        /// <summary>
        ///     Called when <see cref="Dispose"/> is invoked. Thread safe and provides guarantees that the method will never be called more than once or throw <see cref="ObjectDisposedException"/> if already
        ///     disposed. This method will only execute if Dispose is called.
        /// </summary>
        /// <remarks>Implementers need not call <see langword="base"/>.<see cref="DisposeManagedResources"/>.</remarks>
        protected virtual void DisposeManagedResources() { }

        /// <summary>
        ///     Called when <see cref="Dispose"/> is invoked. Thread safe and provides guarantees that the method will never be called more than once or throw <see cref="ObjectDisposedException"/> if already
        ///     disposed. This method will be called bo the object finalizer if <see cref="Dispose"/> is not called.
        /// </summary>
        /// <remarks>Implementers need not call <see langword="base"/>.<see cref="DisposeUnmanagedResources"/>.</remarks>
        protected virtual void DisposeUnmanagedResources() { }

#if DEBUG
        /// <summary>Get the stack trace on object instantiation; useful for troubleshooting failure to dispose</summary>
        private readonly StackTrace m_instantiationPoint;

        public ThreadSafeDisposableBase()
        {
            m_instantiationPoint = new StackTrace(1, true);
        }
#endif
    }

}