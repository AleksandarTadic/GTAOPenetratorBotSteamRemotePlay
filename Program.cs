using PenetratorBotSteam;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace PenetratorBot
{
    internal class Program
    {
        private enum ProcessDPIAwareness
        {
            ProcessDPIUnaware = 0,
            ProcessSystemDPIAware = 1,
            ProcessPerMonitorDPIAware = 2
        }
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(ProcessDPIAwareness value);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(System.Int32 vKey);

        private const int QUIT_KEY = 0x51; // 'Q' key

        static void Main(string[] args)
        {
            // set DPI awareness to handle proper screen resolution if we are zoomed in windows
            SetProcessDpiAwareness(ProcessDPIAwareness.ProcessPerMonitorDPIAware);

            // create new penetrator worket bot
            Penetrator penetrator = new Penetrator();

            // setup callback to interceptor
            //Interceptor.Callback = new InterceptionDelegate(penetrator.OnReceiveData);
            //Interceptor.EmulateController = false;
            //Interceptor.InjectionMode = InjectionMode.Compatability;

            Process remotePlayProcess;
            // Attempt to inject into PS Remote Play
            try
            {
                Console.WriteLine("Press 'Q' to quit program.");


                // find steam remote play process
                remotePlayProcess = Process.GetProcessesByName("streaming_client").FirstOrDefault();

                if (remotePlayProcess == null)
                {
                    Console.WriteLine("StreamingClient process not found. Exiting program...");
                    return;
                }

                IntPtr handle = remotePlayProcess.MainWindowHandle;
                SetForegroundWindow(handle);

                // create a thread to play the game
                Thread gameThread = new Thread(penetrator.PlayGame);
                gameThread.Start();

                int quitKey = 0;
                while (gameThread.IsAlive)
                {
                    // press 'Q' key to quit game
                    quitKey = GetAsyncKeyState(QUIT_KEY);

                    if ((quitKey & 0x01) == 0x01)
                    {
                        Console.WriteLine("Quit key pressed exiting program...");
                        break;
                    }
                    Thread.Sleep(100);
                }

                // end penetrator bot
                penetrator.EndProgram = true;

                // wait for our game thread to end
                gameThread.Join();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                Environment.Exit(-1);
            }
        }
    }
}

