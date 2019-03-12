using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using ISC.iNet.DS.DataAccess;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InstrumentDiagnosticOperationMockTest
    {

        private Mock<InstrumentController> instrumentController;
        private Mock<ISwitchService> switchServiceInt;
        private Mock<ControllerWrapper> controllerWrapper;
        private Mock<IConsoleService> consoleService;
        private Mock<PumpManager> pumpManager;
        private Master masterService;
        private Mock<CriticalErrorDataAccess> DiagErrorDataAccess;

        public InstrumentDiagnosticOperationMockTest()
        {

        }

        private void InitializeForTest(InstrumentDiagnosticAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InstrumentDiagnosticAction diagAction)
        {
            instrumentController = MockHelper.GetInstrumentControllerMockForDiag(diagAction);
            switchServiceInt = MockHelper.GetSwitchServiceMock(diagAction.Instrument, false, instrumentController.Object);
            controllerWrapper = MockHelper.GetControllerMock(diagAction.DockingStation, diagAction.Instrument);
            consoleService = MockHelper.GetConsoleServiceMock();
            pumpManager = MockHelper.GetPumpMock();
            DiagErrorDataAccess = MockHelper.GetDiagCriticalErrorsMock();
        }

        private void CreateMasterForMockTest()
        {
            masterService = Master.CreateMaster();

            switchServiceInt.Setup(x => x.InstrumentController).Returns(instrumentController.Object);
            masterService.SwitchService = switchServiceInt.Object;
            masterService.ControllerWrapper = controllerWrapper.Object;
            masterService.ConsoleService = consoleService.Object;
            masterService.PumpWrapper = pumpManager.Object;
            masterService.Scheduler = MockHelper.GetSchedulerMock().Object;
        }

        [Fact]
        public void ExecuteGeneralDiag()
        {
            // arrange
            InstrumentDiagnosticAction action = Helper.GetDiagnosticAction(DeviceType.MX4);

            InitializeForTest(action);

            InstrumentDiagnosticOperation diagOperation = new InstrumentDiagnosticOperation(action);
            InstrumentDiagnosticEvent diag = (InstrumentDiagnosticEvent)diagOperation.Execute();

            Assert.True(diag.Diagnostics.Count == 2);

            instrumentController.Verify(x => x.ClearInstrumentErrors(), Times.Once);
        }

        [Fact]
        public void ExecuteDiagnosticsForMX6InstrumentErrorsOnCustomerAccount()
        {
            // arrange
            InstrumentDiagnosticAction action = Helper.GetDiagnosticAction(DeviceType.MX6);

            InitializeForTest(action);                        

            InstrumentDiagnosticOperation diagOperation = new InstrumentDiagnosticOperation(action);
            diagOperation.criticalErrorsList = DiagErrorDataAccess.Object.FindAll();

            InstrumentDiagnosticEvent diag = (InstrumentDiagnosticEvent)diagOperation.Execute();

            Assert.True(diag.Diagnostics.Count == 2 && diag.InstrumentInCriticalError 
                && diag.InstrumentCriticalErrorCode == diagOperation.criticalErrorsList[0].Code.ToString());

            instrumentController.Verify(x => x.ClearInstrumentErrors(), Times.Never);
        }

        [Fact]
        public void ExecuteDiagnosticsForMX4InstrumentErrorsOnRepairAccount()
        {
            // arrange
            InstrumentDiagnosticAction action = Helper.GetDiagnosticAction(DeviceType.MX4);

            InitializeForTest(action);

            Configuration.Schema.ServiceCode = "REPAIR";

            InstrumentDiagnosticOperation diagOperation = new InstrumentDiagnosticOperation(action);
            diagOperation.criticalErrorsList = DiagErrorDataAccess.Object.FindAll();

            InstrumentDiagnosticEvent diag = (InstrumentDiagnosticEvent)diagOperation.Execute();

            Assert.True(diag.Diagnostics.Count == 2 && diag.InstrumentInCriticalError
                && diag.InstrumentCriticalErrorCode == diagOperation.criticalErrorsList[0].Code.ToString());

            instrumentController.Verify(x => x.ClearInstrumentErrors(), Times.Never);
        }
    }
}
