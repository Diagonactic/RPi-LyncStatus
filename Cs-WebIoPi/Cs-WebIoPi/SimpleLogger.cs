using System;
using System.Diagnostics;

namespace CsWebIopi
{
    public static class SimpleLogger
    {
        public static void Log(string msg, bool? success = null)
        {
            if (success.HasValue)
                msg = msg + " " + (success.Value ? "Succeeded" : "Failed");

            Console.WriteLine(msg);
            Debug.WriteLine(msg);
        }
    }
}