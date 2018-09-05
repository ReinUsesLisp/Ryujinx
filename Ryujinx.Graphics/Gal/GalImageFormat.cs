﻿using System;

namespace Ryujinx.Graphics.Gal
{
    [Flags]
    public enum GalImageFormat
    {
        Snorm  = 1 << 27,
        Unorm  = 1 << 28,
        Sint   = 1 << 29,
        Uint   = 1 << 30,
        Sfloat = 1 << 31,

        TypeMask = Snorm | Unorm | Sint | Uint | Sfloat,

        FormatMask = ~TypeMask,

        ASTC_BEGIN = ASTC_4x4,

        ASTC_4x4 = 0,
        ASTC_5x4,
        ASTC_5x5,
        ASTC_6x5,
        ASTC_6x6,
        ASTC_8x5,
        ASTC_8x6,
        ASTC_8x8,
        ASTC_10x5,
        ASTC_10x6,
        ASTC_10x8,
        ASTC_10x10,
        ASTC_12x10,
        ASTC_12x12,

        ASTC_END = ASTC_12x12,

        R4G4,
        R4G4B4A4,
        B4G4R4A4,
        R5G6B5,
        B5G6R5,
        R5G5B5A1,
        B5G5R5A1,
        A1R5G5B5,
        R8,
        R8G8,
        R8G8B8,
        B8G8R8,
        R8G8B8A8,
        B8G8R8A8,
        A8B8G8R8,
        A8B8G8R8_SRGB,
        A2R10G10B10,
        A2B10G10R10,
        R16,
        R16G16,
        R16G16B16,
        R16G16B16A16,
        R32,
        R32G32,
        R32G32B32,
        R32G32B32A32,
        R64,
        R64G64,
        R64G64B64,
        R64G64B64A64,
        B10G11R11,
        E5B9G9R9,
        D16,
        X8_D24,
        D32,
        S8,
        D16_S8,
        D24_S8,
        D32_S8,
        BC1_RGB,
        BC1_RGBA,
        BC2,
        BC3,
        BC4,
        BC5,
        BC6H_SF16,
        BC6H_UF16,
        BC7,
        ETC2_R8G8B8,
        ETC2_R8G8B8A1,
        ETC2_R8G8B8A8,
        EAC_R11,
        EAC_R11G11,

        REVERSED_BEGIN,

        R4G4B4A4_REVERSED = REVERSED_BEGIN,

        REVERSED_END
    }
}