using OpenTK.Graphics.Vulkan;
using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    using static VulkanHelper;

    internal class VulkanSynchronization
    {
        internal struct Call
        {
            public VulkanList<VkCommandBuffer> Commands;
            public List<VkCommandPool> Pools;
            public VkFence Fence;
            public VkSemaphore Semaphore;
        }

        private readonly VkDevice Device;

        private VkCommandPool OneShotPool;

        private List<Call> Calls;

        private Call CurrentCall;

        private VkSemaphore WaitSemaphore;

        public VulkanSynchronization(VkDevice Device, uint QueueFamilyIndex)
        {
            this.Device = Device;

            Calls = new List<Call>(1024);

            CleanCall();

            VkCommandPoolCreateInfo CommandPoolCI = new VkCommandPoolCreateInfo()
            {
                sType = VkStructureType.CommandPoolCreateInfo,
                flags = VkCommandPoolCreateFlags.Transient,
                queueFamilyIndex = QueueFamilyIndex
            };

            Check(VK.CreateCommandPool(Device, ref CommandPoolCI, IntPtr.Zero, out OneShotPool));
        }

        public void Dispose()
        {
            VK.DeviceWaitIdle(Device);

            FreeUnusedMemory();

            VK.DestroyCommandPool(Device, OneShotPool, IntPtr.Zero);
        }

        public void AddCommand(VkCommandBuffer Command, VkCommandPool Pool)
        {
            CurrentCall.Commands.Add(Command);

            CurrentCall.Pools.Add(Pool);
        }

        public void AddCommand(VkCommandBuffer Command)
        {
            AddCommand(Command, VkCommandPool.Null);
        }

        public VkCommandBuffer BeginRecord()
        {
            VkCommandBufferAllocateInfo CommandBufferAI = new VkCommandBufferAllocateInfo()
            {
                sType = VkStructureType.CommandBufferAllocateInfo,
                commandBufferCount = 1,
                commandPool = OneShotPool,
                level = VkCommandBufferLevel.Primary
            };

            Check(VK.AllocateCommandBuffers(Device, ref CommandBufferAI, out VkCommandBuffer CommandBuffer));

            VkCommandBufferBeginInfo BeginInfo = new VkCommandBufferBeginInfo()
            {
                sType = VkStructureType.CommandBufferBeginInfo,
                flags = VkCommandBufferUsageFlags.OneTimeSubmit
            };

            Check(VK.BeginCommandBuffer(CommandBuffer, ref BeginInfo));

            AddCommand(CommandBuffer, OneShotPool);

            return CommandBuffer;
        }

        public unsafe void Execute(VkQueue Queue)
        {
            VkSemaphoreCreateInfo SemaphoreCI = new VkSemaphoreCreateInfo()
            {
                sType = VkStructureType.SemaphoreCreateInfo
            };

            Check(VK.CreateSemaphore(Device, ref SemaphoreCI, IntPtr.Zero, out VkSemaphore Semaphore));

            CurrentCall.Semaphore = Semaphore;

            VkFenceCreateInfo FenceCI = new VkFenceCreateInfo()
            {
                sType = VkStructureType.FenceCreateInfo,
                flags = VkFenceCreateFlags.None
            };

            Check(VK.CreateFence(Device, ref FenceCI, IntPtr.Zero, out CurrentCall.Fence));

            VkSubmitInfo SubmitInfo = new VkSubmitInfo()
            {
                sType = VkStructureType.SubmitInfo,
                commandBufferCount = CurrentCall.Commands.Count,
                pCommandBuffers = (VkCommandBuffer*)CurrentCall.Commands.Data,
                signalSemaphoreCount = 1,
                pSignalSemaphores = &Semaphore
            };

            if (WaitSemaphore != VkSemaphore.Null)
            {
                //TODO: This could be optimized in some way
                VkPipelineStageFlags StageFlags = VkPipelineStageFlags.AllCommands;

                VkSemaphore DimPreviousSemaphore = WaitSemaphore;

                SubmitInfo.waitSemaphoreCount = 1;
                SubmitInfo.pWaitSemaphores = &DimPreviousSemaphore;
                SubmitInfo.pWaitDstStageMask = &StageFlags;
            }

            Check(VK.QueueSubmit(Queue, 1, ref SubmitInfo, CurrentCall.Fence));

            WaitSemaphore = Semaphore;

            Calls.Add(CurrentCall);

            CleanCall();
        }

        public VkSemaphore QuerySemaphore()
        {
            VkSemaphore WaitSemaphore = this.WaitSemaphore;

            WaitSemaphore = VkSemaphore.Null;

            return WaitSemaphore;
        }

        public unsafe void FreeUnusedMemory()
        {
            foreach (Call Call in Calls)
            {
                VkResult Result = VK.WaitForFences(Device, 1, &Call.Fence, true, 0);
                
                if (Result == VkResult.Success)
                {
                    VK.DestroySemaphore(Device, Call.Semaphore, IntPtr.Zero);

                    VK.DestroyFence(Device, Call.Fence, IntPtr.Zero);

                    for (int i = 0; i < Call.Commands.Count; i++)
                    {
                        VkCommandPool Pool = Call.Pools[i];

                        if (Pool != VkCommandPool.Null)
                        {
                            VkCommandBuffer CommandBuffer = Call.Commands[i];

                            VK.FreeCommandBuffers(Device, Pool, 1, &CommandBuffer);
                        }
                    }
                }
                else if (Result != VkResult.Timeout)
                {
                    throw new VulkanException(Result);
                }

                Calls.Remove(Call);
            }
        }

        private void CleanCall()
        {
            CurrentCall = new Call();

            CurrentCall.Commands = new VulkanList<VkCommandBuffer>(4);

            CurrentCall.Pools = new List<VkCommandPool>(4);
        }
    }
}