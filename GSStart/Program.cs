using System;
using System.Diagnostics;
using System.Threading;
using GameStart;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GSStart
{
    static class Program
    {
        const string exeName = "Embark";

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        static bool IsEmbarkAlive()
        {
            return Process.GetProcessesByName(exeName).Length > 0;
        }

        static bool IsMouseComboPressed(ref Joypad cntr)
        {
            var buttons = cntr.GetInput();

            return buttons != null && (buttons.Start && buttons.LS);
        }

        static bool IsOpenComboPressed(ref Joypad cntr)
        {
            var buttons = cntr.GetInput();

            return buttons != null && (buttons.Start && buttons.Select);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            while (true)
            {
                Search();

                Thread.Sleep(200);
            }
        }

        [DllImport("user32.dll")]

        private static extern void mouse_event(uint flags, uint x, uint y, uint data, uint extraInf);
        static void Search()
        {
            var controller = Joypad.TryConnect();

            while (controller?.IsConnected() == true)
            {
                if (!IsEmbarkAlive() && IsOpenComboPressed(ref controller))
                {
                    Thread.Sleep(2000);

                    if (IsOpenComboPressed(ref controller))
                    {
                        WiggleMouse();

                        StartEmbark();
                    }
                }
                else if (IsMouseComboPressed(ref controller))
                {
                    Thread.Sleep(2000);

                    if (IsMouseComboPressed(ref controller))
                    {
                        WiggleMouse();

                        while (IsMouseComboPressed(ref controller)) { /*wait*/ }

                        StartMouseMode(ref controller);
                    }
                }

                Thread.Sleep(200); // loop until disconnect
            }

            controller = null;
        }

        static void StartEmbark()
        {
            Process process = new Process();
            process.StartInfo.FileName = exeName + ".exe";
            process.StartInfo.CreateNoWindow = true;
            process.Start();
        }

        static void StartMouseMode(ref Joypad controller)
        {
            var isClicked = false;

            while (!IsMouseComboPressed(ref controller))
            {
                Thread.Sleep(20);

                var buttons = controller.GetInput();

                if (!isClicked && buttons.A)
                {
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    isClicked = true;
                }
                else if (isClicked && !buttons.A)
                {
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    isClicked = false;
                }

                if (!IsEmbarkAlive() && buttons.Start && buttons.Select)
                {
                    Thread.Sleep(2000);

                    if (buttons.Start && buttons.Select)
                    {
                        WiggleMouse();

                        StartEmbark();
                    }
                }

                if (Math.Abs(buttons.XAxis) < 3000 &&
                    Math.Abs(buttons.YAxis) < 3000)
                    continue;


                var x = (int)((double)buttons.XAxis * 0.0005);
                var y = (int)((double)-buttons.YAxis * 0.0005);

                Cursor.Position = new Point(Cursor.Position.X + x, Cursor.Position.Y + y);
            }
        }

        static void WiggleMouse()
        {
            const int wiggleDist = 10;

            for (int i = 0; i < 3; i++)
            {
                Cursor.Position = new Point(Cursor.Position.X + wiggleDist, Cursor.Position.Y);
                Thread.Sleep(50);
                Cursor.Position = new Point(Cursor.Position.X - wiggleDist, Cursor.Position.Y);
                Thread.Sleep(50);
            }
        }
    }
}
