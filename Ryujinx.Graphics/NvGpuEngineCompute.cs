using Ryujinx.Graphics.Memory;
using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics
{
    public class NvGpuEngineCompute : INvGpuEngine
    {
        public int[] Registers { private set; get; }

        private NvGpu Gpu;

        private Dictionary<int, NvGpuMethod> Methods;

        public NvGpuEngineCompute(NvGpu Gpu)
        {
            this.Gpu = Gpu;

            Registers = new int[0xcf7];

            Methods = new Dictionary<int, NvGpuMethod>();

            void AddMethod(int Meth, int Count, int Stride, NvGpuMethod Method)
            {
                while (Count-- > 0)
                {
                    Methods.Add(Meth, Method);

                    Meth += Stride;
                }
            }

            AddMethod(0x281, 1, 1, EndCompute);
        }

        private void EndCompute(NvGpuVmm Vmm, NvGpuPBEntry PBEntry)
        {
            throw new NotImplementedException("Compute shaders are not implemented");
        }

        public void CallMethod(NvGpuVmm Vmm, NvGpuPBEntry PBEntry)
        {
            if (Methods.TryGetValue(PBEntry.Method, out NvGpuMethod Method))
            {
                Method(Vmm, PBEntry);
            }
            else
            {
                WriteRegister(PBEntry);
            }
        }

        private long MakeInt64From2xInt32(NvGpuEngineComputeReg Reg)
        {
            return
                (long)Registers[(int)Reg + 0] << 32 |
                (uint)Registers[(int)Reg + 1];
        }

        private void WriteRegister(NvGpuPBEntry PBEntry)
        {
            int ArgsCount = PBEntry.Arguments.Count;

            if (ArgsCount > 0)
            {
                Registers[PBEntry.Method] = PBEntry.Arguments[ArgsCount - 1];
            }
        }

        private int ReadRegister(NvGpuEngineComputeReg Reg)
        {
            return Registers[(int)Reg];
        }

        private void WriteRegister(NvGpuEngineComputeReg Reg, int Value)
        {
            Registers[(int)Reg] = Value;
        }
    }
}