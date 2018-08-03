﻿using System;
using System.Collections.Concurrent;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    public partial class VulkanRenderer : IGalRenderer
    {
        public IGalConstBuffer Buffer { get; private set; }

        public IGalFrameBuffer FrameBuffer { get; private set; }

        public IGalRasterizer Rasterizer { get; private set; }

        public IGalShader Shader { get; private set; }

        public IGalPipeline Pipeline { get; private set; }

        public IGalTexture Texture { get; private set; }

        private ConcurrentQueue<Action> ActionsQueue;

        public VulkanRenderer()
        {
            ActionsQueue = new ConcurrentQueue<Action>();
        }

        public void QueueAction(Action ActionMthd)
        {
            ActionsQueue.Enqueue(ActionMthd);
        }

        public void RunActions()
        {
            int Count = ActionsQueue.Count;

            while (Count-- > 0 && ActionsQueue.TryDequeue(out Action RenderAction))
            {
                RenderAction();
            }
        }
    }
}