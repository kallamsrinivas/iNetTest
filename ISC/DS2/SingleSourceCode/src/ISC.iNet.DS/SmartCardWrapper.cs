using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISC.iNet.DS
{
    public class SmartCardWrapper
    {

        static private SmartCardWrapper _instance;

        internal static SmartCardWrapper CreateLCD()
        {
            _instance = new SmartCardWrapper();

            return _instance;
        }

        public static SmartCardWrapper Instance { get { return _instance; } }

        public virtual bool IsCardPresent(int position)
        {
            return SmartCardManager.IsCardPresent(position);
        }

        public virtual bool IsPressureSwitchPresent(int position)
        {
            return SmartCardManager.IsPressureSwitchPresent(position);
        }

        public virtual bool CheckPressureSwitch(int position)
        {
            return SmartCardManager.CheckPressureSwitch(position);
        }
    }
}
