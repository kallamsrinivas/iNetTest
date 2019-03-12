using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISC.iNet.DS
{
    public class LCDWrapper
    {
        static private LCDWrapper _instance;

        internal static LCDWrapper CreateLCD()
        {
            _instance = new LCDWrapper();

            return _instance;
        }

        public static LCDWrapper Instance { get { return _instance; } }

        public virtual void Backlight(bool backlight)
        {
            LCD.Backlight = backlight;
        }

        public virtual void Display(string text)
        {
            LCD.Display(text);
        }

        public virtual void BlackScreen()
        {
            LCD.BlackScreen();
        }
    }
}
