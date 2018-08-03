using OpenTK.Graphics.Vulkan;
using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    using static VulkanHelper;

    internal class VulkanSemaphores
    {
        private readonly VkDevice Device;

        private VkSemaphore LastWait = VkSemaphore.Null;

        private List<VkSemaphore> PreviousSemaphores;

        public VulkanSemaphores(VkDevice Device)
        {
            this.Device = Device;

            PreviousSemaphores = new List<VkSemaphore>(1024);
        }

        public unsafe (VkSemaphore, VkSemaphore) QueueSemaphore()
        {
            VkSemaphoreCreateInfo SemaphoreCI = VkSemaphoreCreateInfo.New();

            Check(VK.CreateSemaphore(Device, &SemaphoreCI, IntPtr.Zero, out VkSemaphore Signal));

            VkSemaphore Wait = Pop();

            LastWait = Signal;

            return (Wait, Signal);
        }

        public VkSemaphore Pop()
        {
            VkSemaphore Wait = LastWait;

            if (LastWait != VkSemaphore.Null)
            {
                PreviousSemaphores.Add(LastWait);
            }

            LastWait = VkSemaphore.Null;

            return Wait;
        }

        public void ClearSemaphores()
        {
            /*
            foreach (VkSemaphore Semaphore in PreviousSemaphores)
            {
                VK.DestroySemaphore(Device, Semaphore, IntPtr.Zero);
            }
            */
        }
    }
}