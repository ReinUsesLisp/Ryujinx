using OpenTK.Graphics.Vulkan;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    using static VulkanHelper;

    internal class VulkanConstBuffer : IGalConstBuffer
    {
        struct VulkanBuffer
        {
            public VkBuffer StagingBuffer;
            public VkDeviceMemory StagingMemory;

            public VkBuffer DeviceBuffer;
            public VkDeviceMemory DeviceMemory;

            public VulkanBuffer(
                VkBuffer StagingBuffer, VkDeviceMemory StagingMemory,
                VkBuffer DeviceBuffer, VkDeviceMemory DeviceMemory)
            {
                this.StagingBuffer = StagingBuffer;
                this.StagingMemory = StagingMemory;

                this.DeviceBuffer = DeviceBuffer;
                this.DeviceMemory = DeviceMemory;
            }
        }

        private readonly VulkanSynchronization Synchronization;
        private readonly VulkanDeviceQuery DeviceQuery;
        private readonly VkSurfaceKHR Surface;
        private readonly VkPhysicalDevice PhysicalDevice;
        private readonly VkDevice Device;
        private readonly VkQueue GraphicsQueue;
        private readonly uint GraphicsFamily;

        private CachedResource<VulkanBuffer> Cache;

        public VulkanConstBuffer(
            VulkanSynchronization Synchronization,
            VulkanDeviceQuery DeviceQuery,
            VkSurfaceKHR Surface,
            VkPhysicalDevice PhysicalDevice,
            VkDevice Device,
            VkQueue GraphicsQueue)
        {
            this.Synchronization = Synchronization;
            this.DeviceQuery = DeviceQuery;
            this.Surface = Surface;
            this.PhysicalDevice = PhysicalDevice;
            this.Device = Device;
            this.GraphicsQueue = GraphicsQueue;

            GraphicsFamily = (uint)QueueFamilyIndices.Find(PhysicalDevice, Surface).GraphicsFamily;

            Cache = new CachedResource<VulkanBuffer>((Buffer) => DeleteBuffer(Device, Buffer));
        }

        public unsafe void Create(long Key, long Size)
        {
            CreateBuffer(
                DeviceQuery,
                (ulong)Size,
                VkBufferUsageFlags.TransferSrc,
                VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
                out VkBuffer StagingBuffer, out VkDeviceMemory StagingMemory);

            CreateBuffer(
                DeviceQuery,
                (ulong)Size,
                VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.UniformBuffer,
                VkMemoryPropertyFlags.DeviceLocal,
                out VkBuffer DeviceBuffer, out VkDeviceMemory DeviceMemory);

            VulkanBuffer Buffer = new VulkanBuffer(StagingBuffer, StagingMemory, DeviceBuffer, DeviceMemory);

            Cache.AddOrUpdate(Key, Buffer, Size);
        }

        public bool IsCached(long Key, long Size)
        {
            return Cache.TryGetSize(Key, out long CachedSize) && CachedSize == Size;
        }

        public void LockCache()
        {
            Cache.Lock();
        }

        public unsafe void SetData(long Key, long Size, IntPtr HostAddress)
        {
            if (!Cache.TryGetValue(Key, out VulkanBuffer VulkanBuffer))
            {
                throw new InvalidOperationException();
            }

            IntPtr MapAddress;

            Check(VK.MapMemory(Device, VulkanBuffer.StagingMemory, 0, (ulong)Size, 0, (void**)&MapAddress));

            Buffer.MemoryCopy((void*)HostAddress, (void*)MapAddress, Size, Size);

            VK.UnmapMemory(Device, VulkanBuffer.DeviceMemory);

            VkCommandBuffer CopyCmd = Synchronization.BeginRecord();

            VkBufferCopy BufferCopy = new VkBufferCopy()
            {
                srcOffset = 0,
                dstOffset = 0,
                size = (ulong)Size
            };

            VK.CmdCopyBuffer(CopyCmd, VulkanBuffer.StagingBuffer, VulkanBuffer.DeviceBuffer, 1, ref BufferCopy);

            VK.EndCommandBuffer(CopyCmd);
        }

        public void UnlockCache()
        {
            Cache.Unlock();
        }

        private static void DeleteBuffer(VkDevice Device, VulkanBuffer Buffer)
        {
            VK.FreeMemory(Device, Buffer.StagingMemory, IntPtr.Zero);
            VK.DestroyBuffer(Device, Buffer.StagingBuffer, IntPtr.Zero);

            VK.FreeMemory(Device, Buffer.DeviceMemory, IntPtr.Zero);
            VK.DestroyBuffer(Device, Buffer.DeviceBuffer, IntPtr.Zero);
        }
    }
}