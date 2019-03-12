using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Moq;
using Xunit;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InstrumentSettingsReadOperationMockTest
    {

        #region [ Fields ]
        
        private Mock<ISwitchService> switchService = null;
        private Mock<ControllerWrapper> controllerWrapper = null;

        DockingStation dockingStation = null;
        Instrument instrument = null;
        Master master = null;
        InstrumentSettingsReadAction action = null;

        #endregion

        #region [ Private Methods ]

        private void Initialize()
        {
            if (dockingStation != null)
                action.DockingStation = dockingStation;
            if (instrument != null)
                action.Instrument = instrument;
            InitializeMocks(action.DockingStation, action.Instrument);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForTest();
        }

        private void CreateMasterForTest()
        {
            master = Master.CreateMaster();
            
            master.SwitchService = switchService.Object;
            master.ControllerWrapper = controllerWrapper.Object;
        }

        private void InitializeMocks(DockingStation dockingStation = null, Instrument instrument = null)
        {
            switchService = MockHelper.GetSwitchServiceMock(instrument);
            controllerWrapper = MockHelper.GetControllerMock(dockingStation, instrument);

        }

        #endregion

        #region [ Test Methods ]

        [Fact]
        public void ExecuteInstrumentSettingsReadOperation()
        {
            // arrange
            InstrumentSettingsReadAction action = new InstrumentSettingsReadAction();
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro5);
            Initialize();

            InstrumentSettingsReadOperation operation = new InstrumentSettingsReadOperation(action);

            // act
            InstrumentSettingsReadEvent dsEvent = (InstrumentSettingsReadEvent)operation.Execute();

            // assert
            Instrument testInstrument = dsEvent.DockedInstrument;
            Xunit.Assert.True(testInstrument.SerialNumber == instrument.SerialNumber
                && testInstrument.Type == instrument.Type
                && testInstrument.Subtype == instrument.Subtype
                && testInstrument.SoftwareVersion == instrument.SoftwareVersion
                && testInstrument.BumpTimeout == instrument.BumpTimeout);
        }

        [Fact]
        public void ThrowNotDockedExceptionIfInstrumentNotDocked()
        {
            // arrange
            action = new InstrumentSettingsReadAction();
            Initialize();

            InstrumentSettingsReadOperation operation = new InstrumentSettingsReadOperation(action);

            // act and assert
            Xunit.Assert.Throws<InstrumentNotDockedException>(() => operation.Execute());
        }

        #endregion

    }
}
