using System;
using Diagonactic;

namespace CsWebIopi
{
    /// <summary>
    /// Enum containing the possible lights that can be set. When a flag is set, that indicates that the LED
    /// corresponding to that status should be turned on
    /// </summary>
    [Flags]
    public enum GpioLights
    {
        None = 0,
        /// <summary>
        /// The "Available" LED (Green)
        /// </summary>
        Available = Enums.Flag.F1,
        /// <summary>
        /// The "Away" LED (Yellow)
        /// </summary>
        Away = Enums.Flag.F2,
        /// <summary>
        /// The "Busy" LED (Red)
        /// </summary>
        Busy = Enums.Flag.F3
    }
}