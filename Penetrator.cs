using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PenetratorBotSteam
{
    internal class Penetrator
    {
        // Importing necessary Windows API methods for simulating keyboard input
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        // Define the constants for key press simulation (enter, spacebar, A, D)
        public const ushort VK_RETURN = 0x0D; // Enter DEFAULT_PRESS_TICKS
        public const ushort VK_SPACE = 0x20; // Spacebar
        public const ushort VK_A = 0x41;     // 'A' key
        public const ushort VK_D = 0x44;     // 'D' key

        public const uint KEYEVENTF_KEYDOWN = 0x0000; // Key down flag
        public const uint KEYEVENTF_KEYUP = 0x0002;   // Key up flag
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        // Structure definitions for simulating input events
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;  // Virtual Key code
            public ushort wScan; // Hardware scan code for the key
            public uint dwFlags; // Key press / Key release flags
            public uint time; // Time stamp for the event
            public UIntPtr dwExtraInfo; // Additional info about the event
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint dwMsg;
            public ushort wParamL;
            public ushort wParamH;
        }


        // Method to simulate key press
        private static readonly object _keyLock = new object();

        private static DateTime _lastKeyPressTime = DateTime.MinValue;
        private const int MIN_KEY_PRESS_INTERVAL_MS = 15; // Tune this (e.g. 50–100ms)

        private static void SendKeyPress(ushort keyCode, int tickLength = DEFAULT_PRESS_TICKS)
        {
            lock (_keyLock)
            {
                var now = DateTime.Now;
                if ((now - _lastKeyPressTime).TotalMilliseconds < MIN_KEY_PRESS_INTERVAL_MS)
                {
                    // Sleep if key press is too soon
                    Thread.Sleep(50);
                }

                _lastKeyPressTime = now;

                ushort scanCode = (ushort)MapVirtualKey(keyCode, 0);
                INPUT[] inputs = new INPUT[2];

                // Key down
                inputs[0] = new INPUT
                {
                    type = 1,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wScan = scanCode,
                            dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYDOWN,
                            time = 0,
                            dwExtraInfo = UIntPtr.Zero
                        }
                    }
                };

                // Key up
                inputs[1] = new INPUT
                {
                    type = 1,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wScan = scanCode,
                            dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = UIntPtr.Zero
                        }
                    }
                };
                if (tickLength >= 40)
                {
                    SendInput((uint)1, ref inputs[0], Marshal.SizeOf(typeof(INPUT)));
                    Thread.Sleep(80);
                    SendInput((uint)1, ref inputs[1], Marshal.SizeOf(typeof(INPUT)));
                }
                else
                {
                    SendInput((uint)1, ref inputs[0], Marshal.SizeOf(typeof(INPUT)));
                    Thread.Sleep(50);
                    SendInput((uint)1, ref inputs[1], Marshal.SizeOf(typeof(INPUT)));
                }
            }
        }







        private readonly object _firePositionLock = new object();


        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetDC(IntPtr window);
        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern uint GetPixel(IntPtr dc, int x, int y);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int ReleaseDC(IntPtr window, IntPtr dc);

        static Rectangle resolution = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

        // constants for already processed position
        const int PROCESSED_POSITION = 2000; // DEFAULT 20
        const int NONE = -1;
        const int maxStoredMoves = 2000; // DEFAULT 20

        // button press length is quick to ensure fast enough movement
        const int DEFAULT_PRESS_TICKS = 30;
        // weapon doesn't fire sometimes with quick button presses so we need to ensure this button press is longer so it
        // has a better chance of registering with the console
        const int FIRE_PRESS_TICKS = 40;
        const int NUM_RELEASE_TICKS = 10;

        //static readonly (int, int) NATIVE_RESOLUTION = (2560, 1440);
        static readonly (int, int) NATIVE_RESOLUTION = (1920, 1080);

        static readonly double xMultiple = (double)resolution.Width / NATIVE_RESOLUTION.Item1;
        static readonly double yMultiple = (double)resolution.Height / NATIVE_RESOLUTION.Item2;


        static readonly (int, int)[] enemyPositions =
        {
            (908, 495), (942, 495), (978, 495), (1012, 495),
            (1047, 531), (1047, 565), (1047, 602), (1047, 636),
            (1012, 671), (978, 671), (942, 671), (907, 671),
            (871, 636), (871, 601), (871, 565), (871, 531)
        };
        static readonly int numEnemyPositions = enemyPositions.Length;

        static readonly (int, int)[] firePositions =
        {
            (761, 236), (892, 236), (1023, 236), (1155, 236),
            (1305, 382), (1305, 513), (1305, 645), (1305, 776),
            (1155, 926), (1023, 926), (892, 926), (761, 926),
            (615, 776), (615, 645), (615, 513), (615, 382)
        };

        // Change location x, y if resolution on device that is used to play on is not 1920x1080
        static readonly (int, int) penetratedTuple = ((int)Math.Round(963 * xMultiple),
                                                        (int)Math.Round(582 * yMultiple));
        //static readonly (int, int) penetratedTuple = ((int)Math.Round(1165 * xMultiple),
        //                                                (int)Math.Round(772 * yMultiple));

        // array of enemy locations
        long[] Enemies {  get; set; } = new long[numEnemyPositions];
        int[] NextFirePosition { get; set; } = new int[maxStoredMoves];

        int NextIndexAvailable { get; set; } = 0;

        int numLives = 3;
        public bool EndProgram { get; set; }
        public long TickCounter { get; set; }
        public bool Died { get; set; }

        internal void PlayGame()
        {
            // start the game
            SendKeyPress(VK_RETURN);
            Thread.Sleep(2000);
            SendKeyPress(VK_RETURN);
            Thread.Sleep(2000);

            while (numLives > 0 && !EndProgram)
            {
                StartNewLife();
                
                if (!EndProgram)
                {
                    // decrement our life counter
                    numLives--;
                    Console.WriteLine("Current lives " + numLives);
                    // sleep 3 seconds to let the death animation finish
                    Thread.Sleep(3000);
                }
            }
        }

        private void StartNewLife()
        {
            Console.WriteLine("Starting new life. " + numLives + " livesleft.");
            Died = false;
            // reset all the positioning data
            for (int i = 0; i < numEnemyPositions; i++)
            {
                Enemies[i] = NONE;
            }
            for (int i = 0; i < maxStoredMoves; i++)
            {
                NextFirePosition[i] = 0;
            }
            NextIndexAvailable = 0;

            // start thread to search for enemies
            Thread findEnemiesThread = new Thread(FindEnemies);
            findEnemiesThread.Start();

            MoveAndFire();

            // if we get here then we died or were asked to end, join the thread and return
            findEnemiesThread.Join();
        }

        private void MoveAndFire()
        {
            int currentPosition = 1;
            int positionOffset = 0;
            int currentIndex = 0;

            while (!EndProgram && !Died)
            {
                int firePos;

                lock (_firePositionLock)
                {
                    firePos = NextFirePosition[currentIndex];
                }
                //firePos = NextFirePosition[currentIndex];
                // check if we have anywhere to move and fire
                if (firePos != 0)
                {
                    // check if position was processed already
                    if (firePos != PROCESSED_POSITION)
                    {
                        positionOffset = CalculatePositionOffset(currentPosition, firePos);

                        if (positionOffset != 0)
                        {
                            // now move in the quickest direction
                            MoveWeapon(firePos, positionOffset);
                        } else {
                            // fire the weapon twice in case additional enemies appear here and
                            // one of the enemies moves faster than expected, thus eluding our detection
                            // algorithm
                            FireWeapon();
                        }

                        // we are at the correct position, fire our weapon
                        FireWeapon();

                        // update position
                        currentPosition = NextFirePosition[currentIndex];

                        // now check if there are anymore enemies at this same spot before we move
                        for (int i = 0; i < maxStoredMoves; i++)
                        {
                            int pos;

                            lock (_firePositionLock)
                            {
                                pos = NextFirePosition[i];
                            }
                            //pos = NextFirePosition[i];

                            if (i == currentIndex && pos == currentPosition)
                            {
                                // fire the weapon again since we are already at this position
                                // fire the weapon twice if case additional enemies appear here and
                                // one of the enemies moves faster than expected, thus eluding our detection
                                // algorithm
                                FireWeapon();
                                lock (_firePositionLock)
                                {
                                    NextFirePosition[i] = PROCESSED_POSITION;
                                }
                            }
                        }
                    }

                    // reset position
                    lock (_firePositionLock)
                    {
                        NextFirePosition[currentIndex] = 0;
                    }

                    // set array index to next movement position
                    currentIndex += 1;
                    currentIndex %= maxStoredMoves;
                }
                else
                {
                    Thread.Sleep(20);
                }
            }
        }

        private int CalculatePositionOffset(int startPosition, int endPosition)
        {
            int positionOffset = startPosition - endPosition;

            // check if we need to reverse our direction
            int absPositionOffset = Math.Abs(positionOffset);
            if (absPositionOffset > (numEnemyPositions / 2))
            {
                if (positionOffset > 0)
                {
                    positionOffset = -1 * (numEnemyPositions - absPositionOffset);
                }
                else
                {
                    positionOffset = numEnemyPositions - absPositionOffset;
                }
            }

            return positionOffset;
        }

        private void FireWeapon()
        {
            SendKeyPress(VK_SPACE, FIRE_PRESS_TICKS);
        }

        private void MoveWeapon(int expectedPosition, int moveCount)
        {
            if (!EndProgram && !Died)
            {
                Console.WriteLine("Moving " + (-1 * moveCount) + " to position " + expectedPosition);
                int absMoveCount = Math.Abs(moveCount);
                for (int i = 0; (i < absMoveCount) && !EndProgram && !Died; i++)
                {
                    if (moveCount > 0)
                    {
                        // move counterclockwise until we are there
                        SendKeyPress(VK_A);
                    }
                    else
                    {
                        // move clockwise until we are there
                        SendKeyPress(VK_D);
                    }

                }


                // the moves don't always register correctly via remote play so let's make sure we are in the right position
                Thread.Sleep(250); // 200 DEFAULT 200 Change if needed
                if (GetColorAt((int)Math.Round(firePositions[expectedPosition - 1].Item1 * xMultiple),
                               (int)Math.Round(firePositions[expectedPosition - 1].Item2 * yMultiple)).R < 100)
                {
                    // we are not where we are supposed to be, find our current position
                    for (int i = 0; i < numEnemyPositions; i++ )
                    {
                        if (GetColorAt((int)Math.Round(firePositions[i].Item1 * xMultiple),
                                       (int)Math.Round(firePositions[i].Item2 * yMultiple)).R > 100)
                        {
                            Console.WriteLine("Position " + i + " incorrect, adjusting...");
                            // adjust our position
                            MoveWeapon(expectedPosition, CalculatePositionOffset(i + 1, expectedPosition));

                            break;
                        }
                    }
                }
            }
        }

        private Color GetColorAt(int x, int y)
        {
            IntPtr dc = GetDC(IntPtr.Zero);
            int rgb = (int)GetPixel(dc, x, y);
            ReleaseDC(IntPtr.Zero, dc);

            return Color.FromArgb((rgb >> 0) & 0xff, (rgb >> 8) & 0xff, (rgb >> 16) & 0xff);
        }

        private void FindEnemies()
        {
            long timeDiff = 0;
            long currentTime;
            Color pixelColor;

            // loop until we run out of lives or quit the program
            while (!EndProgram)
            {
                // first check if we died
                pixelColor = GetColorAt((int)Math.Round(penetratedTuple.Item1 * xMultiple),
                                        (int)Math.Round(penetratedTuple.Item2 * yMultiple));

                if ((pixelColor.R > 100) && (pixelColor.G > 100))
                {
                    Console.WriteLine("We died :(");
                    Died = true;
                    // end the thread so we can start our next life
                    break;
                }

                currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                for (int index = 0; index < numEnemyPositions; index++)
                {
                    pixelColor = GetColorAt((int)Math.Round(enemyPositions[index].Item1 * xMultiple),
                                            (int)Math.Round(enemyPositions[index].Item2 * yMultiple));
                    if (pixelColor.G > 100)
                    {
                        if (Enemies[index] != NONE)
                        {
                            timeDiff = currentTime - Enemies[index];
                        }
                        // enemies takes typically between 1000-1200 miliseconds to move after they spawn
                        // hovever, a third enemy at the same position can move after ~700ms!
                        if (Enemies[index] == NONE || timeDiff > 1200)
                        {
                            Enemies[index] = currentTime;
                            Console.WriteLine("Found enemy at position " + (index + 1));

                            // set the fire position in our array
                            // LOCK ADDED
                            lock (_firePositionLock)
                            {
                                NextFirePosition[NextIndexAvailable] = index + 1;
                                NextIndexAvailable++;
                                NextIndexAvailable %= maxStoredMoves;
                            }
                        }
                    } else if (pixelColor.G < 100 && Enemies[index] != NONE)
                    {
                        Enemies[index] = NONE;
                    }
                }
                Thread.Sleep(20);
            }
        }
    }
}
