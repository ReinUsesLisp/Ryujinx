using OpenTK.Graphics.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    abstract class OGLStreamBuffer : IDisposable
    {
        public int Handle { get; protected set; }

        public long Size { get; protected set; }

        protected BufferTarget Target { get; private set; }

        private bool Mapped = false;

        public OGLStreamBuffer(BufferTarget Target, long MaxSize)
        {
            this.Target = Target;
            this.Size   = MaxSize;
        }

        public static OGLStreamBuffer Create(BufferTarget Target, long MaxSize)
        {
            return new SubDataBuffer(Target, MaxSize);
        }

        public abstract void SetData(long Size, IntPtr HostAddress);

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool Disposing)
        {
            if (Disposing && Handle != 0)
            {
                GL.DeleteBuffer(Handle);

                Handle = 0;
            }
        }

        class SubDataBuffer : OGLStreamBuffer
        {
            public SubDataBuffer(BufferTarget Target, long MaxSize)
                : base(Target, MaxSize)
            {
                Handle = GL.GenBuffer();

                GL.BindBuffer(Target, Handle);

                GL.BufferData(Target, (IntPtr)Size, IntPtr.Zero, BufferUsageHint.StreamDraw);
            }

            public override void SetData(long Size, IntPtr HostAddress)
            {
                GL.BindBuffer(Target, Handle);

                GL.BufferSubData(Target, IntPtr.Zero, (IntPtr)Size, HostAddress);
            }
        }
    }
}
