using OpenTK.Graphics.Vulkan;
using System;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class QueueFamilyIndices
    {
        public int GraphicsFamily = -1;
        public int PresentFamily  = -1;

        public bool IsComplete()
        {
            return GraphicsFamily >= 0 && PresentFamily >= 0;
        }

        public static unsafe QueueFamilyIndices Find(VkPhysicalDevice PhysicalDevice, VkSurfaceKHR Surface)
        {
            QueueFamilyIndices Indices = new QueueFamilyIndices();

            uint QueueFamilyCount = 0;
            VK.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, ref QueueFamilyCount, (VkQueueFamilyProperties*)null);

            VulkanList<VkQueueFamilyProperties> QueueFamilies = new VulkanList<VkQueueFamilyProperties>(QueueFamilyCount);
            VK.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, ref QueueFamilyCount, (VkQueueFamilyProperties*)QueueFamilies.Data);

            for (int i = 0; i < QueueFamilyCount; i++)
            {
                VkQueueFamilyProperties QueueFamily = QueueFamilies[i];

                if (QueueFamily.queueCount > 0 && QueueFamily.queueFlags.HasFlag(VkQueueFlags.Graphics))
                {
                    Indices.GraphicsFamily = i;
                }

                VK.GetPhysicalDeviceSurfaceSupportKHR(PhysicalDevice, (uint)i, Surface, out VkBool32 PresentSupported);

                if (QueueFamily.queueCount > 0 && PresentSupported)
                {
                    Indices.PresentFamily = i;
                }

                if (Indices.IsComplete())
                {
                    break;
                }
            }

            if (!Indices.IsComplete())
            {
                throw new NotSupportedException("Failed to find graphics queue");
            }

            return Indices;
        }
    }
}