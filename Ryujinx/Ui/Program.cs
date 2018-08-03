using Ryujinx.Audio;
using Ryujinx.Audio.OpenAL;
using Ryujinx.Graphics.Gal;
using Ryujinx.Graphics.Gal.OpenGL;
using Ryujinx.Graphics.Gal.Vulkan;
using Ryujinx.HLE;
using Ryujinx.HLE.Logging;
using System;
using System.IO;

namespace Ryujinx
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Ryujinx Console";

            Logger Log = new Logger();

            Config.Read(Log);

            IGalRenderer Renderer = CreateRenderer();

            IAalOutput AudioOut = new OpenALAudioOut();

            Switch Ns = new Switch(Log, Renderer, AudioOut);

            Ns.Log.Updated += ConsoleLog.PrintLog;

            if (args.Length == 1)
            {
                if (Directory.Exists(args[0]))
                {
                    string[] RomFsFiles = Directory.GetFiles(args[0], "*.istorage");

                    if (RomFsFiles.Length == 0)
                    {
                        RomFsFiles = Directory.GetFiles(args[0], "*.romfs");
                    }

                    if (RomFsFiles.Length > 0)
                    {
                        Console.WriteLine("Loading as cart with RomFS.");

                        Ns.LoadCart(args[0], RomFsFiles[0]);
                    }
                    else
                    {
                        Console.WriteLine("Loading as cart WITHOUT RomFS.");

                        Ns.LoadCart(args[0]);
                    }
                }
                else if (File.Exists(args[0]))
                {
                    Console.WriteLine("Loading as homebrew.");

                    Ns.LoadProgram(args[0]);
                }
            }
            else
            {
                Console.WriteLine("Please specify the folder with the NSOs/IStorage or a NSO/NRO.");
            }

            using (Screen Screen = CreateScreen(Ns, Renderer))
            {
                Screen.MainLoop();

                Ns.OnFinish(EventArgs.Empty);
            }

            Environment.Exit(0);
        }

        static IGalRenderer CreateRenderer()
        {
            switch (Config.GraphicsAPI)
            {
                case GraphicsAPI.OpenGL:
                    return new OGLRenderer();

                case GraphicsAPI.Vulkan:
                    return new VulkanRenderer();

                default:
                    throw new InvalidOperationException();
            }
        }

        static Screen CreateScreen(Switch Ns, IGalRenderer Renderer)
        {
            switch (Config.GraphicsAPI)
            {
                case GraphicsAPI.OpenGL: return new GLScreen(Ns, Renderer);
                case GraphicsAPI.Vulkan: return new VKScreen(Ns, Renderer);

                default: throw new InvalidOperationException();
            }
        }
    }
}
