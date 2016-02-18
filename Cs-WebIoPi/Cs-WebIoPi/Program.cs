using System;

namespace CsWebIopi
{
    public class Program
    {
        private static Monitor s_lyncMonitor;
        public static int Main(string[] args)
        {
            Console.WriteLine("Skype for Business Presence Pi Broadcaster - Copyright (C) 2016 Matthew S. Dippel under the Apache 2.0 License");
            Console.WriteLine("Provided AS IS without warranty - see LICENSE for details\r\n");
            Console.WriteLine("Use at your OWN RISK - the code isn't great :( - see LICENSE for details\r\n");

            bool areParametersValid = false;

            // Verify that the arguments passed are valid
            if (args != null && args.Length == 4 || args.Length == 5)
            {
                ushort port = GetPort(args[1]);
                bool isTesting = args.Length == 5 && string.Equals(args[4], "--test", StringComparison.OrdinalIgnoreCase);

                if (IsValidIp(args[0]) && port != 0)
                    try
                    {
                        Console.WriteLine($"Using WebIOpi on {args[0]}:{port} using id {args[2]}");
                        s_lyncMonitor = new Monitor(args[0], port, args[2], args[3], isTesting);
                        areParametersValid = !isTesting;
                    }
                    catch (Exception e)
                    {
                        SimpleLogger.Log("An error occurred initializing the Lync Monitor:\r\n" + e);
                        if (isTesting)
                        {
                            SimpleLogger.Log("Press any key to continue . . .");
                            Console.ReadKey();
                        }
                    }
            }

            // Not valid, so we'll print a small help blurb and exit with 1
            if (!areParametersValid)
            {
                // Display Help and Exit
                Console.WriteLine("\r\nUsage: Cs-WebIopi <RaspberryPiIP> <Port> <WebIopiUserId> <WebIopiPassword> --test");
                Console.WriteLine("Omit \"--test\" to run the application.  When \"--test\" is used, the application will cycle through each light, allowing you to ensure your wiring is correct");
                s_lyncMonitor?.Dispose();
                return 1;
            }

            // Continually remind the operator that "q" is the exit key until they finally get around to tapping it
            ConsoleKeyInfo key;
            do
            {
                Console.WriteLine("\r\nMonitoring Lync Client - Press 'q' to exit");
                key = Console.ReadKey();
            } while (key.KeyChar != 'q' && key.KeyChar != 'Q');

            s_lyncMonitor?.Dispose();
            return 0;
        }

        private static ushort GetPort(string port)
        {
            ushort retVal;
            if (ushort.TryParse(port, out retVal))
                return retVal;
            return 0;
        }

        private static bool IsValidIp(string ipAddress)
        {
            var ip = ipAddress.Trim().Split('.');
            if (ip.Length != 4)
                return false;

            return IsValidIpByte(ip[0], 1) && IsValidIpByte(ip[1]) && IsValidIpByte(ip[2]) && IsValidIpByte(ip[3]);
        }

        private static bool IsValidIpByte(string ipPart, int min=0)
        {
            byte test;
            return byte.TryParse(ipPart, out test) && test < 255 && test >= min;
        }
    }
}