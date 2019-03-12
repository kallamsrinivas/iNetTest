using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using Xunit;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InstrumentHygieneClearOperationMockTest
    {
        private Mock<InstrumentController> instrumentController;
        private Mock<ISwitchService> switchServiceInt;
        private Mock<ControllerWrapper> controllerWrapper;
        private Master masterService;

        public InstrumentHygieneClearOperationMockTest()
        {

        }

        private void InitializeForTest(InstrumentDatalogClearAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InstrumentDatalogClearAction datalogClearAction)
        {
            instrumentController = MockHelper.GetInstrumentControllerMock();
            switchServiceInt = MockHelper.GetSwitchServiceMock(datalogClearAction.Instrument, false, instrumentController.Object);
            controllerWrapper = MockHelper.GetControllerMock(datalogClearAction.DockingStation, datalogClearAction.Instrument);
        }

        private void CreateMasterForMockTest()
        {
            masterService = Master.CreateMaster();

            switchServiceInt.Setup(x => x.InstrumentController).Returns(instrumentController.Object);
            masterService.SwitchService = switchServiceInt.Object;
            masterService.ControllerWrapper = controllerWrapper.Object;
        }

        [Fact]
        public void ClearAlarmEvents()
        {
            // arrange
            InstrumentDatalogClearAction action = Helper.GetDatalogClearAction(DeviceType.MX4);

            InitializeForTest(action);

            instrumentController.Setup(x => x.ClearDatalog());

            InstrumentDatalogClearOperation datalogClearOperation = new InstrumentDatalogClearOperation(action);
            InstrumentDatalogClearEvent datalogClearEvent = (InstrumentDatalogClearEvent)datalogClearOperation.Execute();

            instrumentController.Verify(x => x.ClearDatalog(), Times.Once);
        }

    }
}
