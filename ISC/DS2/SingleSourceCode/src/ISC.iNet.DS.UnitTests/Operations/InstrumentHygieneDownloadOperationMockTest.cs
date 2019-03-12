using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InstrumentHygieneDownloadOperationMockTest
    {
        private Mock<InstrumentController> instrumentController;
        private Mock<ISwitchService> switchServiceInt;
        private Mock<ControllerWrapper> controllerWrapper;
        private Master masterService;
        bool corruptDatalogDetected = false;

        public InstrumentHygieneDownloadOperationMockTest()
        {

        }

        private void InitializeForTest(InstrumentDatalogDownloadAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InstrumentDatalogDownloadAction datalogAction)
        {
            instrumentController = MockHelper.GetInstrumentControllerMock();
            switchServiceInt = MockHelper.GetSwitchServiceMock(datalogAction.Instrument, false, instrumentController.Object);
            controllerWrapper = MockHelper.GetControllerMock(datalogAction.DockingStation, datalogAction.Instrument);
        }

        private void CreateMasterForMockTest()
        {
            masterService = Master.CreateMaster();

            switchServiceInt.Setup(x => x.InstrumentController).Returns(instrumentController.Object);
            masterService.SwitchService = switchServiceInt.Object;
            masterService.ControllerWrapper = controllerWrapper.Object;
        }

        [Fact]
        public void ExecuteDatalogDownload()
        {
            // arrange
            InstrumentDatalogDownloadAction action = Helper.GetDatalogDownloadAction(DeviceType.MX4);

            InitializeForTest(action);

            InstrumentDatalogDownloadOperation datalogDownloadOperation = new InstrumentDatalogDownloadOperation(action);
            InstrumentDatalogDownloadEvent datalogDownloadEvent = (InstrumentDatalogDownloadEvent)datalogDownloadOperation.Execute();

            Assert.True(datalogDownloadEvent.InstrumentSessions.Count == 0);
        }

        [Fact]
        public void ExecuteDatalogDownloadAndDownloadDatalogDetailsFromInstrument()
        {
            // arrange
            InstrumentDatalogDownloadAction action = Helper.GetDatalogDownloadAction(DeviceType.MX6);

            InitializeForTest(action);

            instrumentController.Setup(x => x.GetDatalog(out corruptDatalogDetected))
                .Returns(new List<DatalogSession> { new DatalogSession() { BaseUnitSerialNumber = string.Empty, Comments= string.Empty,
                 RecordingInterval = 60, SerialNumber = action.Instrument.SerialNumber, SessionNumber = 1, TWATimeBase = 10
                , User = string.Empty, Session = DateTime.Now
                , SensorSessions = new List<DatalogSensorSession>() { new DatalogSensorSession("SENSORTST123", new ComponentType(SensorCode.O2)) } } } );

            InstrumentDatalogDownloadOperation datalogDownloadOperation = new InstrumentDatalogDownloadOperation(action);
            InstrumentDatalogDownloadEvent datalogDownloadEvent = (InstrumentDatalogDownloadEvent)datalogDownloadOperation.Execute();

            Assert.True(datalogDownloadEvent.InstrumentSessions.Count == 1);
        }

        [Fact]
        public void ExecuteCorruptDatalogDownload()
        {
            // arrange
            InstrumentDatalogDownloadAction action = Helper.GetDatalogDownloadAction(DeviceType.MX6);

            InitializeForTest(action);

            DateTime sessionTime = DateTime.Now;

            instrumentController.Setup(x => x.GetDatalog(out corruptDatalogDetected))
                .Returns(new List<DatalogSession> { new DatalogSession() { BaseUnitSerialNumber = string.Empty, Comments= string.Empty,
                 RecordingInterval = 60, SerialNumber = action.Instrument.SerialNumber, SessionNumber = 1, TWATimeBase = 10
                , User = string.Empty, Session = sessionTime, CorruptionException = new Exception("Corrupt Datalog Exception")
                , SensorSessions = new List<DatalogSensorSession>() { new DatalogSensorSession("SENSORTST123", new ComponentType(SensorCode.O2)) } } });

            InstrumentDatalogDownloadOperation datalogDownloadOperation = new InstrumentDatalogDownloadOperation(action);
            InstrumentDatalogDownloadEvent datalogDownloadEvent = (InstrumentDatalogDownloadEvent)datalogDownloadOperation.Execute();

            Assert.True(datalogDownloadEvent.InstrumentSessions.Count == 1 
                && datalogDownloadEvent.InstrumentSessions[0].BaseUnitSerialNumber == string.Empty
                && datalogDownloadEvent.InstrumentSessions[0].Comments == string.Empty
                && datalogDownloadEvent.InstrumentSessions[0].RecordingInterval == 60
                && datalogDownloadEvent.InstrumentSessions[0].SerialNumber == action.Instrument.SerialNumber
                && datalogDownloadEvent.InstrumentSessions[0].SessionNumber == 1
                && datalogDownloadEvent.InstrumentSessions[0].TWATimeBase == 10
                && datalogDownloadEvent.InstrumentSessions[0].User == string.Empty
                && datalogDownloadEvent.InstrumentSessions[0].Session == sessionTime
                && datalogDownloadEvent.InstrumentSessions[0].CorruptionException.Message == "Corrupt Datalog Exception");
        }
    }
}
