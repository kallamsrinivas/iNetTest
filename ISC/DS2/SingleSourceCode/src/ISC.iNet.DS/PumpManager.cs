using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ISC.iNet.DS.DomainModel;

namespace ISC.iNet.DS
{
    public class PumpManager
    {
        static private PumpManager _instance;

        internal static PumpManager CreatePumpManager()
        {
            _instance = new PumpManager();

            return _instance;
        }

        public static PumpManager Instance { get { return _instance; } }

        public virtual int GetFlowRateTolerance()
        {
            return Pump.FLOWRATE_TOLERANCE;
        }

        public virtual int GetStandardFlowRate()
        {
            return Pump.StandardFlowRate;
        }

        public virtual int GetStandardFlowCounts()
        {
            return Pump.StandardFlowCounts;
        }

        public virtual int GetStandardStartVoltage()
        {
            return Pump.StandardStartVoltage;
        }

        public virtual int GetMaxFlowRate()
        {
            return Pump.MaxFlowRate;
        }

        public virtual int GetMinFlowRate()
        {
            return Pump.MinFlowRate;
        }

        public virtual int GetMaxVoltage()
        {
            return Pump.MaxVoltage;
        }

        public virtual int GetMinVoltage()
        {
            return Pump.MinVoltage;
        }

        public virtual bool IsBadPumpTubing()
        {
            return Pump.IsBadPumpTubing;
        }

        public virtual void OpenGasEndPoint(GasEndPoint endPoint)
        {
            Pump.OpenGasEndPoint(endPoint);
        }

        public virtual void CloseGasEndPoint(GasEndPoint endPoint)
        {
            Pump.CloseGasEndPoint(endPoint);
        }

        public virtual void OpenValve(int id, bool startPump)
        {
            Pump.OpenValve(id, startPump);
        }

        public virtual void CloseValve(int id)
        {
            Pump.CloseValve(id);
        }

        public virtual void CloseValve(int id, bool stopPump)
        {
            Pump.CloseValve(id, stopPump);
        }

        public virtual void CloseAllValves(bool stopPump)
        {
            Pump.CloseAllValves(stopPump);
        }

        public virtual void RelieveInternalPressure()
        {
            Pump.RelieveInternalPressure();
        }

        public virtual DateTime GetTimePumpStarted()
        {
            return Pump.GetTimePumpStarted();
        }

        public virtual DateTime GetTimePumpLastStarted()
        {
            return Pump.GetTimePumpLastStarted();
        }

        public virtual int GetOpenValvePosition()
        {
            return Pump.GetOpenValvePosition();
        }

        public virtual void Start()
        {
            Pump.Start();
        }

        public virtual void Start(int voltage)
        {
            Pump.Start(voltage);
        }

        public virtual void Stop()
        {
            Pump.Stop();
        }

        public virtual int GetPumpErrorState()
        {
            return Pump.GetPumpErrorState();
        }

        public virtual int GetVacuumErrorState()
        {
            return Pump.GetVacuumErrorState();
        }

        public virtual bool IsRunning()
        {
            return Pump.IsRunning();
        }

        public virtual void SetNewPumpVoltage(byte pumpVoltage)
        {
            Pump.SetNewPumpVoltage(pumpVoltage);
        }

        public virtual ushort GetRawFlow()
        {
            return Pump.GetRawFlow();
        }

    }
}
