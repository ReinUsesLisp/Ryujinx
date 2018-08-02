using System;
using OpenTK;
using OpenTK.Graphics;
using Ryujinx.Graphics.Gal;
using Ryujinx.HLE;

namespace Ryujinx
{
    public class GLScreen : Screen
    {
        private static GraphicsMode GraphicsMode = GraphicsMode.Default;

        public GraphicsContext Context;

        public GLScreen(Switch Ns, IGalRenderer Renderer)
            : base(Ns, Renderer, GraphicsMode)
        {
        }

        protected override void Prepare()
        {
        }

        protected override void PrepareRender()
        {
            Context = new GraphicsContext(
                GraphicsMode,
                WindowInfo,
                3, 3,
                GraphicsContextFlags.ForwardCompatible);

            Context.MakeCurrent(WindowInfo);

            (Context as IGraphicsContextInternal).LoadAll();

            Context.SwapInterval = 0;

            Renderer.FrameBuffer.SetWindowSize(Width, Height);
        }

        protected override void SwapBuffers()
        {
            Context.SwapBuffers();
        }

        protected override void Resized()
        {
            Context.Update(WindowInfo);
        }

        protected override void Dispose()
        {
            Context.Dispose();
        }
    }
}