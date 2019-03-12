using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using Xunit;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InstrumentManualOperationsClearOperationMockTest
    {

        private Mock<InstrumentController> instrumentController;
        private Mock<ISwitchService> switchServiceInt;
        private Mock<ControllerWrapper> controllerWrapper;
        private Master masterService;

        public InstrumentManualOperationsClearOperationMockTest()
        {

        }

        private void InitializeForTest(InstrumentManualOperationsClearAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InstrumentManualOperationsClearAction manualOperationsClearAction)
        {
            instrumentController = MockHelper.GetInstrumentControllerMock();
            switchServiceInt = MockHelper.GetSwitchServiceMock(manualOperationsClearAction.Instrument, false, instrumentController.Object);
            controllerWrapper = MockHelper.GetControllerMock(manualOperationsClearAction.DockingStation, manualOperationsClearAction.Instrument);
        }

        private void CreateMasterForMockTest()
        {
            masterService = Master.CreateMaster();

            switchServiceInt.Setup(x => x.InstrumentController).Returns(instrumentController.Object);
            masterService.SwitchService = switchServiceInt.Object;
            masterService.ControllerWrapper = controllerWrapper.Object;
        }

        [Fact]
        public void ClearManualGasOperations()
        {
            // arrange
            InstrumentManualOperationsClearAction action = Helper.GetManualOperationsClearAction(DeviceType.MX4);

            InitializeForTest(action);

            instrumentController.Setup(x => x.ClearManualGasOperations());

            InstrumentManualOperationsClearOperation manualOperationsClearOperation = new InstrumentManualOperationsClearOperation(action);
            InstrumentManualOperationsClearEvent manualOperationsClearEvent = (InstrumentManualOperationsClearEvent)manualOperationsClearOperation.Execute();

            instrumentController.Verify(x => x.ClearManualGasOperations(), Times.Once);
        }
    }
}
