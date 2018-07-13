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
            Handle = 0;
            Mapped = false;

            this.Target = Target;
            this.Size   = MaxSize;
        }

        public static OGLStreamBuffer Create(BufferTarget Target, long MaxSize)
        {
            return new SubDataBuffer(Target, MaxSize);
        }

        public IntPtr Map(long Size)
        {
            if (Handle == 0 || Mapped || Size > this.Size)
            {
                throw new InvalidOperationException();
            }

            IntPtr Memory = InternMap(Size);

            Mapped = true;

            return Memory;
        }

        public void Unmap(long UsedSize)
        {
            if (Handle == 0 || !Mapped)
            {
                throw new InvalidOperationException();
            }

            InternUnmap(UsedSize);

            Mapped = false;
        }

        protected abstract IntPtr InternMap(long Size);

        protected abstract void InternUnmap(long UsedSize);

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
            private IntPtr Memory;

            public SubDataBuffer(BufferTarget Target, long MaxSize)
                : base(Target, MaxSize)
            {
                Memory = Marshal.AllocHGlobal((IntPtr)Size);

                Handle = GL.GenBuffer();

                GL.BindBuffer(Target, Handle);

                GL.BufferData(Target, (IntPtr)Size, IntPtr.Zero, BufferUsageHint.StreamDraw);
            }

            protected override IntPtr InternMap(long Size)
            {
                return Memory;
            }

            protected override void InternUnmap(long UsedSize)
            {
                GL.BindBuffer(Target, Handle);

                GL.BufferSubData(Target, IntPtr.Zero, (IntPtr)UsedSize, Memory);
            }
        }
    }
}
