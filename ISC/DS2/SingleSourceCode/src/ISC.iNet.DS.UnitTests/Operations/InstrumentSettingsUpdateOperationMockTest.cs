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
    public class InstrumentSettingsUpdateOperationMockTest
    {
        #region [ Fields ]

        private Mock<ISwitchService> switchService = null;
        private Mock<ControllerWrapper> controllerWrapper = null;

        DockingStation dockingStation = null;
        Instrument instrument = null;
        Master master = null;
        InstrumentSettingsUpdateAction action = null;

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
        public void ThrowNotDockedExceptionIfInstrumentNotDocked()
        {
            // arrange
            action = new InstrumentSettingsUpdateAction();
            Initialize();

            InstrumentSettingsUpdateOperation operation = new InstrumentSettingsUpdateOperation(action);

            // act and assert
            Xunit.Assert.Throws<InstrumentNotDockedException>(() => operation.Execute());
        }

        [Fact]
        public void ThrowNotDockedExceptionIfInstrumentSerialNumberIsEmpty()
        {
            // arrange
            action = new InstrumentSettingsUpdateAction();
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);
            instrument = Helper.GetInstrumentForTest(DeviceType.MX4);

            instrument.SerialNumber = string.Empty;

            Initialize();

            InstrumentSettingsUpdateOperation operation = new InstrumentSettingsUpdateOperation(action);

            // act and assert
            Xunit.Assert.Throws<InstrumentNotDockedException>(() => operation.Execute());
        }

        [Fact]
        public void ThrowNotDockedExceptionIfInstrumentTypeIsUnknown()
        {
            // arrange
            action = new InstrumentSettingsUpdateAction();
            instrument = Helper.GetInstrumentForTest(DeviceType.Unknown);

            Initialize();

            InstrumentSettingsUpdateOperation operation = new InstrumentSettingsUpdateOperation(action);

            // act and assert
            Xunit.Assert.Throws<InstrumentNotDockedException>(() => operation.Execute());
        }
        #endregion
    }
}
