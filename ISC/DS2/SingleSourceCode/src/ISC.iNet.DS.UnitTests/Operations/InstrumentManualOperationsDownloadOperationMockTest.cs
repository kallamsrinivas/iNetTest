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
    public class InstrumentManualOperationsDownloadOperationMockTest
    {

        private Mock<InstrumentController> instrumentController;
        private Mock<ISwitchService> switchServiceInt;
        private Mock<ControllerWrapper> controllerWrapper;
        private Master masterService;

        public InstrumentManualOperationsDownloadOperationMockTest()
        {

        }

        private void InitializeForTest(InstrumentManualOperationsDownloadAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InstrumentManualOperationsDownloadAction manualOperationsDownloadAction)
        {
            instrumentController = MockHelper.GetInstrumentControllerMock();
            switchServiceInt = MockHelper.GetSwitchServiceMock(manualOperationsDownloadAction.Instrument, false, instrumentController.Object);
            controllerWrapper = MockHelper.GetControllerMock(manualOperationsDownloadAction.DockingStation, manualOperationsDownloadAction.Instrument);
        }

        private void CreateMasterForMockTest()
        {
            masterService = Master.CreateMaster();

            switchServiceInt.Setup(x => x.InstrumentController).Returns(instrumentController.Object);
            masterService.SwitchService = switchServiceInt.Object;
            masterService.ControllerWrapper = controllerWrapper.Object;
        }

        [Fact]
        public void DownloadManualOperationsWithoutData()
        {
            // arrange
            InstrumentManualOperationsDownloadAction action = Helper.GetManualOperationsDownloadAction(DeviceType.MX4);

            InitializeForTest(action);

            InstrumentManualOperationsDownloadOperation manualOpDownloadOperation = new InstrumentManualOperationsDownloadOperation(action);
            InstrumentManualOperationsDownloadEvent manualOpDownloadEvent = (InstrumentManualOperationsDownloadEvent)manualOpDownloadOperation.Execute();

            Assert.True(manualOpDownloadEvent.GasResponses.Count == 0);
        }

        [Fact]
        public void DownloadManualOperationsData()
        {
            // arrange
            InstrumentManualOperationsDownloadAction action = Helper.GetManualOperationsDownloadAction(DeviceType.MX6);

            InitializeForTest(action);

            DateTime cylExpirationDate = DateTime.Now.AddMonths(8);

            instrumentController.Setup(x => x.GetManualGasOperations())
                .Returns(new SensorGasResponse[] { new SensorGasResponse() });

            InstrumentManualOperationsDownloadOperation manualOpDownloadOperation = new InstrumentManualOperationsDownloadOperation(action);
            InstrumentManualOperationsDownloadEvent manualOpDownloadEvent = (InstrumentManualOperationsDownloadEvent)manualOpDownloadOperation.Execute();

            Assert.True(manualOpDownloadEvent.GasResponses.Count == 1);
        }

        [Fact]
        public void ThrowArgumentOutOfRangeException()
        {
            // arrange
            InstrumentManualOperationsDownloadAction action = Helper.GetManualOperationsDownloadAction(DeviceType.MX6);

            InitializeForTest(action);

            DateTime sessionTime = DateTime.Now;

            instrumentController.Setup(x => x.GetManualGasOperations())
                .Throws( new ArgumentOutOfRangeException());

            InstrumentManualOperationsDownloadOperation manualOpDownloadOperation = new InstrumentManualOperationsDownloadOperation(action);
            InstrumentManualOperationsDownloadEvent manualOpDownloadEvent = (InstrumentManualOperationsDownloadEvent)manualOpDownloadOperation.Execute();

            Assert.True(manualOpDownloadEvent.Errors.Count > 0);
        }
    }
}
