using OpenTK.Graphics.Vulkan;
using System;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class VulkanException : Exception
    {
        public VulkanException(VkResult Result)
            : base(Result.ToString())
        {
        }
    }

    internal static class VulkanHelper
    {
        public static void Check(VkResult Result)
        {
            if (Result != VkResult.Success)
            {
                throw new VulkanException(Result);
            }
        }

        public static void SetImageLayout(
            VkCommandBuffer CommandBuffer,
            VkImage Image,
            VkImageLayout OldImageLayout,
            VkImageLayout NewImageLayout,
            VkImageSubresourceRange SubresourceRange,
            VkPipelineStageFlags SrcStageMask,
            VkPipelineStageFlags DstStageMask)
        {
            VkImageMemoryBarrier ImageMemoryBarrier = new VkImageMemoryBarrier()
            {
                sType = VkStructureType.ImageMemoryBarrier,
                oldLayout = OldImageLayout,
                newLayout = NewImageLayout,
                image = Image,
                subresourceRange = SubresourceRange
            };

            switch (OldImageLayout)
            {
                case VkImageLayout.Undefined:
                    ImageMemoryBarrier.srcAccessMask = VkAccessFlags.None;
                    break;

                case VkImageLayout.Preinitialized:
                    ImageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite;
                    break;

                case VkImageLayout.ColorAttachmentOptimal:
                    ImageMemoryBarrier.srcAccessMask = VkAccessFlags.ColorAttachmentWrite;
                    break;

                case VkImageLayout.DepthStencilAttachmentOptimal:
                    ImageMemoryBarrier.srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite;
                    break;

                case VkImageLayout.TransferSrcOptimal:
                    ImageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferRead;
                    break;

                case VkImageLayout.TransferDstOptimal:
                    ImageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferWrite;
                    break;

                case VkImageLayout.ShaderReadOnlyOptimal:
                    ImageMemoryBarrier.srcAccessMask = VkAccessFlags.ShaderRead;
                    break;

                default:
                    break;
            }

            switch (NewImageLayout)
            {
                case VkImageLayout.TransferDstOptimal:
                    ImageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferWrite;
                    break;

                case VkImageLayout.TransferSrcOptimal:
                    ImageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferRead;
                    break;

                case VkImageLayout.ColorAttachmentOptimal:
                    ImageMemoryBarrier.dstAccessMask = VkAccessFlags.ColorAttachmentWrite;
                    break;

                case VkImageLayout.DepthStencilAttachmentOptimal:
                    ImageMemoryBarrier.dstAccessMask |= VkAccessFlags.DepthStencilAttachmentWrite;
                    break;

                case VkImageLayout.ShaderReadOnlyOptimal:
                    if (ImageMemoryBarrier.srcAccessMask == VkAccessFlags.None)
                    {
                        ImageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite | VkAccessFlags.TransferWrite;
                    }

                    ImageMemoryBarrier.dstAccessMask = VkAccessFlags.ShaderRead;;
                    break;

                default:
                    break;
            }

            VK.CmdPipelineBarrier(
                CommandBuffer,
                SrcStageMask,
                DstStageMask,
                0,
                0, IntPtr.Zero,
                0, IntPtr.Zero,
                1, ref ImageMemoryBarrier);
        }

        public static void SetImageLayout(
            VkCommandBuffer CommandBuffer,
            VkImage Image,
            VkImageAspectFlags AspectMask,
            VkImageLayout OldImageLayout,
            VkImageLayout NewImageLayout,
            VkPipelineStageFlags SrcStageMask,
            VkPipelineStageFlags DstStageMask)
        {
            VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange()
            {
                aspectMask = AspectMask,
                baseMipLevel = 0,
                levelCount = 1,
                layerCount = 1
            };

            SetImageLayout(CommandBuffer, Image, OldImageLayout, NewImageLayout, subresourceRange, SrcStageMask, DstStageMask);
        }
    }
}
