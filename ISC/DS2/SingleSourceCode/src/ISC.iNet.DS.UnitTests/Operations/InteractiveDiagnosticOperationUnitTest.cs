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
using static ISC.iNet.DS.Controller;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InteractiveDiagnosticOperationUnitTest
    {
        private Mock<ControllerWrapper> controllerWrapper;
        private Mock<IConsoleService> consoleService;
        private Mock<PumpManager> pumpManager;
        private Master masterService;
        private Mock<LCDWrapper> lcdMock;
        private Mock<SmartCardWrapper> smartCardWrapper;
        
        public InteractiveDiagnosticOperationUnitTest()
        {

        }

        private void InitializeForTest(InteractiveDiagnosticAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InteractiveDiagnosticAction diagAction)
        {
            controllerWrapper = MockHelper.GetControllerMock(diagAction.DockingStation, null);
            consoleService = MockHelper.GetConsoleServiceMock();
            pumpManager = MockHelper.GetPumpMock();
            lcdMock = MockHelper.GetLCDMock();
            smartCardWrapper = MockHelper.GetSmarcardWrapper();
        }

        private void CreateMasterForMockTest()
        {
            masterService = Master.CreateMaster();

            masterService.ControllerWrapper = controllerWrapper.Object;
            masterService.ConsoleService = consoleService.Object;
            masterService.PumpWrapper = pumpManager.Object;
            masterService.LCDWrapper = lcdMock.Object;
            masterService.SmartCardWrapper = smartCardWrapper.Object;
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsButNoTestsEnabled()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);

            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);
            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.DoesNotContain("Controller.Key", diag.Details);
        }       

        [Fact]
        public void ExecuteInteractiveDiagnosticsForBatteryChargingForNonRechargableBatteries()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.TX1);
            action.DiagnoseBatteryCharging = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.DoesNotContain("Controller.Key", diag.Details);
        }

        [Fact]
        // This test is for testing the instrument's battery charging, so dock expects instrument to be present
        public void ExecuteInteractiveDiagnosticsForBatteryChargingWhenInstrumentNotDocked()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseBatteryCharging = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Instrument present:                  FAILED", diag.Details);
        }

        [Fact]
        // This test is for testing the instrument's battery charging, so dock expects instrument to be present
        // This calls discoverdocked instrument and thereby throws exception, however is exception is NOT thrown
        public void ExecuteInteractiveDiagnosticsForBatteryChargingWhenInstrumentIsDocked()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseBatteryCharging = true;
            InitializeForTest(action);

            controllerWrapper.Setup(x => x.IsDocked()).Returns(true);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Battery installed:                   FAILED", diag.Details);
        }

        [Fact]
        // This test is for testing the instrument's battery charging, so dock expects instrument to be present
        // This calls discoverdocked instrument and thereby throws exception, however is exception is NOT thrown
        public void ExecuteInteractiveDiagnosticsForBatteryChargingWhenInstrumentIsDockedAndFetchInstrumentBatteryDetails() // TODO
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseBatteryCharging = true;
            InitializeForTest(action);

            controllerWrapper.Setup(x => x.IsDocked()).Returns(true);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Battery installed:                   PASSED", diag.Details);
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestKeypad() 
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX6);
            action.DiagnoseKeypad = true;
            InitializeForTest(action);

            controllerWrapper.SetupSequence(x => x.GetKeyPress())
                .Returns(new KeyPress(Key.Left, new TimeSpan(0, 0, 5)))
                .Returns(new KeyPress(Key.Middle, new TimeSpan(0, 0, 5)))
                .Returns(new KeyPress(Key.Right, new TimeSpan(0, 0, 5))); 

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Left key press:                      PASSED", diag.Details);
            Assert.Contains("Middle key press:                    PASSED", diag.Details);
            Assert.Contains("Right key press:                     PASSED", diag.Details);
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestLCD()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX6);
            action.DiagnoseLcd = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("LCD and backlight work correctly:    PASSED", diag.Details);
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestLEDs()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX6);
            action.DiagnoseLeds = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("LEDs work correctly:                 PASSED", diag.Details);
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestBuzzer()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX6);
            action.DiagnoseBuzzer = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Buzzer works correctly:              PASSED", diag.Details);
        }

        [Fact]
        // For docking stations with no lid switches, there's no need to perform the test.
        public void SkipInteractiveDiagnosticsForTestLidSwitchesForDiffusionOnlyDocks()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.GBPRO);
            action.DiagnoseLidSwitches = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.DoesNotContain("Pump adapter present:", diag.Details);
        }

        [Fact]
        // For new Ventis Cradle docking station, there's no need to perform the test.
        public void SkipInteractiveDiagnosticsForTestLidSwitchesForNewVentisCradle()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseLidSwitches = true;
            InitializeForTest(action);

            Configuration.DockingStation.HasNewVentisCradle = true;

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.DoesNotContain("Pump adapter present:", diag.Details);
        }

        [Fact]
        // For docking stations with no lid switches, there's no need to perform the test.
        public void ExecuteInteractiveDiagnosticsForTestLidSwitches()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX6);
            action.DiagnoseLidSwitches = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Pump adapter present:", diag.Details);
        }

        [Fact]
        // For docking stations with no lid switches, there's no need to perform the test.
        public void ExecuteInteractiveDiagnosticsForTestLidSwitchesOnlyForDiffusionLidForVentisLS()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseLidSwitches = true;
            InitializeForTest(action);

            Configuration.DockingStation.PartNumber = Configuration.VENTISLS_PARTNUMBER;

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.DoesNotContain("Pump adapter present:", diag.Details);
            Assert.Contains("Diffusion lid raised:                PASSED", diag.Details);
        }

        [Fact]
        // For docking stations with no lid switches, there's no need to perform the test.
        public void ExecuteInteractiveDiagnosticsForTestiGas()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX6);
            action.DiagnoseIGas = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Pressure switch #3 pressure:         PASSED", diag.Details);
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestCradleGasFlowForMx6()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX6);
            action.DiagnoseCradleGasFlow = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Cradle routes flow to lid:           PASSED", diag.Details);
            Assert.Contains("Cradle routes flow to hose:          PASSED", diag.Details);
        }

        [Fact]
        public void SkipInteractiveDiagnosticsForTestCradleGasFlowForVentisLS()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseCradleGasFlow = true;
            InitializeForTest(action);

            Configuration.DockingStation.PartNumber = Configuration.VENTISLS_PARTNUMBER;

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.DoesNotContain("Cradle routes flow to lid:", diag.Details);
            Assert.DoesNotContain("Cradle routes flow to hose:", diag.Details);
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestCradleGasFlowForSC()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.SC);
            action.DiagnoseCradleGasFlow = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Cradle flow through hose:            PASSED", diag.Details);
            Assert.DoesNotContain("Cradle routes flow to lid:", diag.Details);
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestFlowRateForDSX()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseFlowRate = true;
            InitializeForTest(action);

            controllerWrapper.SetupSequence(x => x.GetKeyPress())
              .Returns(new KeyPress(Key.Left, new TimeSpan(0, 0, 5)))
              .Returns(new KeyPress(Key.Right, new TimeSpan(0, 0, 5)))
              .Returns(new KeyPress(Key.Middle, new TimeSpan(0, 0, 5)));

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Flow Offset:                         PASSED", diag.Details);
        }

        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestFlowRateForiNetDS()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseFlowRate = true;
            InitializeForTest(action);

            Configuration.DockingStation.Reservoir = true;

            controllerWrapper.SetupSequence(x => x.GetKeyPress())
              .Returns(new KeyPress(Key.Left, new TimeSpan(0, 0, 5)))
              .Returns(new KeyPress(Key.Right, new TimeSpan(0, 0, 5)))
              .Returns(new KeyPress(Key.Middle, new TimeSpan(0, 0, 5)));

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Flow Offset:                         PASSED", diag.Details);
        }


        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestInstrumentDetect()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseInstrumentDetection = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Instrument present:", diag.Details);
        }
        
        [Fact]
        public void ExecuteInteractiveDiagnosticsForTestInstrumentCommunicationWithoutInstrument()
        {
            // arrange
            InteractiveDiagnosticAction action = Helper.GetInteractiveDiagnosticAction(DeviceType.MX4);
            action.DiagnoseInstrumentCommunication = true;
            InitializeForTest(action);

            InteractiveDiagnosticOperation interactiveDiagOperation = new InteractiveDiagnosticOperation(action);

            InteractiveDiagnosticEvent diag = (InteractiveDiagnosticEvent)interactiveDiagOperation.Execute();

            Assert.Contains("Instrument present:                  FAILED", diag.Details);
        }
    }
}
