using System;
using System.Collections.Generic;
using ISC.iNet.DS.DomainModel;

namespace ISC.iNet.DS.Services
{
    public interface IExecuterService : IService
    {
        bool DeadBatteryCharging { get; set; }
        bool DeadBatteryChargingPeriodDone { get; set; }
        Dictionary<string, string> ReplacedEquipment { get; set; }

        DockingStationEvent ExecuteNow(DockingStationAction action, bool report);
        void HeartBeat();
        void ReportDiscoveredInstrumentErrors(InstrumentEvent instrumentEvent);
        void ReportExceptionError(Exception ex);
        DockingStationEvent Discover();
    }
}