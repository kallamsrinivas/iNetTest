using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using System;
using System.Linq;
using Xunit;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InstrumentAlarmEventsDownloadOperationMockTest
    {
        private Mock<InstrumentController> instrumentController;
        private Mock<ISwitchService> switchServiceInt;
        private Mock<ControllerWrapper> controllerWrapper;
        private Master masterService;

        public InstrumentAlarmEventsDownloadOperationMockTest()
        {

        }

        private void InitializeForTest(InstrumentAlarmEventsDownloadAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InstrumentAlarmEventsDownloadAction alarmAction)
        {
            instrumentController = MockHelper.GetInstrumentControllerMock();
            switchServiceInt = MockHelper.GetSwitchServiceMock(alarmAction.Instrument, false, instrumentController.Object);
            controllerWrapper = MockHelper.GetControllerMock(alarmAction.DockingStation, alarmAction.Instrument);
        }

        private void CreateMasterForMockTest()
        {
            masterService = Master.CreateMaster();

            switchServiceInt.Setup(x => x.InstrumentController).Returns(instrumentController.Object);
            masterService.SwitchService = switchServiceInt.Object;
            masterService.ControllerWrapper = controllerWrapper.Object;
        }

        [Fact]
        public void ExecuteAlarmEventDownload()
        {
            // arrange
            InstrumentAlarmEventsDownloadAction action = Helper.GetAlarmEventDownloadAction(DeviceType.MX4);

            InitializeForTest(action);

            InstrumentAlarmEventsDownloadOperation alarmDownloadOperation = new InstrumentAlarmEventsDownloadOperation(action);
            InstrumentAlarmEventsDownloadEvent alarmDownloadEvent = (InstrumentAlarmEventsDownloadEvent)alarmDownloadOperation.Execute();

            Assert.True(alarmDownloadEvent.AlarmEvents.Length == 0);            
        }

        [Fact]
        public void ExecuteAlarmEventDownloadAndDownloadAlarmDetailsFromInstrument()
        {
            // arrange
            InstrumentAlarmEventsDownloadAction action = Helper.GetAlarmEventDownloadAction(DeviceType.GBPRO);

            InitializeForTest(action);

            DateTime alarmTime = DateTime.Now;

            instrumentController.Setup(x => x.GetAlarmEvents())
                .Returns(new AlarmEvent[1] { new AlarmEvent() { InstrumentSerialNumber = action.Instrument.SerialNumber , AlarmOperatingMode = AlarmOperatingMode.Running, Duration = 10
                , AlarmHigh = 23.5, AlarmLow = 19.5, BaseUnitSerialNumber = string.Empty, GasCode = GasCode.O2, IsDocked = true
                , IsDualSense = false, PeakReading = 20.9, SensorCode = SensorCode.O2, SensorSerialNumber = "TESTSENSOR123"
                , Site = string.Empty, User = string.Empty, SpeakerVoltage = 10, Ticks = 20, Timestamp = alarmTime, VibratingMotorVoltage = 10 } });

            InstrumentAlarmEventsDownloadOperation alarmDownloadOperation = new InstrumentAlarmEventsDownloadOperation(action);
            InstrumentAlarmEventsDownloadEvent alarmDownloadEvent = (InstrumentAlarmEventsDownloadEvent)alarmDownloadOperation.Execute();

            Assert.True(alarmDownloadEvent.AlarmEvents.Length == 1
                && alarmDownloadEvent.AlarmEvents[0].InstrumentSerialNumber == action.Instrument.SerialNumber
                && alarmDownloadEvent.AlarmEvents[0].AlarmOperatingMode == AlarmOperatingMode.Running
                && alarmDownloadEvent.AlarmEvents[0].Duration == 10
                && alarmDownloadEvent.AlarmEvents[0].AlarmHigh == 23.5
                && alarmDownloadEvent.AlarmEvents[0].AlarmLow == 19.5
                && alarmDownloadEvent.AlarmEvents[0].BaseUnitSerialNumber == string.Empty
                && alarmDownloadEvent.AlarmEvents[0].GasCode == GasCode.O2
                && alarmDownloadEvent.AlarmEvents[0].IsDocked == true
                && alarmDownloadEvent.AlarmEvents[0].IsDualSense == false
                && alarmDownloadEvent.AlarmEvents[0].PeakReading == 20.9
                && alarmDownloadEvent.AlarmEvents[0].SensorCode == SensorCode.O2
                && alarmDownloadEvent.AlarmEvents[0].SensorSerialNumber == "TESTSENSOR123"
                && alarmDownloadEvent.AlarmEvents[0].Site == string.Empty
                && alarmDownloadEvent.AlarmEvents[0].User == string.Empty
                && alarmDownloadEvent.AlarmEvents[0].SpeakerVoltage == 10
                && alarmDownloadEvent.AlarmEvents[0].Ticks == 20
                && alarmDownloadEvent.AlarmEvents[0].Timestamp == alarmTime
                && alarmDownloadEvent.AlarmEvents[0].VibratingMotorVoltage == 10);
        }
    }
}
