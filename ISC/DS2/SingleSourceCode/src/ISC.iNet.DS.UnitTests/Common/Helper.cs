using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ISC.iNet.DS.UnitTests
{
    public class Helper
    {
        /// <summary>
        /// Gives default implementation of bump test action with docking station, instrument with sensors, and gas end points
        /// Changes properties as you need for your testing
        /// </summary>
        internal static InstrumentBumpTestAction GetBumpTestAction(DeviceType deviceType, List<string> gasCodes = null, DeviceSubType subType = DeviceSubType.None)
        {
            //It is not expected that user would pass gas codes.  If not passed in, create empty list.
            if (gasCodes == null)
                gasCodes = new List<string>();

            InstrumentBumpTestAction action = new InstrumentBumpTestAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);
            action.Instrument.InstalledComponents.AddRange(GetSensorsForTest(gasCodes));
            action.GasEndPoints = GetGasEndPointsForTest(action.Instrument.InstalledComponents);
            return action;
        }

        /// <summary>
        /// Gives default implementation of bump test action with docking station, instrument with sensors, and gas end points
        /// Changes properties as you need for your testing
        /// </summary>
        internal static InstrumentDiagnosticAction GetDiagnosticAction(DeviceType deviceType, DeviceSubType subType = DeviceSubType.None)
        {     
            InstrumentDiagnosticAction action = new InstrumentDiagnosticAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);
            return action;
        }

        internal static InstrumentManualOperationsClearAction GetManualOperationsClearAction(DeviceType deviceType, DeviceSubType subType = DeviceSubType.None)
        {
            InstrumentManualOperationsClearAction action = new InstrumentManualOperationsClearAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);

            return action;
        }

        internal static InstrumentDatalogClearAction GetDatalogClearAction(DeviceType deviceType, DeviceSubType subType = DeviceSubType.None)
        {
            InstrumentDatalogClearAction action = new InstrumentDatalogClearAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);

            return action;
        }

        internal static InstrumentAlarmEventsClearAction GetAlarmEventsClearAction(DeviceType deviceType, DeviceSubType subType = DeviceSubType.None)
        {
            InstrumentAlarmEventsClearAction action = new InstrumentAlarmEventsClearAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);

            return action;
        }

        internal static InstrumentDatalogDownloadAction GetDatalogDownloadAction(DeviceType deviceType, DeviceSubType subType = DeviceSubType.None)
        {
            InstrumentDatalogDownloadAction action = new InstrumentDatalogDownloadAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);
            return action;
        }

        internal static InstrumentManualOperationsDownloadAction GetManualOperationsDownloadAction(DeviceType deviceType, DeviceSubType subType = DeviceSubType.None)
        {
            InstrumentManualOperationsDownloadAction action = new InstrumentManualOperationsDownloadAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);
            return action;
        }

        /// <summary>
        /// Gives default implementation of calibration action with docking station, instrument with sensors, and gas end points
        /// Changes properties as you need for your testing
        /// </summary>
        internal static InstrumentCalibrationAction GetCalibrationAction(DeviceType deviceType, List<string> gasCodes = null, DeviceSubType subType = DeviceSubType.None)
        {
            //It is not expected that user would pass gas codes.  If not passed in, create empty list.
            if (gasCodes == null)
                gasCodes = new List<string>();

            InstrumentCalibrationAction action = new InstrumentCalibrationAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);
            action.Instrument.InstalledComponents.AddRange(GetSensorsForTest(gasCodes));
            action.GasEndPoints = GetGasEndPointsForTest(action.Instrument.InstalledComponents);
            return action;
        }

        internal static InstrumentAlarmEventsDownloadAction GetAlarmEventDownloadAction(DeviceType deviceType, DeviceSubType subType = DeviceSubType.None)
        {
            InstrumentAlarmEventsDownloadAction action = new InstrumentAlarmEventsDownloadAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            action.Instrument = GetInstrumentForTest(deviceType, subType);
                        
            return action;

        }

        internal static InteractiveDiagnosticAction GetInteractiveDiagnosticAction(DeviceType deviceType)
        {
            InteractiveDiagnosticAction action = new InteractiveDiagnosticAction();

            action.DockingStation = GetDockingStationForTest(deviceType);
            
            return action;
        }

        /// <summary>
        /// Create gas endpoints for testing, port 1 contains fresh air and port 2 contains default 4-gas cylinder
        /// Change gas endpoints as needed for your testing if default configuration is not what you wanted.
        /// </summary>
        /// <param name="components"></param>
        /// <returns></returns>
        internal static List<GasEndPoint> GetGasEndPointsForTest(List<InstalledComponent> components)
        {
            List<GasEndPoint> gasEndPoints = new List<GasEndPoint>();

            //Port 1 is fresh air
            gasEndPoints.Add(GasEndPoint.CreateFreshAir(1));

            //Port 2, add 4-gas cylinder
            Cylinder cyl = new Cylinder("1810-9155", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.CO.ToString()], 100.00),
                                                                   new GasConcentration(GasType.Cache[GasCode.H2S.ToString()], 25.00),
                                                                   new GasConcentration(GasType.Cache[GasCode.Pentane.ToString()], 3521.10),
                                                                   new GasConcentration(GasType.Cache[GasCode.O2.ToString()], 180000.00) });

            gasEndPoints.Add(new GasEndPoint(cyl, 2, GasEndPoint.Type.Manual));

            return gasEndPoints;
        }

        /// <summary>
        /// Create needed sensors based on list of gas codes tester requested.
        /// </summary>
        internal static List<InstalledComponent> GetSensorsForTest(List<string> gasCodes)
        {
            List<InstalledComponent> sensors = new List<InstalledComponent>();

            int position = 0;
            foreach (string gasCode in gasCodes)
            {
                position++;

                InstalledComponent component = new InstalledComponent
                {
                    Component = new Sensor(String.Format("TESTSEN-{0}", position.ToString("D3"))),
                    Position = position
                };

                //Set typical properties and let caller change as needed
                Sensor sensor = component.Component as Sensor;
                sensor.Type = new SensorType(gasCode.Replace("G", "S"));
                sensor.Enabled = true;
                sensor.BumpTestStatus = true;
                sensor.CalibrationStatus = Status.Passed;
                sensor.CalibrationTimeout = 10;

                // POPULATED CAL ORDER AND BUMP ODER FROM FS 
                switch (sensor.Type.Code)
                {
                    case SensorCode.CO:
                        sensor.CalibrationGas = new GasType(GasCode.CO, 14, 13, 0.0, "CO", true, "Carbon Monoxide");
                        sensor.CalibrationGasConcentration = 100.0;
                        sensor.Alarm.Low = 35;
                        sensor.Alarm.High = 70;
                        sensor.Alarm.TWA = 35;
                        sensor.Alarm.STEL = 200;
                        ((SensorType)component.Component.Type).MeasurementType = MeasurementType.PPM;
                        break;

                    case SensorCode.H2S:
                        sensor.CalibrationGas = new GasType(GasCode.H2S, 13, 12, 0.0, "H2S", true, "HYDROGEN SULPHIDE");
                        sensor.CalibrationGasConcentration = 25.0;
                        sensor.Alarm.Low = 10;
                        sensor.Alarm.High = 20;
                        sensor.Alarm.TWA = 10;
                        sensor.Alarm.STEL = 15;
                        ((SensorType)component.Component.Type).MeasurementType = MeasurementType.PPM;
                        break;

                    case SensorCode.O2:
                        sensor.CalibrationGas = new GasType(GasCode.O2, 1, 23, 0.0, "O2", true, "OXYGEN");
                        sensor.CalibrationGasConcentration = 18;
                        sensor.Alarm.Low = 19.5;
                        sensor.Alarm.High = 23.5;
                        ((SensorType)component.Component.Type).MeasurementType = MeasurementType.VOL;
                        break;

                    case SensorCode.CombustibleLEL:
                        sensor.CalibrationGas = new GasType(GasCode.Pentane, 18, 17, 0.0071, "LEL", true, "COMBUSTIBLE LEL");
                        sensor.CalibrationGasConcentration = 25;
                        sensor.Alarm.Low = 10;
                        sensor.Alarm.High = 20;
                        ((SensorType)component.Component.Type).MeasurementType = MeasurementType.LEL;
                        break;

                    case SensorCode.ClO2:
                        sensor.CalibrationGas = new GasType(GasCode.ClO2, 2, 1, 0.0, "CLO2", true, "CHLORINE DIOXIDE");
                        sensor.CalibrationGasConcentration = 1;
                        sensor.Alarm.Low = 0.1;
                        sensor.Alarm.High = 0.2;
                        sensor.Alarm.TWA = 0.1;
                        sensor.Alarm.STEL = 0.3;
                        ((SensorType)component.Component.Type).MeasurementType = MeasurementType.PPM;
                        break;

                    case SensorCode.CO2:
                        sensor.CalibrationGas = new GasType(GasCode.CO2, 60, 1, 0.0, "CO2", true, "CARBON DIOXIDE");
                        sensor.CalibrationGasConcentration = 1;
                        sensor.Alarm.Low = 0.5;
                        sensor.Alarm.High = 1;
                        sensor.Alarm.TWA = 0.5;
                        sensor.Alarm.STEL = 3;
                        ((SensorType)component.Component.Type).MeasurementType = MeasurementType.PPM;
                        break;

                    case SensorCode.HCl:
                        sensor.CalibrationGas = new GasType(GasCode.HCl, 3, 1, 0.0, "HCL", true, "HYDROGEN CHLORIDE");
                        sensor.CalibrationGasConcentration = 10;
                        sensor.Alarm.Low = 2.5;
                        sensor.Alarm.High = 5;
                        sensor.Alarm.TWA = 2.5;
                        sensor.Alarm.STEL = 2.5;
                        ((SensorType)component.Component.Type).MeasurementType = MeasurementType.PPM;
                        break;

                    case SensorCode.NH3:
                        sensor.CalibrationGas = new GasType(GasCode.NH3, 8, 1, 0.0, "NH3", true, "AMMONIA");
                        sensor.CalibrationGasConcentration = 50;
                        sensor.Alarm.Low = 25;
                        sensor.Alarm.High = 50;
                        sensor.Alarm.TWA = 25;
                        sensor.Alarm.STEL = 35;
                        ((SensorType)component.Component.Type).MeasurementType = MeasurementType.PPM;
                        break;

                    default:
                        sensor.CalibrationGas = GasType.Cache[gasCode.ToString()];
                        break;

                }

                sensors.Add(component);
            }

            return sensors;
        }

        /// <summary>
        /// Get an instrument object with default settings based on device type and/or sub type.
        /// </summary>
        internal static Instrument GetInstrumentForTest(DeviceType deviceType, DeviceSubType deviceSubType = DeviceSubType.None)
        {
            Instrument instrument = new Instrument("TSTINST-001");
            instrument.Type = deviceType;
            instrument.Subtype = deviceSubType;
            instrument.SoftwareVersion = "1.10.01";

            instrument.BumpTimeout = 10;
            return instrument;
        }

        /// <summary>
        /// Gets a docking station object with default settings based on device type given
        /// </summary>
        internal static DockingStation GetDockingStationForTest(DeviceType deviceType)
        {
            DockingStation dockingStation = new DockingStation();
            if (deviceType != DeviceType.VPRO)
                dockingStation.Type = deviceType;
            else
                dockingStation.Type = DeviceType.MX4;
            dockingStation.SerialNumber = "TESTDOC-001";
            dockingStation.Reservoir = false;
            dockingStation.PurgeAfterBump = false;

            return dockingStation;
        }

        internal static Master GetMasterForTest(DockingStation dockingStation = null, Instrument instrument = null, bool isCal = true)
        {
            Master master = Master.CreateMaster();

            master.SwitchService = MockHelper.GetSwitchServiceMock(instrument, isCal).Object;
            master.ConsoleService = MockHelper.GetConsoleServiceMock().Object;
            master.ExecuterService = MockHelper.GetExecuterServiceMock().Object;
            master.ControllerWrapper = MockHelper.GetControllerMock(dockingStation, instrument).Object;
            master.Scheduler = MockHelper.GetSchedulerMock().Object;
            master.PumpWrapper = MockHelper.GetPumpMock().Object;

            return master;
        }  
        
        internal static Schema GetSchemaForTest()
        {
            // Setup accCustomer Account for DSXi
            Schema sch = new Schema();
            sch.Activated = true;
            sch.AccountNum = "12345";
            sch.ServiceCode = "Exchange";

            return sch;
        }
    }
}
