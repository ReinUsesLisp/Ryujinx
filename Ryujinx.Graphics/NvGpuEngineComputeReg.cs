namespace Ryujinx.Graphics
{
    enum NvGpuEngineComputeReg
    {
        SharedBase     = 0x0085,
        ComputeBegin   = 0x00a7,
        Unknown00C4    = 0x00c4,
        LocalBase      = 0x01df,
        TempAddress    = 0x01e4,
        ComputeEnd     = 0x0281,
        Unknown054A    = 0x054a,
        TscAddress     = 0x0557,
        TicAddress     = 0x055d,
        TscLimit       = 0x0559,
        TicLimit       = 0x055f,
        CodeAddress    = 0x0582,
        Unknown0982    = 0x0982
    }
}