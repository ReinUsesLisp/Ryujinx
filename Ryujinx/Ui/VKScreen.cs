using OpenTK.Graphics;
using Ryujinx.Graphics.Gal;
using Ryujinx.Graphics.Gal.Vulkan;
using Ryujinx.HLE;

namespace Ryujinx
{
    public class VKScreen : Screen
    {
        private readonly VulkanRenderer VKRenderer;

        public VKScreen(Switch Ns, IGalRenderer Renderer)
            : base(Ns, Renderer, GraphicsMode.Default)
        {
            VKRenderer = Renderer as VulkanRenderer;
        }

        protected override void Prepare()
        {
        }

        protected override void PrepareRender()
        {
            VKRenderer.Initialize(this);
        }

        protected override void SwapBuffers()
        {
            VKRenderer.SwapBuffers();
        }

        protected override void Resized()
        {
            VKRenderer.FrameBuffer.SetWindowSize(Width, Height);
        }

        protected override void Shutdown()
        {
            VKRenderer.Dispose();
        }
    }
}
