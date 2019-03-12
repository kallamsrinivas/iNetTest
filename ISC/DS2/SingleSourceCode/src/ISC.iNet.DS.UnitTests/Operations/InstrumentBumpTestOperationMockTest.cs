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
using static ISC.iNet.DS.Instruments.InstrumentController;

namespace ISC.iNet.DS.UnitTests.Operations
{
    public class InstrumentBumpTestOperationMockTest
    {

        private Mock<InstrumentController> instrumentController;
        private Mock<ISwitchService> switchServiceInt;
        private Mock<ControllerWrapper> controllerWrapper ;
        private Mock<IConsoleService> consoleService;
        private Mock<PumpManager> pumpManager;
        private Master masterService;

        public InstrumentBumpTestOperationMockTest()
        {

        }

        private void InitializeForTest(InstrumentBumpTestAction action)
        {
            InitializeMocks(action);

            Configuration.DockingStation = action.DockingStation;
            Configuration.Schema = Helper.GetSchemaForTest();

            CreateMasterForMockTest();
        }

        private void InitializeMocks(InstrumentBumpTestAction bumpTestAction)
        {
            instrumentController = MockHelper.GetInstrumentControllerMockForBump(bumpTestAction);
            switchServiceInt = MockHelper.GetSwitchServiceMock(bumpTestAction.Instrument, false, instrumentController.Object);
            controllerWrapper = MockHelper.GetControllerMock(bumpTestAction.DockingStation, bumpTestAction.Instrument);
            consoleService = MockHelper.GetConsoleServiceMock();
            pumpManager = MockHelper.GetPumpMock();
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
        public void ThrowInstrumentNotDockedBeforeActualBumpBegins()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX4);

            InitializeForTest(action);

            controllerWrapper.Setup(x => x.IsDocked()).Returns(false);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);
            Assert.Throws<InstrumentNotDockedException>(() =>  test.Execute());
        }



        [Fact]
        public void ThrowInstrumentNotDockedDuringBumpTest()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX4, new List<string>() { GasCode.CO });

            InitializeForTest(action);

            controllerWrapper.SetupSequence(x => x.IsDocked())
                .Returns(true)
                .Returns(false);
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);
            Assert.Throws<InstrumentNotDockedException>(() => test.Execute());
        }

        [Fact]
        public void IgnoreIfNoSensorsInstalled()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6);

            Battery battComp = new Battery("TESTBAT-001");
            InstalledComponent installedComp = new InstalledComponent();
            installedComp.Position = 1;
            installedComp.Component = battComp;
            action.Instrument.InstalledComponents.Add(installedComp);

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);
            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 0);
        }

        [Fact]
        public void IgnoreIfNoSensorsAreEnabledInSingleSensorMode()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.SC, new List<string>() { GasCode.CO.ToString(), GasCode.H2S.ToString() });
            action.Instrument.InstalledComponents[0].Component.Enabled = false;
            action.Instrument.InstalledComponents[1].Component.Enabled = false;
            action.DockingStation.SingleSensorMode = true;

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);
            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 0);
        }

        [Fact]
        public void IgnoreIfNoSensorsAreEnabled()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.SC, new List<string>() { GasCode.CO.ToString(), GasCode.H2S.ToString() });
            action.Instrument.InstalledComponents[0].Component.Enabled = false;
            action.Instrument.InstalledComponents[1].Component.Enabled = false;

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);
            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 0);
        }

        [Fact]
        public void ThrowIfGasEndPointNotIdentified()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX4, new List<string>() { GasCode.O2.ToString() }, DeviceSubType.VentisPro5);
            
            InitializeForTest(action);

            if (action.GasEndPoints.Count > 1)
                action.GasEndPoints.RemoveAt(1); 
            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);

            Assert.Throws<CorrectBumpTestGasUnavailable>(() => operation.Execute());
        }

        [Fact]
        public void ThrowIfInstrumentNotInBiasState()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX4, new List<string>() { GasCode.O2.ToString() }, DeviceSubType.VentisPro5);

            InitializeForTest(action);

            instrumentController.Setup(x => x.GetSensorBiasStatus()).Returns(false);
            
            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);

            Assert.Throws<InstrumentNotReadyException>(() => operation.Execute());
        }

        [Fact]
        public void CLO2BumpSkippedIfCL2GasEndPointNotAvailable()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.ClO2.ToString() });

            InitializeForTest(action);

            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);
            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)operation.Execute();

            // act and assert
            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].Status == Status.Skipped);
        }

        [Fact]
        public void ConfirmBumpTimeoutAndThresholdMatchInstrumentSettingForDSX()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.ClO2.ToString() });

            action.Instrument.BumpThreshold = 45;

            InitializeForTest(action);

            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);
            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)operation.Execute();

            // act and assert
            Assert.True(bump.GasResponses[0].Threshold == 45 && bump.GasResponses[0].Timeout == 10);
        }

        [Fact]
        public void ConfirmBumpTimeoutAndThresholdMatchDefaultValuesForStandalone()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.ClO2.ToString() });

            action.Instrument.BumpTimeout = 100; // This should NOT be considered for standalone
            action.Instrument.BumpThreshold = 45;  // This should NOT be considered for standalone

            InitializeForTest(action);
                        
            Configuration.Schema.Activated = false; // Standalone

            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);
            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)operation.Execute();

            Assert.True(bump.GasResponses[0].Threshold == SensorGasResponse.DEFAULT_BUMP_THRESHOLD && bump.GasResponses[0].Timeout == SensorGasResponse.DEFAULT_BUMP_TIMEOUT);
        }

        [Fact]
        public void SkipCurrentPassIfSensorSymbolMessageOnLCDIsEmptyString()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.TX1, new List<string>() { GasCode.CO.ToString(), GasCode.CO.ToString() });

            InitializeForTest(action);

            consoleService.Setup(x => x.GetSensorLabel(It.IsAny<string>())).Returns("");
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses[0].Duration <= 0 && bump.GasResponses[1].Duration <= 0
                                && bump.GasResponses[0].Reading <= 0 && bump.GasResponses[1].Reading <= 0
                                && bump.GasResponses[0].UsedGasEndPoints.Count <= 0 && bump.GasResponses[1].UsedGasEndPoints.Count <= 0);
        }

        [Fact]
        public void ThrowFlowFailedIfFlowCheckFails()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.TX1, new List<string>() { GasCode.CO.ToString(), GasCode.CO.ToString() });

            InitializeForTest(action);

            pumpManager.Setup(x => x.GetOpenValvePosition()).Returns(0); // to mark flow failed as true
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            Assert.Throws<FlowFailedException>(() => test.Execute());
        }

        [Fact]
        public void IgnoreCalFaultSensorIfRedundantSensorPassed()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.TX1, new List<string>() { GasCode.CO.ToString(), GasCode.CO.ToString() });
            ((Sensor)action.Instrument.InstalledComponents[0].Component).CalibrationStatus = Status.Failed; // FailedManual is NOT accepted why?
                       
            action.DockingStation.SingleSensorMode = true;

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1);
        }

        [Fact]
        public void ConsiderCalFaultSensorIfRedundantSensorPassedForRepairAccount()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.TX1, new List<string>() { GasCode.CO.ToString(), GasCode.CO.ToString() });
            ((Sensor)action.Instrument.InstalledComponents[0].Component).CalibrationStatus = Status.Failed; // FailedManual is NOT accepted why?
            
            action.DockingStation.SingleSensorMode = true;

            InitializeForTest(action);

            Configuration.Schema.ServiceCode = "REPAIR";

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 2);
        }

        [Fact]
        public void ConsiderExpiredCylinderIfConfigured()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.TX1, new List<string>() { GasCode.CO.ToString(), GasCode.CO.ToString() });

            action.GasEndPoints[1].Cylinder.ExpirationDate = DateTime.Now.AddMonths(-10);

            action.DockingStation.SingleSensorMode = true;
            action.DockingStation.UseExpiredCylinders = true;

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 2
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[1].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[0].Status == Status.Failed
                && bump.GasResponses[1].Status == Status.Failed);
        }

        [Fact]
        public void ContinueBumpIfNOExpiredCylinderIsAvailableWhenUseExpiredCylinderForBumpIsEnabled()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.TX1, new List<string>() { GasCode.CO.ToString(), GasCode.CO.ToString() });

            action.DockingStation.SingleSensorMode = true;
            action.DockingStation.UseExpiredCylinders = true;

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 2
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[1].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[0].Status == Status.Failed
                && bump.GasResponses[1].Status == Status.Failed);
        }

        [Fact]
        public void UseTheConfiguredBumpGasForLEL()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.CombustibleLEL.ToString() });

            //Port 3, add Methane cylinder
            Cylinder cyl = new Cylinder("1810-2242", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.CO.ToString()], 100.00),
                                                                   new GasConcentration(GasType.Cache[GasCode.H2S.ToString()], 25.00),
                                                                   new GasConcentration(GasType.Cache[GasCode.Methane.ToString()], 25000.00),
                                                                   new GasConcentration(GasType.Cache[GasCode.O2.ToString()], 180000.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));
                        
            action.DockingStation.CombustibleBumpTestGas = GasCode.Methane.ToString();

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].UsedGasEndPoints.Count == 1
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[2].Cylinder.PartNumber) != null
                && bump.GasResponses[0].Status == Status.Failed);
        }

        [Fact]
        public void BumpFailedFreshAirPassed()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(20.2)   // O2 High Bump Test Reading to Pass Initial High Bump Test so that O2 recovery purge DOES NOT happen
               .Returns(0);     // O2 Low bump test reading to FAIL LOW BUMP TEST
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].UsedGasEndPoints.Count == 1
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[0].Status == Status.BumpFailedFreshAirPassed);
        }

        [Fact]
        public void BumpPassedFreshAirPassed()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(20.2)   // O2 High Bump Test Reading to Pass Initial High Bump Test so that O2 recovery purge DOES NOT happen
               .Returns(18.5)   // Readings within first  5 seconds are IGNORED, 
               .Returns(18.5)   // Readings within first  5 seconds are IGNORED, 
               .Returns(18.5);  // O2 Low bump test reading to PASS LOW BUMP TEST
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].UsedGasEndPoints.Count == 1  // To ensure that O2 recovery Purge did NOT happen
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[0].Status == Status.Passed);
        }

        [Fact]
        public void BumpPassedFreshAirFailed()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(10.2)   // O2 High Bump Test Reading to FAIL Initial High Bump Test 
               .Returns(12.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(18.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(18.6)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(18.7)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(18.5)   // Readings within first  5 seconds are IGNORED, 
               .Returns(18.5)   // O2 Low bump test reading to PASS LOW BUMP TEST
               .Returns(18.5);  // O2 Low bump test reading to PASS LOW BUMP TEST
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses[0].Status == Status.BumpPassedFreshAirFailed);
        }

        [Fact]
        public void BumpFailedFreshAirFailed()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(10.2)   // O2 High Bump Test Reading to FAIL Initial High Bump Test 
               .Returns(12.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.6)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.7)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.8)   // Readings within first  5 seconds are IGNORED, 
               .Returns(14.8)   // O2 Low bump test reading to PASS LOW BUMP TEST
               .Returns(14.9);  // O2 Low bump test reading to PASS LOW BUMP TEST 
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses[0].Status == Status.BumpFailedFreshAirFailed);
        }

        [Fact]
        // This can also be covered by the  commented Asset on "BumpFailedFreshAirFailed" unit test case. However I have FAILED initial High BUmp and 
        // Passed Final high bump here so that the scenario is a bit different from "BumpFailedFreshAirFailed" unit test case. But I believe that we
        // do NOT need a unit test for a scenario where if reading on FINAL HIGH BUMP is higher than 20 and less than 22 is a high PASS CRITERIA.
        // Because thats what I have done here.
        public void PerformO2RecoveryPurgeFor2MinsIfInitialHighBumpFails()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(19.9)   // O2 High Bump Test Reading to FAIL Initial High Bump Test - 20 is a PASS
               .Returns(20.5)   // O2 High Bump Test Reading to PASS FINAL High Bump Test / O2 recovery Purge
               .Returns(14.7)   // Readings within first 5 seconds are IGNORED, 
               .Returns(18.8)   // Readings within first 5 seconds are IGNORED, 
               .Returns(18.8)   // O2 Low bump test reading to PASS LOW BUMP TEST
               .Returns(18.9);  // O2 Low bump test reading to PASS LOW BUMP TEST 
                        
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses[0].UsedGasEndPoints.Count == 2
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.BumpHigh && x.Cylinder.IsFreshAir) != null
                && bump.GasResponses[0].Status == Status.Passed);
        }

        [Fact]
        public void IgnoreO2CALIfFinalHighBumpFailsAndO2ConcIsNot209Or21()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(10.2)   // O2 High Bump Test Reading to FAIL Initial High Bump Test 
               .Returns(12.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.5)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.6)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.7)   // O2 High Bump Test Reading to FAIL FINAL High Bump Test / O2 recovery Purge
               .Returns(14.8)   // Readings within first  5 seconds are IGNORED, 
               .Returns(14.8)   // O2 Low bump test reading to PASS LOW BUMP TEST
               .Returns(14.9);  // O2 Low bump test reading to PASS LOW BUMP TEST 
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].UsedGasEndPoints.Count == 2
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.BumpHigh && x.Cylinder.IsFreshAir) != null
                && bump.HasHighBumpFailCalGasResponses == false     // did NOT use ! to avoid confusion
                && bump.GasResponses[0].IsSecondO2HighBump == false // May NOT be required here
                && bump.GasResponses[0].Status == Status.BumpFailedFreshAirFailed);
        }

        [Fact]
        // This unit covers more than one scenario as follows :
        // 1. CLO2 sensor is bump tested when CL2 cylinder is assigned.
        // 2. Pre conditioning happens for CLO2 sensor
        // 3. Pre Bump and post purge is necessary for CLO2 sensor when CL2 cylinder is assigned.
        // 4. bump test criteria for CLO2 is now verified
        public void CLO2BumpPassedIfCL2GasEndPointAssignedWithPassCriteriaofPointFivePPMOrHigher()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.ClO2.ToString() });

            //Port 3, add CL2 cylinder
            Cylinder cyl = new Cylinder("1810-9336", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.Cl2.ToString()], 10.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(0)     // pre bump purge
               .Returns(0.5)   // Pre conditioning
               .Returns(0.5)   // Readings within first  5 seconds are IGNORED, 
               .Returns(0.5)   // CLO2 reading to PASS BUMP TEST
               .Returns(0.5)   // CLO2 reading to PASS BUMP TEST 
               .Returns(0.5)   // CLO2 reading to PASS BUMP TEST 
               .Returns(0);    // post bump purge
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            // CLO2 has performed BUmp if Cl2 is assigned and Passed the Bump
            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].Status == Status.Passed);
        }

        [Fact]
        // This unit covers more than one scenario as follows :
        // 1. Pre conditioning happens for HCL sensor
        // 2. Pre Bump and post purge is necessary for HCL sensor.
        // 3. bump test criteria for HCL is now verified
        // 4. This test covers preconditioning for HCL ONLY. Pre condition Pass criteria for HCL is full span reserve should be greater than full greater than 50.
        // however for for CLO2 sensors since we're bumping with CL2, We instead just use bump ppm criteria.
        // To check whether preconditioning is being executed for CLO2/CL2 and HCl sensor, this is the ONLY unit test with HCL. DO we have to have for CL2 and CLO2?
        public void PreconditionSensorsWhichRequiresBumpPreconditionCl2OrHCLOrCLO2()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.HCl.ToString() });

            //Port 3, add HCL cylinder
            Cylinder cyl = new Cylinder("1810-6963", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.HCl.ToString()], 10.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(0)     // pre bump purge
               .Returns(6)   // Pre conditioning
               .Returns(6)   // Readings within first  5 seconds are IGNORED, 
               .Returns(6)   // CLO2 reading to PASS BUMP TEST
               .Returns(6)   // CLO2 reading to PASS BUMP TEST 
               .Returns(6)   // CLO2 reading to PASS BUMP TEST 
               .Returns(0);    // post bump purge
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].Status == Status.Passed
               && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Precondition && x.Cylinder.PartNumber == test.GasEndPoints[2].Cylinder.PartNumber) != null);
        }

        [Fact]
        // For HCl BumpPreconditionPauseTime is 120 whereas for CL2 and CLO2 its 60.
        // for other sensors its 0. This unit test covers ONLY for HCl. Let me know if we need to cover for CL2 and CLO2.
        // For more details see sensor.BumpPreconditionPauseTime
        public void PauseGasFlowAfterPreconditioningForSpecificSensors()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.HCl.ToString() });

            //Port 3, add HCL cylinder
            Cylinder cyl = new Cylinder("1810-6963", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.HCl.ToString()], 10.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));

            InitializeForTest(action);

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(0)     // pre bump purge
               .Returns(6)   // Pre conditioning
               .Returns(6)   // Readings within first  5 seconds are IGNORED, 
               .Returns(6)   // CLO2 reading to PASS BUMP TEST
               .Returns(6)   // CLO2 reading to PASS BUMP TEST 
               .Returns(6)   // CLO2 reading to PASS BUMP TEST 
               .Returns(0);    // post bump purge           

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            instrumentController.Verify(x => x.PauseGasFlow(It.IsAny<GasEndPoint>(), 120), Times.Once);
        }

        [Fact]
        public void VerifyPreBumpPurgeApplicableForToxicOrAspiratedInstruments()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.NH3.ToString() });

            //Port 3, add CL2 cylinder
            Cylinder cyl = new Cylinder("1810-7516", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.NH3.ToString()], 25.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));
                        
            action.Instrument.BumpTimeout = 1; // To save time since we are failing the bump

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].Status == Status.Failed
                && bump.UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Purge && x.Cylinder.IsFreshAir) != null);

            instrumentController.Verify(x => x.GetSensorLowAlarm(It.IsAny<int>(), It.IsAny<double>()), Times.Once);
        }

        [Fact]
        public void VerifyPostBumpPurgeApplicableForToxicOrAspiratedInstruments()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.NH3.ToString() });

            //Port 3, add CL2 cylinder
            Cylinder cyl = new Cylinder("1810-7516", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.NH3.ToString()], 25.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));

            action.Instrument.BumpTimeout = 1; // To save time since we are failing the bump

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].Status == Status.Failed
                && bump.UsedGasEndPoints.Count == 2
                && bump.UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Purge && x.Cylinder.IsFreshAir) != null);
        }

        [Fact]
        public void VerifyPostBumpPurgeApplicableForPurgeGasAfterBumpConfiguration()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.CO.ToString() });
            
            action.Instrument.BumpTimeout = 1;
            action.DockingStation.PurgeAfterBump = true;
            
            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].Status == Status.Failed
                && bump.UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Purge && x.Cylinder.IsFreshAir) != null);

            instrumentController.Verify(x => x.GetSensorLowAlarm(It.IsAny<int>(), It.IsAny<double>()), Times.Once);
        }

        [Fact]
        public void VerifySetSensorBumpFault()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.CO.ToString() });
            bool sensorBumpStatus = ((Sensor)action.Instrument.InstalledComponents[0].Component).BumpTestStatus;
                        
            action.Instrument.BumpTimeout = 5; // To save time since we are failing the bump

            InitializeForTest(action);
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].Status == Status.Failed
                && ((Sensor)test.Instrument.InstalledComponents[0].Component).BumpTestStatus != sensorBumpStatus);

            instrumentController.Verify(x => x.SetSensorBumpFault(It.IsAny<int>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public void ThrowFlowFailedExceptionIfBadPumpTubingIsDetected()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.CO.ToString() });

            InitializeForTest(action);
            
            pumpManager.Setup(x => x.IsBadPumpTubing()).Returns(true);
            
            // act and assert            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            Assert.Throws<FlowFailedException>(() => test.Execute());
            Assert.True(Master.Instance.SwitchService.BadPumpTubingDetectedDuringBump);
        }

        [Fact]
        public void ThrowFailedBumpTestExceptionIfAnyKindOfUnhandledExceptions()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.CO.ToString() });

            InitializeForTest(action);

            consoleService.Setup(x => x.GetSensorLabel(It.IsAny<string>())).Throws(new Exception());

            masterService.ConsoleService = consoleService.Object;

            // act and assert            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            Assert.Throws<FailedBumpTestException>(() => test.Execute());
        }

        [Fact]
        // This unit covers more than one scenario as follows :
        // 1. CLO2 sensor is bump tested when CL2 cylinder is assigned.
        // 2. Pre conditioning happens for CLO2 sensor
        // 3. Pre Bump and post purge is necessary for CLO2 sensor when CL2 cylinder is assigned.
        // 4. bump test criteria for CLO2 is now verified
        public void VerifyCylinderSwitchPurgeApplicableForMoreThanOnePass()
        {
            // arrange           
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.H2S.ToString(), GasCode.SO2.ToString() });

            //Port 3, add CL2 cylinder
            Cylinder cyl = new Cylinder("1810-1220", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.SO2.ToString()], 10.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 3, GasEndPoint.Type.Manual));

            InitializeForTest(action);
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 2 && bump.GasResponses[0].Status == Status.Failed && bump.GasResponses[1].Status == Status.Failed
               && bump.UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Purge && x.Cylinder.IsFreshAir) != null);
        }

        [Fact]
        public void PerformCalIfFinalO2HighBumpFailsAndO2ConcIs21or20P9()
        {
            // arrange           
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            ((Sensor)action.Instrument.InstalledComponents[0].Component).CalibrationGasConcentration = 21;

            InitializeForTest(action);
            
            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses[0].Status == Status.BumpFailedFreshAirFailed
                && bump.HasHighBumpFailCalGasResponses
                && bump.HighBumpFailCalGasResponses.Count == 1);

            instrumentController.Verify(x => x.GetSensorZeroingStatus(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        // TODO update Assert
        public void PerformSecondHighBumpFor30SecsIfCalIsPassed()
        {
            // arrange          
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            ((Sensor)action.Instrument.InstalledComponents[0].Component).CalibrationGasConcentration = 20.9;

            InitializeForTest(action);

            // For Cal
            instrumentController.Setup(x => x.GetSensorZeroingStatus(It.IsAny<int>())).Returns(true);
            instrumentController.Setup(x => x.IsSensorCalibrationEnabled(It.IsAny<InstalledComponent>())).Returns(true);
            instrumentController.Setup(x => x.SetCalibrationGasConcentration(It.IsAny<InstalledComponent>(), It.IsAny<GasEndPoint>())).Returns(20.9);
            instrumentController.Setup(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>())).Returns(20.1);
            instrumentController.Setup(x => x.GetSensorCalibrationStatus(It.IsAny<int>())).Returns(true);
            instrumentController.Setup(x => x.IsSensorCalibrating(It.IsAny<int>())).Returns(false);
            instrumentController.SetupSequence(x => x.GetSensorLastCalibrationTime(It.IsAny<int>()))
                   .Returns(DateTime.Now.AddMonths(-1))
                   .Returns(DateTime.Now);
            instrumentController.Setup(x => x.GetSensorSpanCoeff(It.IsAny<int>())).Returns(1);
            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(100);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses[0].Status == Status.BumpFailedFreshAirFailed
                && bump.HasHighBumpFailCalGasResponses
                && bump.HighBumpFailCalGasResponses.Count == 1);

            instrumentController.Verify(x => x.IsSensorCalibrating(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void ConfirmIfCalResultsAreMarkedAsSkippedForEnabledNonO2Sensors()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString(), GasCode.CO.ToString() });

            // Overidding for O2 cal to select FRESH air gas end point
            ((Sensor)action.Instrument.InstalledComponents[0].Component).CalibrationGasConcentration = 20.9;

            InitializeForTest(action);

            // For Cal
            instrumentController.Setup(x => x.GetSensorZeroingStatus(It.IsAny<int>())).Returns(true);
            instrumentController.Setup(x => x.IsSensorCalibrationEnabled(It.IsAny<InstalledComponent>())).Returns(true);
            instrumentController.Setup(x => x.SetCalibrationGasConcentration(It.IsAny<InstalledComponent>(), It.IsAny<GasEndPoint>())).Returns(20.9);
            instrumentController.Setup(x => x.GetSensorCalibrationReading(It.IsAny<int>(), It.IsAny<double>())).Returns(20.1);
            instrumentController.Setup(x => x.GetSensorCalibrationStatus(It.IsAny<int>())).Returns(true);
            instrumentController.Setup(x => x.IsSensorCalibrating(It.IsAny<int>())).Returns(false);
            instrumentController.SetupSequence(x => x.GetSensorLastCalibrationTime(It.IsAny<int>()))
                   .Returns(DateTime.Now.AddMonths(-1))
                   .Returns(DateTime.Now);
            instrumentController.Setup(x => x.GetSensorSpanCoeff(It.IsAny<int>())).Returns(1);
            instrumentController.Setup(x => x.GetSensorSpanReserve(It.IsAny<int>())).Returns(100);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses[0].Status == Status.Failed
                && bump.GasResponses[1].Status == Status.BumpFailedFreshAirFailed
                && bump.HasHighBumpFailCalGasResponses
                && bump.HighBumpFailCalGasResponses.Count > 1
                && bump.HighBumpFailCalGasResponses.FirstOrDefault(x => x.SensorCode != SensorCode.O2 && x.Status == Status.Skipped) != null);

            instrumentController.Verify(x => x.IsSensorCalibrating(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        // This can also be covered by the  commented Asset on "BumpFailedFreshAirFailed" unit test case. However I have FAILED initial High BUmp and 
        // Passed Final high bump here so that the scenario is a bit different from "BumpFailedFreshAirFailed" unit test case. But I believe that we
        // do NOT need a unit test for a scenario where if reading on FINAL HIGH BUMP is higher than 20 and less than 22 is a high PASS CRITERIA.
        // Because thats what I have done here.
        public void ThrowCorrectBumpGasUnavailableForO2BumpIfCylinderWithO2ConcIsAbove19Vol()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            InitializeForTest(action);

            if (action.GasEndPoints.Count > 1)
                action.GasEndPoints.RemoveAt(1);

            //Port 3, add Methane cylinder
            Cylinder cyl = new Cylinder("1810-0289", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.O2.ToString()], 209000.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 2, GasEndPoint.Type.Manual));

            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);

            Assert.Throws<CorrectBumpTestGasUnavailable>(() => operation.Execute());
        }

        [Fact]
        public void BumpSkippedIfInstalledComponentIsNotAPartOfCoponentSpecificBump()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.ClO2.ToString() });
            action.ComponentCodes.Add("S0001");

            InitializeForTest(action);

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].Status == Status.Skipped);
        }


        [Fact]
        public void BumpPassedFreshAirPassedForO2SensorWhenCylinderConcentrationIsLessThan18Vol()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string>() { GasCode.O2.ToString() });

            InitializeForTest(action);

            if (action.GasEndPoints.Count > 1)
                action.GasEndPoints.RemoveAt(1);

            //Port 3, add Methane cylinder
            Cylinder cyl = new Cylinder("1810-5635", "ISC") { ExpirationDate = DateTime.Today.AddDays(30), Pressure = PressureLevel.Full };
            cyl.GasConcentrations.AddRange(new List<GasConcentration>() { new GasConcentration(GasType.Cache[GasCode.O2.ToString()], 160000.00),
                                                                         new GasConcentration(GasType.Cache[GasCode.Methane.ToString()], 25000.00),
                                                                         new GasConcentration(GasType.Cache[GasCode.CO.ToString()], 100.00),
                                                                         new GasConcentration(GasType.Cache[GasCode.H2S.ToString()], 50.00) });

            action.GasEndPoints.Add(new GasEndPoint(cyl, 2, GasEndPoint.Type.Manual));

            instrumentController.SetupSequence(x => x.GetSensorReading(It.IsAny<int>(), It.IsAny<double>()))
               .Returns(20.2)   // O2 High Bump Test Reading to Pass Initial High Bump Test so that O2 recovery purge DOES NOT happen
               .Returns(10.5)   // Readings within first  5 seconds are IGNORED, 
               .Returns(11.5)   // Readings within first  5 seconds are IGNORED, 
               .Returns(12.5);  // O2 Low bump test reading to PASS LOW BUMP TEST

            InstrumentBumpTestOperation test = new InstrumentBumpTestOperation(action);

            InstrumentBumpTestEvent bump = (InstrumentBumpTestEvent)test.Execute();

            Assert.True(bump.GasResponses.Count == 1 && bump.GasResponses[0].UsedGasEndPoints.Count == 1  // To ensure that O2 recovery Purge did NOT happen
                && bump.GasResponses[0].UsedGasEndPoints.FirstOrDefault(x => x.Usage == CylinderUsage.Bump && x.Cylinder.PartNumber == test.GasEndPoints[1].Cylinder.PartNumber) != null
                && bump.GasResponses[0].Status == Status.Passed);
        }
    }
}
