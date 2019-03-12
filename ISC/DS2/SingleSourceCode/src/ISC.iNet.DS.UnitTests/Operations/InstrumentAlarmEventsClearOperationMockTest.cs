using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using Xunit;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InstrumentAlarmEventsClearOperationMockTest
    {
        private Mock<InstrumentController> instrumentController;
        private Mock<ISwitchService> switchServiceInt;
        private Mock<ControllerWrapper> controllerWrapper;
        private Master masterService;

        public InstrumentAlarmEventsClearOperationMockTest()
        {

        }

        private void InitializeForTest(InstrumentAlarmEventsClearAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InstrumentAlarmEventsClearAction manualOperationsDownloadAction)
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
        public void ClearAlarmEvents()
        {
            // arrange
            InstrumentAlarmEventsClearAction action = Helper.GetAlarmEventsClearAction(DeviceType.MX4);

            InitializeForTest(action);

            instrumentController.Setup(x => x.ClearAlarmEvents());

            InstrumentAlarmEventsClearOperation alarmEventsClearOperation = new InstrumentAlarmEventsClearOperation(action);
            InstrumentAlarmEventsClearEvent alarmEventsClearEvent = (InstrumentAlarmEventsClearEvent)alarmEventsClearOperation.Execute();

            instrumentController.Verify(x => x.ClearAlarmEvents(), Times.Once);
        }        
    }
}
