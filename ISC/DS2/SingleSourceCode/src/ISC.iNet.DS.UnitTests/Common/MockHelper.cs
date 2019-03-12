using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ISC.iNet.DS.Controller;

namespace ISC.iNet.DS.UnitTests
{
    public class MockHelper
    {
        internal static Mock<InstrumentController> GetInstrumentControllerMockForCal(Instrument instrument = null)
        {
            Mock<InstrumentController> instrumentController = new Mock<InstrumentController>();
            SensorPosition[] sensorPositions = null;

            if (instrument != null)
            {
                sensorPositions = instrument.InstalledComponents
                .Where(installedComponent => installedComponent.Component is Sensor)
                .Select(sensor => new SensorPosition(sensor.Position, SensorMode.Installed, false))
                .ToArray();

                instrumentController.Setup(x => x.AccessoryPump).Returns(instrument.AccessoryPump);
            }

            instrumentController.Setup(x => x.Initialize(It.IsAny<InstrumentController.Mode>()));
            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), It.IsAny<string>())).Returns(false);
            instrumentController.Setup(x => x.GetSensorBiasStatus()).Returns(true);
            instrumentController.Setup(x => x.EnablePump(It.IsAny<bool>()));
            instrumentController.Setup(x => x.GetSensorPositions()).Returns(sensorPositions);
            instrumentController.Setup(x => x.OpenGasEndPoint(It.IsAny<GasEndPoint>(), It.IsAny<int>()));
            instrumentController.Setup(x => x.CloseGasEndPoint(It.IsAny<GasEndPoint>()));            
            instrumentController.Setup(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>())).Returns(0);            
            instrumentController.Setup(x => x.GetSensorBumpStatus(It.IsAny<int>())).Returns(true);            
            instrumentController.Setup(x => x.PauseGasFlow(It.IsAny<GasEndPoint>(), It.IsAny<long>()));
            instrumentController.Setup(x => x.GetSensorMaximumReading(It.IsAny<int>(), It.IsAny<double>())).Returns(30);
            
            instrumentController.Setup(x => x.IsSensorEnabled(It.IsAny<int>())).Returns<int>(pos =>
            {
                InstalledComponent installedComponent = instrument.InstalledComponents.Find(component => component.Position == pos);
                if (installedComponent != null)
                    return installedComponent.Component.Enabled;
                return false;
            });
            instrumentController.Setup(x => x.GetSensorLowAlarm(It.IsAny<int>(), It.IsAny<double>())).Returns<int, double>((pos, resolution) =>
            {
                InstalledComponent installedComponent = instrument.InstalledComponents.Find(component => component.Position == pos);
                if (installedComponent != null)
                {
                    Sensor sensor = (Sensor)installedComponent.Component;
                    return sensor.Alarm.Low;
                }
                return 0;
            });

            #region [ Calibration Methods ]
            
            instrumentController.Setup(x => x.PreconditionSensor(It.IsAny<InstalledComponent>(), It.IsAny<GasEndPoint>(), It.IsAny<SensorGasResponse>())).Returns(new TimeSpan(0));
            instrumentController.Setup(x => x.GetSensorZeroingStatus(It.IsAny<int>())).Returns(true);
            instrumentController.Setup(x => x.IsSensorCalibrationEnabled(It.IsAny<InstalledComponent>())).Returns(true);
            instrumentController.Setup(x => x.GetSensorLastCalibrationTime(It.IsAny<int>())).Returns(DateTime.Now);
            instrumentController.Setup(x => x.ZeroSensors(It.IsAny<GasEndPoint>())).Returns(true);
            instrumentController.Setup(x => x.SetSensorCalGasConcentration(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>()));
            instrumentController.Setup(x => x.SetCalibrationGasConcentration(It.IsAny<InstalledComponent>(), It.IsAny<double>(), It.IsAny<bool>()));
            instrumentController.Setup(x => x.GetSensorCalibrationTimeout(It.IsAny<int>())).Returns(new TimeSpan(0,0,10));
            instrumentController.Setup(x => x.GetSensorBaseline(It.IsAny<int>())).Returns(0);
            instrumentController.Setup(x => x.GetSensorZeroOffset(It.IsAny<int>(), It.IsAny<double>())).Returns(0);
            instrumentController.Setup(x => x.GetSensorSpanCoeff(It.IsAny<int>())).Returns(49485);
            instrumentController.Setup(x => x.BeginSensorCalibration(It.IsAny<IEnumerable<int>>()));
            instrumentController.Setup(x => x.GetSensorCalibrationFlowRate(It.IsAny<InstalledComponent>())).Returns(500);
            instrumentController.Setup(x => x.GetSensorPreconditionFlowRate(It.IsAny<InstalledComponent>())).Returns(500);
            instrumentController.Setup(x => x.GetSensorPreconditionTimeout(It.IsAny<InstalledComponent>())).Returns(new TimeSpan(0, 0, 10));

            instrumentController.Setup(x => x.SetCalibrationGasConcentration(It.IsAny<InstalledComponent>(), It.IsAny<GasEndPoint>())).Returns<InstalledComponent, GasEndPoint>((installedComponent, gasEndPoint) =>
            {
                Sensor sensor = (Sensor)installedComponent.Component;
                GasConcentration gasConcentration = null;

                gasConcentration = gasEndPoint.Cylinder.GasConcentrations.Find(gas => gas.Type.Code == sensor.CalibrationGas.Code);
                if (gasConcentration != null)
                {
                    sensor.CalibrationGasConcentration = gasConcentration.Concentration;
                    return gasConcentration.Concentration;
                }

                gasConcentration = gasEndPoint.Cylinder.GasConcentrations.Find(gas => gas.Type.Code == GasCode.FreshAir && sensor.CalibrationGas.Code == GasCode.O2);
                if (gasConcentration != null)
                {
                    sensor.CalibrationGasConcentration = 209000;
                    return 209000;
                }

                return 0;
            });
            if (instrument != null && instrument.InstalledComponents.Where(installedComponent => installedComponent.Component is Sensor).Count() > 0)
            {
                int noOfReadingsNeeded = 3;
                Func<string, int, string> sensorID = (sn, position) => string.Join("_", sn, position);
                Func<Sensor, double> sensorCalibrationGasConcentration = s => ((SensorType)s.Type).MeasurementType != MeasurementType.VOL ? s.CalibrationGasConcentration : s.CalibrationGasConcentration * 10000;
                Dictionary<string, double> sensorReadings = createSensorReadingDictionary(instrument.InstalledComponents.Where(installedComponent => installedComponent.Component is Sensor));
                Dictionary<int, double> sensorReadingCounts = createSensorReadingCount(instrument.InstalledComponents.Where(installedComponent => installedComponent.Component is Sensor));
                Func<int, double> sensorSpanReserve = position =>
                {
                    double spanReserve = 0;
                    InstalledComponent installedComponent = instrument.InstalledComponents.Find(installComp => installComp.Position == position);
                    if (installedComponent != null)
                    {
                        Sensor sensor = (Sensor)installedComponent.Component;
                        double calGasConc = sensorCalibrationGasConcentration(sensor);
                        double sensorReading = sensorReadings[sensorID(sensor.SerialNumber, position)];
                        spanReserve = (sensorReading / calGasConc) * 100;
                    }
                    return spanReserve;
                };

                instrumentController.Setup(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>()))
                    .Returns<int, double>((position, resolution) =>
                    {
                        double reading = 0;
                        InstalledComponent installedComponent = instrument.InstalledComponents.Find(installComp => installComp.Position == position);
                        if (installedComponent != null)
                        {
                            reading = sensorReadings[sensorID(installedComponent.Component.SerialNumber, position)];
                        }
                        return reading;
                    })
                    .Callback<int, double>((position, resolution) =>
                    {
                        InstalledComponent installedComponent = instrument.InstalledComponents.Find(installComp => installComp.Position == position);
                        if (installedComponent != null)
                        {
                            Sensor sensor = (Sensor)installedComponent.Component;
                            double calGasConc = sensorCalibrationGasConcentration(sensor);
                            double increment = calGasConc / noOfReadingsNeeded;
                            double sensorReading = sensorReadings[sensorID(sensor.SerialNumber, position)];
                            if (sensorReading < (calGasConc + increment))
                                sensorReadings[sensorID(sensor.SerialNumber, position)] += increment;
                        }
                    });

                instrumentController.Setup(x => x.GetSensorCalibrationStatus(It.IsAny<int>()))
                    .Returns<int>(position => sensorSpanReserve(position) > 80);

                instrumentController.Setup(x => x.IsSensorCalibrating(It.IsAny<int>()))
                    .Returns<int>(position => sensorReadingCounts[position] <= noOfReadingsNeeded)
                    .Callback<int>(position => sensorReadingCounts[position] += 1);

                instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns<int>(pos => sensorSpanReserve(pos));
                instrumentController.Setup(x => x.GetSensorSpanCoeff(It.IsAny<int>())).Returns(1);

                instrumentController.Setup(x => x.GetSensorLastCalibrationTime(It.IsAny<int>()))
                    .Returns<int>(position => sensorReadingCounts[position] > 0 ? DateTime.Now : DateTime.Now.AddMinutes(-5))
                    .Callback<int>(position =>
                    {
                        if (sensorReadingCounts[position] > 1)
                            sensorReadingCounts[position] = 0;
                    });
            }

            #endregion

            return instrumentController;
        }

        internal static Mock<InstrumentController> GetInstrumentControllerMock()
        {
            Mock<InstrumentController> instrumentController = new Mock<InstrumentController>();
            instrumentController.Setup(x => x.Initialize(It.IsAny<InstrumentController.Mode>()));
            instrumentController.Setup(x => x.EnablePump(false));

            return instrumentController;
        }

        internal static Mock<SmartCardWrapper> GetSmarcardWrapper()
        {
            Mock<SmartCardWrapper> smartCardMock = new Mock<SmartCardWrapper>();

            smartCardMock.Setup(x => x.IsCardPresent(It.IsAny<int>())).Returns(true);
            smartCardMock.Setup(x => x.IsPressureSwitchPresent(It.IsAny<int>())).Returns(true);
            smartCardMock.Setup(x => x.CheckPressureSwitch(It.IsAny<int>())).Returns(true);

            return smartCardMock;
        }

        internal static Mock<LCDWrapper> GetLCDMock()
        {
            Mock<LCDWrapper> lcdMock = new Mock<LCDWrapper>();
            lcdMock.Setup(x => x.Backlight(true));

            return lcdMock;
        }

        internal static Mock<InstrumentController> GetInstrumentControllerMockForDiag(InstrumentDiagnosticAction diagAction)
        {
            Mock<InstrumentController> instrumentController = new Mock<InstrumentController>();
            instrumentController.Setup(x => x.Initialize(It.IsAny<InstrumentController.Mode>()));          
            instrumentController.Setup(x => x.EnablePump(false));
            instrumentController.Setup(x => x.GetGeneralDiagnosticProperties()).Returns(new GeneralDiagnosticProperty[] { new GeneralDiagnosticProperty("PUMP", "FINE" )});
            instrumentController.Setup(x => x.GetInstrumentErrors()).Returns(new ErrorDiagnostic[] { new ErrorDiagnostic(3850, DateTime.UtcNow, ErrorCategory.Instrument, string.Empty) });
            instrumentController.Setup(x => x.ClearInstrumentErrors());

            return instrumentController;
        }

        internal static Mock<ISwitchService> GetSwitchServiceMock(Instrument instrument = null, bool isCal = true, InstrumentController instrumentControllerForBump = null)
        {
            Mock<ISwitchService> switchService = new Mock<ISwitchService>();
            InstrumentController instrumentController = isCal ? GetInstrumentControllerMockForCal(instrument).Object
                : instrumentControllerForBump;

            switchService.SetupAllProperties();

            switchService.Setup(x => x.InitialReadSettingsNeeded).Returns(false);
            switchService.Setup(x => x.IsInstrumentInSystemAlarm).Returns(false);
            switchService.Setup(x => x.Instrument).Returns(instrument);
            switchService.Setup(x => x.InstrumentController).Returns(instrumentController);
            switchService.Setup(x => x.IsDocked()).Returns(instrument != null);
            return switchService;
        }

        internal static Mock<InstrumentController> GetInstrumentControllerMockForBump(InstrumentBumpTestAction action)
        {
            Mock<InstrumentController> instrumentController = new Mock<InstrumentController>();
            instrumentController.Setup(x => x.Initialize(It.IsAny<InstrumentController.Mode>()));
            instrumentController.Setup(x => x.GetSensorMaximumReading(It.IsAny<int>(), It.IsAny<double>())).Returns(30); // Can be any valid number
            instrumentController.Setup(x => x.GetSensorBumpFlowRate(It.IsAny<InstalledComponent>())).Returns(Pump.StandardFlowRate);
            instrumentController.Setup(x => x.GetSensorBiasStatus()).Returns(true);
            instrumentController.Setup(x => x.BeginInstrumentBump());
            instrumentController.Setup(x => x.BeginSensorBump(It.IsAny<int>()));
            instrumentController.Setup(x => x.PauseGasFlow(It.IsAny<GasEndPoint>(), It.IsAny<long>()));
            instrumentController.Setup(x => x.EndInstrumentBump());
            instrumentController.Setup(x => x.SetSensorBumpFault(It.IsAny<int>(), It.IsAny<bool>()));
            instrumentController.Setup(x => x.EnablePump(false));
            instrumentController.Setup(x => x.OpenGasEndPoint(action.GasEndPoints[1], 500));
            instrumentController.Setup(x => x.GetSensorLowAlarm(It.IsAny<int>(), It.IsAny<double>())).Returns(0.2); // To Pass pre bump Purge
            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>())).Returns(0);
            instrumentController.Setup(x => x.GetSensorSpanCoeff(It.IsAny<int>())).Returns(12.5);
            return instrumentController;
        }

        internal static Mock<IConsoleService> GetConsoleServiceMock()
        {
            Mock<IConsoleService> consoleService = new Mock<IConsoleService>();

            consoleService.Setup(x => x.UpdateState(It.IsAny<ConsoleState>(), It.IsAny<string[]>()));
            consoleService.Setup(x => x.GetSensorLabel(It.IsAny<string>())).Returns<string>(code =>
            {
                code = code.Replace('S', 'G');
                if (!GasType.Cache.ContainsKey(code))
                    return string.Empty;

                return GasType.Cache[code].Symbol;
            });
            return consoleService;
        }

        internal static Mock<IExecuterService> GetExecuterServiceMock()
        {
            Mock<IExecuterService> executerService = new Mock<IExecuterService>();

            executerService.Setup(x => x.HeartBeat());
            return executerService;
        }

        internal static Mock<ControllerWrapper> GetControllerMock(DockingStation dockingStation, Instrument instrument)
        {
            Mock<ControllerWrapper> controllerWrapper = new Mock<ControllerWrapper>();

            controllerWrapper.Setup(x => x.GetDockingStation()).Returns(dockingStation);
            controllerWrapper.Setup(x => x.IsDocked()).Returns(instrument != null);
            controllerWrapper.Setup(x => x.IsUsbDriveAttached(It.IsAny<string>())).Returns(true);
            controllerWrapper.Setup(x => x.Round(It.IsAny<double>(), It.IsAny<int>())).Returns<double, int>((value, resolution) => Math.Round(value, resolution));
            controllerWrapper.Setup(x => x.GetKeyPress()).Returns(new KeyPress(Key.Left, new TimeSpan(0 , 0, 5)));

            return controllerWrapper;
        }

        internal static Mock<Scheduler> GetSchedulerMock()
        {
            Mock<Scheduler> scheduler = new Mock<Scheduler>();

            scheduler.Setup(x => x.GetNextGasOperationDate(It.IsAny<InstrumentCalibrationEvent>())).Returns(DateTime.Now.AddMonths(1));
            scheduler.Setup(x => x.GetNextGasOperationDate(It.IsAny<InstrumentBumpTestEvent>())).Returns(DateTime.Now.AddDays(1));

            return scheduler;
        }

        internal static Mock<PumpManager> GetPumpMock()
        {
            Mock<PumpManager> pump = new Mock<PumpManager>();

            pump.Setup(x => x.IsBadPumpTubing()).Returns(false);
            pump.Setup(x => x.GetOpenValvePosition()).Returns(1);
            pump.Setup(x => x.CloseGasEndPoint(It.IsAny<GasEndPoint>()));

            return pump;
        }

        internal static Mock<Schema> GetSchemaMock()
        {
            Mock<Schema> schema = new Mock<Schema>();

            schema.Setup(x => x.Synchronized).Returns(true);

            return schema;
        }

        internal static Mock<CriticalErrorDataAccess> GetDiagCriticalErrorsMock()
        {
            Mock<CriticalErrorDataAccess> criticalInstrumentDiagErrors = new Mock<CriticalErrorDataAccess>();

            criticalInstrumentDiagErrors.Setup(x => x.FindAll()).Returns(new List<CriticalError>() { new CriticalError(3850, "MX6 pump fault")});

            return criticalInstrumentDiagErrors;
        }

        private static Dictionary<string, double> createSensorReadingDictionary(IEnumerable<InstalledComponent> installedComponents)
        {
            Dictionary<string, double> sensorReadingsDictionary = new Dictionary<string, double>();
            foreach (InstalledComponent installedComponent in installedComponents)
            {
                sensorReadingsDictionary.Add(string.Join("_", installedComponent.Component.SerialNumber, installedComponent.Position), 0);
            }
            return sensorReadingsDictionary;
        }

        private static Dictionary<int, double> createSensorReadingCount(IEnumerable<InstalledComponent> installedComponents)
        {
            Dictionary<int, double> sensorReadingsDictionary = new Dictionary<int, double>();
            foreach (InstalledComponent installedComponent in installedComponents)
            {
                sensorReadingsDictionary.Add(installedComponent.Position, 0);
            }
            return sensorReadingsDictionary;
        }
    }
}
