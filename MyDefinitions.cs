using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynamic_Lighting_Key_Indicator
{
    internal class MyDefinitions
    {
        internal enum StateColorApply
        {
            On,
            Off,
            Both
        }

        // TODO: Implement this instead of using strings
        internal enum ColorProperty
        {
            NumLockOn,
            NumLockOff,
            CapsLockOn,
            CapsLockOff,
            ScrollLockOn,
            ScrollLockOff,
            DefaultColor
        }
    }
}
