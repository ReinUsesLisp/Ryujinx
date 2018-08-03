using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;
using Ryujinx.Graphics.Gal;
using Ryujinx.HLE;
using Ryujinx.HLE.Input;
using System;
using System.Threading;

using Stopwatch = System.Diagnostics.Stopwatch;

namespace Ryujinx
{
    public abstract class Screen : NativeWindow
    {
        private const int TouchScreenWidth  = 1280;
        private const int TouchScreenHeight = 720;

        private const int TargetFPS = 60;

        protected Switch Ns { private set; get; }

        protected IGalRenderer Renderer { private set; get; }

        private KeyboardState? Keyboard = null;

        private MouseState? Mouse = null;

        private Thread RenderThread;

        private bool RenderThreadPrepared;

        private bool ResizeEvent;

        private bool TitleEvent;

        private string NewTitle;

        private bool Quit;

        public Screen(Switch Ns, IGalRenderer Renderer, GraphicsMode GraphicsMode)
            : base(1280, 720, "Ryujinx",
                  GameWindowFlags.Default,
                  GraphicsMode,
                  DisplayDevice.Default)
        {
            this.Ns       = Ns;
            this.Renderer = Renderer;

            KeyDown   += (sender, e) => { OnKeyDown(e); };
            KeyUp     += (sender, e) => { Keyboard = e.Keyboard; };

            MouseDown += (sender, e) => { Mouse = e.Mouse; };
            MouseUp   += (sender, e) => { Mouse = e.Mouse; };
            MouseMove += (sender, e) => { Mouse = e.Mouse; };

            Resize += (sender, e) =>
            {
                ResizeEvent = true;
                Resized();
            };

            Location = new Point(
                (DisplayDevice.Default.Width  / 2) - (Width  / 2),
                (DisplayDevice.Default.Height / 2) - (Height / 2));
        }

        private void RenderLoop()
        {
            PrepareRender();

            Renderer.FrameBuffer.SetWindowSize(Width, Height);

            RenderThreadPrepared = true;

            Stopwatch Chrono = new Stopwatch();

            Chrono.Start();

            long TicksPerFrame = Stopwatch.Frequency / TargetFPS;

            long Ticks = 0;

            while (Exists && !Quit)
            {
                if (Ns.WaitFifo())
                {
                    Ns.ProcessFrame();
                }

                Renderer.RunActions();

                if (ResizeEvent)
                {
                    ResizeEvent = false;

                    Renderer.FrameBuffer.SetWindowSize(Width, Height);
                }

                Ticks += Chrono.ElapsedTicks;

                Chrono.Restart();

                if (Ticks >= TicksPerFrame)
                {
                    RenderFrame();

                    //Queue max. 1 vsync
                    Ticks = Math.Min(Ticks - TicksPerFrame, TicksPerFrame);
                }
            }

            RenderThread.Join();
        }

        public void MainLoop()
        {
            Prepare();

            Visible = true;

            RenderThread = new Thread(RenderLoop);

            RenderThread.Start();

            while (!RenderThreadPrepared)
            {
                Thread.Yield();
            }

            while (Exists && !Quit)
            {
                ProcessEvents();

                if (!Quit)
                {
                    UpdateFrame();

                    if (TitleEvent)
                    {
                        TitleEvent = false;

                        Title = NewTitle;
                    }
                }

                //Polling becomes expensive if it's not slept
                Thread.Sleep(1);
            }

            Dispose();
        }

        private void UpdateFrame()
        {
            HidControllerButtons CurrentButton = 0;
            HidJoystickPosition  LeftJoystick;
            HidJoystickPosition  RightJoystick;

            int LeftJoystickDX  = 0;
            int LeftJoystickDY  = 0;
            int RightJoystickDX = 0;
            int RightJoystickDY = 0;

            //Keyboard Input
            if (Keyboard.HasValue)
            {
                KeyboardState Keyboard = this.Keyboard.Value;

                CurrentButton = Config.JoyConKeyboard.GetButtons(Keyboard);

                (LeftJoystickDX, LeftJoystickDY) = Config.JoyConKeyboard.GetLeftStick(Keyboard);

                (RightJoystickDX, RightJoystickDY) = Config.JoyConKeyboard.GetRightStick(Keyboard);
            }

            //Controller Input
            CurrentButton |= Config.JoyConController.GetButtons();
                
            //Keyboard has priority stick-wise
            if (LeftJoystickDX == 0 && LeftJoystickDY == 0)
            {
                (LeftJoystickDX, LeftJoystickDY) = Config.JoyConController.GetLeftStick();
            }

            if (RightJoystickDX == 0 && RightJoystickDY == 0)
            {
                (RightJoystickDX, RightJoystickDY) = Config.JoyConController.GetRightStick();
            }
            
            LeftJoystick = new HidJoystickPosition
            {
                DX = LeftJoystickDX,
                DY = LeftJoystickDY
            };

            RightJoystick = new HidJoystickPosition
            {
                DX = RightJoystickDX,
                DY = RightJoystickDY
            };

            bool HasTouch = false;

            //Get screen touch position from left mouse click
            //OpenTK always captures mouse events, even if out of focus, so check if window is focused.
            if (Focused && Mouse?.LeftButton == ButtonState.Pressed)
            {
                MouseState Mouse = this.Mouse.Value;

                int ScrnWidth  = Width;
                int ScrnHeight = Height;

                if (Width > (Height * TouchScreenWidth) / TouchScreenHeight)
                {
                    ScrnWidth = (Height * TouchScreenWidth) / TouchScreenHeight;
                }
                else
                {
                    ScrnHeight = (Width * TouchScreenHeight) / TouchScreenWidth;
                }

                int StartX = (Width  - ScrnWidth)  >> 1;
                int StartY = (Height - ScrnHeight) >> 1;

                int EndX = StartX + ScrnWidth;
                int EndY = StartY + ScrnHeight;

                if (Mouse.X >= StartX &&
                    Mouse.Y >= StartY &&
                    Mouse.X <  EndX   &&
                    Mouse.Y <  EndY)
                {
                    int ScrnMouseX = Mouse.X - StartX;
                    int ScrnMouseY = Mouse.Y - StartY;

                    int MX = (ScrnMouseX * TouchScreenWidth)  / ScrnWidth;
                    int MY = (ScrnMouseY * TouchScreenHeight) / ScrnHeight;

                    HidTouchPoint CurrentPoint = new HidTouchPoint
                    {
                        X = MX,
                        Y = MY,

                        //Placeholder values till more data is acquired
                        DiameterX = 10,
                        DiameterY = 10,
                        Angle     = 90
                    };

                    HasTouch = true;

                    Ns.Hid.SetTouchPoints(CurrentPoint);
                }
            }

            if (!HasTouch)
            {
                Ns.Hid.SetTouchPoints();
            }

            Ns.Hid.SetJoyconButton(
                HidControllerId.CONTROLLER_HANDHELD,
                HidControllerLayouts.Handheld_Joined,
                CurrentButton,
                LeftJoystick,
                RightJoystick);

            Ns.Hid.SetJoyconButton(
                HidControllerId.CONTROLLER_HANDHELD,
                HidControllerLayouts.Main,
                CurrentButton,
                LeftJoystick,
                RightJoystick);
        }

        private void RenderFrame()
        {
            Renderer.FrameBuffer.Render();

            Ns.Statistics.RecordSystemFrameTime();

            double HostFps = Ns.Statistics.GetSystemFrameRate();
            double GameFps = Ns.Statistics.GetGameFrameRate();

            NewTitle = $"Ryujinx | Host FPS: {HostFps:0.0} | Game FPS: {GameFps:0.0}";

            TitleEvent = true;

            SwapBuffers();

            Ns.Os.SignalVsync();
        }

        private new void OnKeyDown(KeyboardKeyEventArgs e)
        {
            bool ToggleFullscreen = e.Key == Key.F11 ||
                (e.Modifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Enter);

            if (WindowState == WindowState.Fullscreen)
            {
                if (e.Key == Key.Escape || ToggleFullscreen)
                {
                    WindowState = WindowState.Normal;
                }
            }
            else
            {
                if (e.Key == Key.Escape)
                {
                    Quit = true;
                }

                if (ToggleFullscreen)
                {
                    WindowState = WindowState.Fullscreen;
                }
            }

            Keyboard = e.Keyboard;
        }

        protected abstract void Prepare();

        protected abstract void PrepareRender();

        protected abstract void Dispose();

        protected abstract void SwapBuffers();

        protected abstract void Resized();
    }
}