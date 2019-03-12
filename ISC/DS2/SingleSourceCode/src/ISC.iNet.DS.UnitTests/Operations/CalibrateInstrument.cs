using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class CalibrateInstrument
    {
        #region [ Fields ]

        InstrumentCalibrationAction action = null;
        InstrumentCalibrationOperation operation = null;
        InstrumentCalibrationEvent calibrationEvent = null;
        Master master = null;

        Mock<ControllerWrapper> controllerWrapper = null;
        Mock<IConsoleService> consoleSerivce = null;
        Mock<PumpManager> pumpWrapper = null;
        Mock<Scheduler> scheduler = null;
        Mock<ISwitchService> switchService = null;
        Mock<InstrumentController> instrumentController = null;

        #endregion

        #region [ Private Methods ]

        private void InitializeMocks(InstrumentCalibrationAction action)
        {
            controllerWrapper = MockHelper.GetControllerMock(action.DockingStation, action.Instrument);
            consoleSerivce = MockHelper.GetConsoleServiceMock();
            pumpWrapper = MockHelper.GetPumpMock();
            scheduler = MockHelper.GetSchedulerMock();
            switchService = MockHelper.GetSwitchServiceMock(action.Instrument);
            instrumentController = MockHelper.GetInstrumentControllerMockForCal(action.Instrument);
        }

        private void CreateMasterForTest()
        {
            master = Master.CreateMaster();

            switchService.Setup(x => x.InstrumentController).Returns(instrumentController.Object);

            master.ControllerWrapper = controllerWrapper.Object;
            master.ConsoleService = consoleSerivce.Object;
            master.PumpWrapper = pumpWrapper.Object;
            master.Scheduler = scheduler.Object;
            master.SwitchService = switchService.Object;
        }

        Func<SensorGasResponse, string, bool> testForInstrumentReset = (response, msg) =>
        {
            response.Status = Status.Failed;
            return true;
        };

        #endregion

        #region [ Test Methods ]

        //TODO: This test should test VPRO instrument, and also check to see time elapsed before throwing an exception if not covered by test configuration.
        [Fact]
        public void ThrowInstrumentNotReadyException()
        {
            // arrange
            action = Helper.GetCalibrationAction(DeviceType.MX6);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorBiasStatus()).Returns(false);

            Configuration.DockingStation = action.DockingStation;

            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<InstrumentNotReadyException>(() => operation.Execute());
        }

        [Fact]
        public void ThrowFailedCalibrationExceptionDueToUnableToZeroInstrumentSensors()
        {
            // arrange
            action = Helper.GetCalibrationAction(DeviceType.MX6);
            InitializeMocks(action);

            instrumentController.Setup(x => x.ZeroSensors(It.IsAny<GasEndPoint>())).Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert

            FailedCalibrationException exception = Xunit.Assert.Throws<FailedCalibrationException>(() => operation.Execute());
            Xunit.Assert.True(exception.InnerException is UnableToZeroInstrumentSensorsException);
        }

        [Fact]
        public void ThrowSensorModeErrorException()
        {
            // arrange
            List<string> sensorList = new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2 };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            SensorPosition[] sensorPositions = action.Instrument.InstalledComponents
                .Where(installedComponent => installedComponent.Component is Sensor)
                .Select(sensor => new SensorPosition(sensor.Position, SensorMode.Error, false))
                .ToArray();

            instrumentController.Setup(x => x.GetSensorPositions()).Returns(sensorPositions);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<SensorErrorModeException>(() => operation.Execute());
        }

        [Fact]
        public void ThrowsCorrectCalibrationGasUnavailableException()
        {
            // arrange
            List<string> sensorList = new List<string>() { GasCode.O2 };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            action.DockingStation.GasEndPoints.Clear();
            action.GasEndPoints.Clear();
            InitializeMocks(action);

            foreach (InstalledComponent installedComponent in action.Instrument.InstalledComponents
                .Where(installedComponent => installedComponent.Component is Sensor))
            {
                Sensor sensor = installedComponent.Component as Sensor;
                if (sensor.Type.Code == SensorCode.O2)
                    sensor.CalibrationGasConcentration = 20.9;
            }

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<CorrectCalibrationGasUnavailable>(() => operation.Execute());
        }

        [Fact]
        public void SkipCalibrationIfNoSensorsArePresent()
        {
            // arrange
            action = Helper.GetCalibrationAction(DeviceType.MX6);

            Battery battery = new Battery("TESTBAT-001");
            battery.Type = new ComponentType(BatteryCode.MX6Alkaline);
            InstalledComponent installedComponent = new InstalledComponent();
            installedComponent.Position = 1;
            installedComponent.Component = battery;
            action.Instrument.InstalledComponents.Add(installedComponent);

            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == 0);
        }

        [Fact]
        public void SkipCalibrationIfNoSensorsAreEnabled()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            action.Instrument.InstalledComponents.ForEach(sensor => sensor.Component.Enabled = false);
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == 0);
        }

        [Fact]
        public void SkipCalibrationIfInstrumentReset()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating instrument, start"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating instrument, start"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }
        [Fact]
        public void SkipCalibrationIfInstrumentResetAfterTurningOnSensor()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating instrument, after turning on sensors"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating instrument, after turning on sensors"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void SkipCalibrationIfInstrumentResetAfterClearingGases()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating instrument, after clearing gases"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating instrument, after clearing gases"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void SkipCalibrationIfInstrumentResetAfterZeroing()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating instrument, after zeroing"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating instrument, after zeroing"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ThrowsCorrectCalibrationGasUnavailableExceptionIfZeroAirIsNotAvaiable()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO2 };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<CorrectCalibrationGasUnavailable>(() => operation.Execute());
        }

        [Fact]
        public void ReturnSensorCalibrationFailureDueToZeroFailure()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorZeroingStatus(It.IsAny<int>())).Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act 
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.GetSensorZeroingStatus(It.IsAny<int>()));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.ZeroFailed);
        }

        [Fact]
        public void ReturnSensorCalibrationZeroPassedIfSensorNotCalibrationEnabled()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.ClO2 };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.IsSensorCalibrationEnabled(It.IsAny<InstalledComponent>())).Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act 
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.IsSensorCalibrationEnabled(It.IsAny<InstalledComponent>()));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.ZeroPassed);
        }

        [Fact]
        public void SkipCalibrationIfInstrumentResetAfterCheckingZeroStatus()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, checked zeroing status"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, checked zeroing status"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void SkipCalibrationIfInstrumentResetAfterSettingCalibrationTimeout()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, getting calibration timeout"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, getting calibration timeout"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ThrowInstrumentNotDockedExceptionAfterSensorPreconditioning()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            controllerWrapper.SetupSequence(x => x.IsDocked())
                .Returns(true)
                .Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert            
            Xunit.Assert.Throws<InstrumentNotDockedException>(() => operation.Execute());
            instrumentController.Verify(x => x.PreconditionSensor(It.IsAny<InstalledComponent>(), It.IsAny<GasEndPoint>(), It.IsAny<SensorGasResponse>()));
        }

        [Fact]
        public void SkipCalibrationIfInstrumentResetAfterSensorPreconditioning()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, sensor preconditioned"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, sensor preconditioned"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void SkipCalibrationIfInstrumentResetAfterGasFlowPaused()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, gas flow paused"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, gas flow paused"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ReturnCalibrationFailureIfCalibrationTimedOut()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>())).Returns(0);
            instrumentController.Setup(x => x.IsSensorCalibrating(It.IsAny<int>())).Returns(true);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            // Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ThrowInstrumentNotDockedExceptionDuringCalibration()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            controllerWrapper.SetupSequence(x => x.IsDocked())
                .Returns(true)
                .Returns(true)
                .Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert            
            Xunit.Assert.Throws<InstrumentNotDockedException>(() => operation.Execute());
            instrumentController.Verify(x => x.PreconditionSensor(It.IsAny<InstalledComponent>(), It.IsAny<GasEndPoint>(), It.IsAny<SensorGasResponse>()));
        }

        [Fact]
        public void ThrowFlowFailedExceptionIfBadPumpTubingIsDetected()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            pumpWrapper.Setup(x => x.IsBadPumpTubing()).Returns(true);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert            
            Xunit.Assert.Throws<FlowFailedException>(() => operation.Execute());
            Xunit.Assert.True(Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal);
        }

        [Fact]
        public void ReturnCalibrationFailureIfSensorSpanFailed()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.SetupSequence(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>()))
                .Returns(0)
                .Returns(20)
                .Returns(35)
                .Returns(55)
                .Returns(60);
            instrumentController.SetupSequence(x => x.IsSensorCalibrating(It.IsAny<int>()))
                .Returns(true)
                .Returns(true)
                .Returns(true)
                .Returns(true)
                .Returns(false);
            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(68);
            instrumentController.Setup(x => x.GetSensorCalibrationStatus(It.IsAny<int>())).Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.SpanFailed);
        }

        [Fact]
        public void SkipCalibrationIfInstrumentResetDuringCalibration()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, getting reading"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, getting reading"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ReturnSensorInstrumentAbortedIfCalibrationWasAbortedDueToInstrumentReset()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.SetupSequence(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>()))
                .Returns(0)
                .Returns(20)
                .Returns(35);
            instrumentController.SetupSequence(x => x.IsSensorCalibrating(It.IsAny<int>()))
                .Returns(true)
                .Returns(true)
                .Returns(null);
            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(68);
            instrumentController.Setup(x => x.GetSensorCalibrationStatus(It.IsAny<int>())).Returns(true);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.InstrumentAborted);

        }

        [Fact]
        public void SkipCalibrationIfInstrumentResetAfterRetrievingCalibrationStatus()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, retrieving calibration status"))
                .Returns(testForInstrumentReset);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.TestForInstrumentReset(It.IsAny<SensorGasResponse>(), "calibrating sensor, retrieving calibration status"));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ReturnSensorCalibrationFailureIfSpanReserveIsLessThanOrEqualToZero()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(0);
            instrumentController.Setup(x => x.GetSensorCalibrationStatus(It.IsAny<int>())).Returns(true);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ReturnSensorInstrumentAbortedIfCalibrationWasAbortedDuePreCalAndPostCalMismatch()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorCalibrationTimeout(It.IsAny<int>())).Returns(new TimeSpan(30));
            instrumentController.Setup(x => x.GetSensorLastCalibrationTime(It.IsAny<int>())).Returns(DateTime.Now);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.InstrumentAborted);
        }

        [Fact]
        public void ReturnSensorCalibratureFailureIfSensorFailedForRepairAccount()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            Mock<Schema> schema = new Mock<Schema>();

            schema.Setup(x => x.Activated).Returns(true);
            schema.Setup(x => x.AccountNum).Returns("12345");
            schema.Setup(x => x.ServiceCode).Returns("REPAIR");

            action.DockingStation.SpanReserveThreshold = 75;

            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(60);

            Configuration.Schema = schema.Object;
            Configuration.DockingStation = action.DockingStation;
            
            // act
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ThowFlowFailedExceptionIfNoValvesAreOpened()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            pumpWrapper.Setup(x => x.GetOpenValvePosition()).Returns(0);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<FlowFailedException>(() => operation.Execute());
            pumpWrapper.Verify(x => x.GetOpenValvePosition());
        }

        [Fact]
        public void ThrowCorrectCalibrationGasUnavailbleExceptionIfAppropriateGasIsNotFound()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.NH3 };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<CorrectCalibrationGasUnavailable>(() => operation.Execute());
        }

        [Fact]
        public void ReturnSensorCalibrationPassForWorkingSensors()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO, SensorCode.H2S, SensorCode.CombustibleLEL, SensorCode.O2, SensorCode.NH3 };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);

            Cylinder cyl = new Cylinder("1810-8241", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.NH3.ToString()], 150.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(gasResponse => gasResponse.Status == Status.Passed));
        }

        [Fact]
        public void ReturnSensorCalibrationFailureIfSensorAgeIsExpiredForServiceAccount()
        {
            List<string> sensorList = new List<string> { GasCode.CO, GasCode.H2S, GasCode.CombustibleLEL, GasCode.O2 };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            foreach (InstalledComponent installedComponent in action.Instrument.InstalledComponents
                                                            .Where(comp => comp.Component is Sensor))
                installedComponent.Component.SetupDate = DateTime.Now.AddMonths(-65);
            InitializeMocks(action);

            Mock<Schema> schema = new Mock<Schema>();

            schema.Setup(x => x.Activated).Returns(true);
            schema.Setup(x => x.AccountNum).Returns("12345");
            schema.Setup(x => x.ServiceCode).Returns("REPAIR");


            Configuration.Schema = schema.Object;
            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(gasResponse => gasResponse.Status == Status.Failed));

        }

        [Fact]
        public void ReturnSensorCalibrationFailureForSensorWithoutSetupDateIfSensorAgeIsExpiredForServiceAccount()
        {
            DateTime setupDate = DateTime.Now.AddMonths(-65);
            InstalledComponent component = new InstalledComponent
            {
                Component = new Sensor(string.Format("{0}{1}SEN-001", setupDate.ToString("yy"), setupDate.ToString("MM")))
                {
                    Type = new SensorType(SensorCode.CO),
                    Enabled = true,
                    CalibrationGas = new GasType(GasCode.CO, 14, 13, 0.0, "CO", true, "Carbon Monoxide"),
                    CalibrationGasConcentration = 100
                },
                Position = 1
            };

            action = Helper.GetCalibrationAction(DeviceType.MX6);
            action.Instrument.InstalledComponents.Add(component);
            InitializeMocks(action);

            Mock<Schema> schema = new Mock<Schema>();

            schema.Setup(x => x.Activated).Returns(true);
            schema.Setup(x => x.AccountNum).Returns("12345");
            schema.Setup(x => x.ServiceCode).Returns("REPAIR");


            Configuration.Schema = schema.Object;
            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == 1);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(gasResponse => gasResponse.Status == Status.Failed));

        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibrationFailureDueToZeroFailure()
        {
            // arrange
            List<string> sensorList = new List<string> { GasCode.CO, GasCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.TX1, sensorList);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorZeroingStatus(It.IsAny<int>())).Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act 
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.GetSensorZeroingStatus(It.IsAny<int>()));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(response => response.Status == Status.ZeroFailed));
        }

        [Fact(Skip = "CLO2 not supported in Calibration Parallel instruments")]
        public void CalibrationParallelReturnSensorCalibrationZeroPassedIfSensorNotCalibrationEnabled()
        {
            // arrange
            List<string> sensorList = new List<string> { GasCode.ClO2 };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            InitializeMocks(action);

            instrumentController.Setup(x => x.IsSensorCalibrationEnabled(It.IsAny<InstalledComponent>())).Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
           calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            instrumentController.Verify(x => x.IsSensorCalibrationEnabled(It.IsAny<InstalledComponent>()));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(response => response.Status == Status.ZeroPassed));
        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibrationFailureIfSensorAgeIsExpiredForServiceAccount()
        {
            List<string> sensorList = new List<string> { GasCode.CO, GasCode.H2S, GasCode.CombustibleLEL, GasCode.O2 };
            action = Helper.GetCalibrationAction(DeviceType.SC, sensorList);

            foreach (InstalledComponent installedComponent in action.Instrument.InstalledComponents.Where(comp => comp.Component is Sensor))
                installedComponent.Component.SetupDate = DateTime.Now.AddMonths(-65);
            InitializeMocks(action);

            Mock<Schema> schema = new Mock<Schema>();

            schema.Setup(x => x.Activated).Returns(true);
            schema.Setup(x => x.AccountNum).Returns("12345");
            schema.Setup(x => x.ServiceCode).Returns("REPAIR");

            Configuration.Schema = schema.Object;
            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(gasResponse => gasResponse.Status == Status.Failed));
        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibrationFailureForSensorWithoutSetupDateIfSensorAgeIsExpiredForServiceAccount()
        {
            action = Helper.GetCalibrationAction(DeviceType.SC);
            DateTime setupDate = DateTime.Now.AddMonths(-65);
            InstalledComponent component = new InstalledComponent
            {
                Component = new Sensor(string.Format("{0}{1}SEN-001", setupDate.ToString("yy"), setupDate.ToString("MM")))
                {
                    Type = new SensorType(SensorCode.CO),
                    Enabled = true,
                    CalibrationGas = new GasType(GasCode.CO, 14, 13, 0.0, "CO", true, "Carbon Monoxide"),
                    CalibrationGasConcentration = 100
                },
                Position = 1
            };
            action.Instrument.InstalledComponents.Add(component);
            InitializeMocks(action);

            Mock<Schema> schema = new Mock<Schema>();

            schema.Setup(x => x.Activated).Returns(true);
            schema.Setup(x => x.AccountNum).Returns("12345");
            schema.Setup(x => x.ServiceCode).Returns("REPAIR");

            Configuration.Schema = schema.Object;
            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == 1);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(gasResponse => gasResponse.Status == Status.Failed));
        }

        [Fact]
        public void CalibrationParallelThrowCorrectCalibrationGasUnavailbleExceptionIfAppropriateGasIsNotFound()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.NH3 };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<CorrectCalibrationGasUnavailable>(() => operation.Execute());
        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibrationFailureIfPreconditionTimedOut()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.NH3 };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);

            Cylinder cyl = new Cylinder("1810-8241", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.NH3.ToString()], 150.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorPreconditionTimeout(It.IsAny<InstalledComponent>())).Returns(new TimeSpan(0, 0, 65));
            instrumentController.Setup(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>())).Returns(0);
            instrumentController.Setup(x => x.GetSensorMaximumReading(It.IsAny<int>(), It.IsAny<double>())).Returns(250);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act 
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.All(response => response.UsedGasEndPoints.Any(gasEndPoint => gasEndPoint.Usage == CylinderUsage.Precondition)));
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(response => response.Status == Status.Failed));
        }

        [Fact]
        public void CalibrationParallelThrowFlowFailedExceptionIfPumpValveWasClosed()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.NH3 };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);

            Cylinder cyl = new Cylinder("1810-8241", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.NH3.ToString()], 150.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));

            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorPreconditionTimeout(It.IsAny<InstalledComponent>())).Returns(new TimeSpan(0, 0, 65));
            instrumentController.Setup(x => x.GetSensorMaximumReading(It.IsAny<int>(), It.IsAny<double>())).Returns(250);

            pumpWrapper.SetupSequence(x => x.GetOpenValvePosition())
                .Returns(1)
                .Returns(1)
                .Returns(1)
                .Returns(0);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<FlowFailedException>(() => operation.Execute());
        }

        [Fact]
        public void CalibrationParallelThrowInstrumentNotDockedExceptionDuringPreConditioning()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.NH3 };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);

            Cylinder cyl = new Cylinder("1810-8241", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.NH3.ToString()], 150.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorPreconditionTimeout(It.IsAny<InstalledComponent>())).Returns(new TimeSpan(0, 0, 65));
            instrumentController.Setup(x => x.GetSensorMaximumReading(It.IsAny<int>(), It.IsAny<double>())).Returns(250);

            controllerWrapper.SetupSequence(x => x.IsDocked())
                .Returns(true)
                .Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<InstrumentNotDockedException>(() => operation.Execute());
        }

        [Fact]
        public void CalibrationParallelThrowInstrumentNotDockedExceptionDuringCalibration()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            InitializeMocks(action);

            controllerWrapper.SetupSequence(x => x.IsDocked())
                .Returns(true)
                .Returns(false);


            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert
            Xunit.Assert.Throws<InstrumentNotDockedException>(() => operation.Execute());
        }

        [Fact]
        public void CalibrationParallelReturnCalibrationFailedIfCalibrationTimedOut()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList,
                DeviceSubType.VentisPro4);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>())).Returns(0);
            instrumentController.Setup(x => x.IsSensorCalibrating(It.IsAny<int>())).Returns(true);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(response => response.Status == Status.Failed));
        }

        [Fact]
        public void CalibrationParallelThrowFlowFailedExceptionIfBadPumpTubingIsDetected()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList,
                DeviceSubType.VentisPro4);
            InitializeMocks(action);

            pumpWrapper.Setup(x => x.IsBadPumpTubing()).Returns(true);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act and assert            
            Xunit.Assert.Throws<FlowFailedException>(() => operation.Execute());
            Xunit.Assert.True(Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal);
        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibrationPassForWorkingSensors()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO, SensorCode.H2S, SensorCode.CombustibleLEL, SensorCode.O2 };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.True(calibrationEvent.GasResponses.All(gasResponse => gasResponse.Status == Status.Passed));
        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibrationFailureForFailedSensor()
        {
            // arrange
            List<string> sensorList = new List<string> { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            InitializeMocks(action);


            instrumentController.SetupSequence(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>()))
                .Returns(0)
                .Returns(20)
                .Returns(35)
                .Returns(55)
                .Returns(60);
            instrumentController.SetupSequence(x => x.IsSensorCalibrating(It.IsAny<int>()))
                .Returns(true)
                .Returns(true)
                .Returns(true)
                .Returns(true)
                .Returns(false);
            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(68);
            instrumentController.Setup(x => x.GetSensorCalibrationStatus(It.IsAny<int>())).Returns(false);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibrationFailureIfSpanReserveIsLessThanOrEqualToZero()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(0);
            instrumentController.Setup(x => x.GetSensorCalibrationStatus(It.IsAny<int>())).Returns(true);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void CalibrationParallelReturnSensorInstrumentAbortedIfPreCalAndPostCalTimeMismatch()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            InitializeMocks(action);

            instrumentController.Setup(x => x.GetSensorCalibrationTimeout(It.IsAny<int>())).Returns(new TimeSpan(30));
            instrumentController.Setup(x => x.GetSensorLastCalibrationTime(It.IsAny<int>())).Returns(DateTime.Now);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.InstrumentAborted);
        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibratureFailureIfSensorFailedForRepairAccount()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            InitializeMocks(action);

            Mock<Schema> schema = new Mock<Schema>();

            schema.Setup(x => x.Activated).Returns(true);
            schema.Setup(x => x.AccountNum).Returns("12345");
            schema.Setup(x => x.ServiceCode).Returns("REPAIR");

            action.DockingStation.SpanReserveThreshold = 75;

            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(60);

            Configuration.Schema = schema.Object;
            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count > 0);
            Xunit.Assert.True(calibrationEvent.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void ReturnSensorCalibrationZeroPassedIfSensorWasNotInvolvedinSensorSpecificCalibration()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO, SensorCode.O2 };
            action = Helper.GetCalibrationAction(DeviceType.MX6, sensorList);
            action.ComponentCodes = new List<string>() { SensorCode.O2 };
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.Contains(calibrationEvent.GasResponses, response => response.SensorCode == SensorCode.CO && response.Status == Status.ZeroPassed);
        }

        [Fact]
        public void CalibrationParallelReturnSensorCalibrationZeroPassedIfSensorWasNotInvolvedinSensorSpecificCalibration()
        {
            // arrange
            List<string> sensorList = new List<string>() { SensorCode.CO, SensorCode.O2 };
            action = Helper.GetCalibrationAction(DeviceType.VPRO, sensorList, DeviceSubType.VentisPro4);
            action.ComponentCodes = new List<string>() { SensorCode.O2 };
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            CreateMasterForTest();
            operation = new InstrumentCalibrationOperation(action);

            // act
            calibrationEvent = (InstrumentCalibrationEvent)operation.Execute();

            // assert
            Xunit.Assert.True(calibrationEvent.GasResponses.Count == sensorList.Count);
            Xunit.Assert.Contains(calibrationEvent.GasResponses, response => response.SensorCode == SensorCode.CO && response.Status == Status.ZeroPassed);
        }

        #endregion

    }
}
