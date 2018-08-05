using OpenTK.Graphics.Vulkan;
using System;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class VulkanDeviceQuery
    {
        public VkPhysicalDevice PhysicalDevice { private set; get; }
        public VkDevice Device { private set; get; }

        private VkPhysicalDeviceMemoryProperties DeviceMemoryProperties;

        public VulkanDeviceQuery(VkPhysicalDevice PhysicalDevice, VkDevice Device)
        {
            this.PhysicalDevice = PhysicalDevice;
            this.Device         = Device;

            VK.GetPhysicalDeviceMemoryProperties(PhysicalDevice, out DeviceMemoryProperties);
        }

        public uint GetMemoryTypeIndex(uint TypeBits, VkMemoryPropertyFlags Properties)
        {
            for (uint i = 0; i < DeviceMemoryProperties.memoryTypeCount; i++)
            {
                if ((TypeBits & 1) == 1)
                {
                    if ((GetMemoryType(i).propertyFlags & Properties) == Properties)
                    {
                        return i;
                    }
                }
                TypeBits >>= 1;
            }

            throw new InvalidOperationException("Failed to find suitable memory type");
        }

        private unsafe VkMemoryType GetMemoryType(uint Index)
        {
            VkPhysicalDeviceMemoryProperties Props = DeviceMemoryProperties;

            VkMemoryType MemoryType = (&Props.memoryTypes_0)[Index];

            return MemoryType;
        }
    }
}