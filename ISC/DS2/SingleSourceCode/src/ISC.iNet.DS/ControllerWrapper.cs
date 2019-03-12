using ISC.iNet.DS.DomainModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISC.iNet.DS
{
    public class ControllerWrapper
    {
        static private ControllerWrapper _instance;

        internal static ControllerWrapper CreateController()
        {
            _instance = new ControllerWrapper();

            return _instance;
        }

        public static ControllerWrapper Instance { get { return _instance; } }

        public virtual bool IsDocked()
        {
            return Controller.IsDocked();
        }

        public virtual bool IsPumpAdapterAttached()
        {
            return Controller.IsPumpAdapterAttached();
        }

        public virtual bool IsUsbDriveAttached(string logLabel)
        {
            return Controller.IsUsbDriveAttached(logLabel);
        }

        public virtual DockingStation GetDockingStation()
        {
            return Controller.GetDockingStation();
        }

        public virtual double Round(double availableConcentration, int precision)
        {
            return Controller.Round(availableConcentration, precision);
        }

        public virtual void TurnLEDsOff()
        {
            Controller.TurnLEDsOff();
        }

        public virtual KeyPress GetKeyPress()
        {
            return Controller.GetKeyPress();
        }

        public virtual void TurnLEDOn(Controller.LEDState state)
        {
            Controller.TurnLEDOn(state);
        }

        public virtual void Buzz(double seconds)
        {
            Controller.Buzz(seconds);
        }

        public virtual void TurnBuzzerOff()
        {
            Controller.TurnBuzzerOff();
        }

        public virtual bool IsDiffusionLidDown()
        {
            return Controller.IsDiffusionLidDown();
        }

        public virtual void SetCradleSolenoid(AccessoryPumpSetting setting)
        {
            Controller.SetCradleSolenoid(setting);
        }

        public virtual string FirmwareVersion => Controller.FirmwareVersion;

    }
}
