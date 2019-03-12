using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ISC.iNet.DS.UnitTests
{

    public class SchedulerMockTests
    {

        #region [ Fields ]

        Scheduler scheduler = null;
        DockingStation dockingStation = null;
        DockingStationAction nextAction = null;
        DockingStationEvent dsEvent = null;
        Instrument instrument = null;
        Master master = null;

        Mock<Schema> schema = null;
        Mock<ISwitchService> switchService = null;
        Mock<ControllerWrapper> controllerWrapper = null;
        Mock<IConsoleService> consoleService = null;
        Mock<IExecuterService> executerService = null;

        Mock<IDataAccessTransaction> _dataAccessTransaction = null;
        Mock<EventJournalDataAccess> _eventJournalDataAccess = null;

        Mock<ScheduledUponDockingDataAccess> _scheduleUponDockingAccess = null;
        Mock<ScheduledOnceDataAccess> _scheduleOnceAccess = null;
        Mock<ScheduledHourlyDataAccess> _scheduleHourlyAccess = null;
        Mock<ScheduledDailyDataAccess> _scheduleDailyAccess = null;
        Mock<ScheduledWeeklyDataAccess> _scheduleWeeklyAccess = null;
        Mock<ScheduledMonthlyDataAccess> _scheduleMonthlyAccess = null;
        Mock<QueueDataAccess> _queueDataAccess = null;

        #endregion

        #region [ Constructors ]

        public SchedulerMockTests()
        {
            _dataAccessTransaction = new Mock<IDataAccessTransaction>();
            
            InitializeJournals();
            InitializeSchedules();

            scheduler = new Scheduler(_dataAccessTransaction.Object, _eventJournalDataAccess.Object, _scheduleUponDockingAccess.Object, _scheduleOnceAccess.Object, _scheduleHourlyAccess.Object, _scheduleDailyAccess.Object, _scheduleWeeklyAccess.Object, _scheduleMonthlyAccess.Object, _queueDataAccess.Object);
        }

        #endregion

        #region [ Private Methods ]

        private void InitializeJournals()
        {
            _eventJournalDataAccess = new Mock<EventJournalDataAccess>();

            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<EventJournal>());
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<EventJournal>());
        }

        private void InitializeSchedules()
        {
            _scheduleUponDockingAccess = new Mock<ScheduledUponDockingDataAccess>();
            _scheduleOnceAccess = new Mock<ScheduledOnceDataAccess>();
            _scheduleHourlyAccess = new Mock<ScheduledHourlyDataAccess>();
            _scheduleDailyAccess = new Mock<ScheduledDailyDataAccess>();
            _scheduleWeeklyAccess = new Mock<ScheduledWeeklyDataAccess>();
            _scheduleMonthlyAccess = new Mock<ScheduledMonthlyDataAccess>();
            _queueDataAccess = new Mock<QueueDataAccess>();
            
            _scheduleUponDockingAccess.Setup(x => x.FindGlobalSchedules(It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleOnceAccess.Setup(x => x.FindGlobalSchedules(It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleHourlyAccess.Setup(x => x.FindGlobalSchedules(It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleDailyAccess.Setup(x => x.FindGlobalSchedules(It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleWeeklyAccess.Setup(x => x.FindGlobalSchedules(It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleMonthlyAccess.Setup(x => x.FindGlobalSchedules(It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());

            _scheduleUponDockingAccess.Setup(x => x.FindGlobalTypeSpecificSchedules(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleOnceAccess.Setup(x => x.FindGlobalTypeSpecificSchedules(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleHourlyAccess.Setup(x => x.FindGlobalTypeSpecificSchedules(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleDailyAccess.Setup(x => x.FindGlobalTypeSpecificSchedules(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleWeeklyAccess.Setup(x => x.FindGlobalTypeSpecificSchedules(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleMonthlyAccess.Setup(x => x.FindGlobalTypeSpecificSchedules(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());

            _scheduleUponDockingAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleOnceAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleHourlyAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleDailyAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleWeeklyAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleMonthlyAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());

            _scheduleUponDockingAccess.Setup(x => x.FindByComponentCodes(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleOnceAccess.Setup(x => x.FindByComponentCodes(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleHourlyAccess.Setup(x => x.FindByComponentCodes(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleDailyAccess.Setup(x => x.FindByComponentCodes(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleWeeklyAccess.Setup(x => x.FindByComponentCodes(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            _scheduleMonthlyAccess.Setup(x => x.FindByComponentCodes(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>());
            
        }
        private void Initialize()
        {
            schema = MockHelper.GetSchemaMock();
            switchService = MockHelper.GetSwitchServiceMock(instrument);
            controllerWrapper = MockHelper.GetControllerMock(dockingStation, instrument);
            consoleService = MockHelper.GetConsoleServiceMock();
            executerService = MockHelper.GetExecuterServiceMock();
        }

        private void CreateMasterForTest()
        {
            if (schema != null)
                Configuration.Schema = schema.Object;

            if (dockingStation != null)
                Configuration.DockingStation = dockingStation;

            master = Master.CreateMaster();
            master.SwitchService = switchService.Object;
            master.ControllerWrapper = controllerWrapper.Object;
            master.ConsoleService = consoleService.Object;
            master.ExecuterService = executerService.Object;
        }
        #endregion

        #region [ Test Methods ]

        [Fact]
        public void DSXNotSynchronized()
        {
            // arrange
            SettingsReadOperation operation = new SettingsReadOperation();
            dsEvent = new SettingsReadEvent(operation);

            Mock<Schema> schema = new Mock<Schema>();
            schema.Setup(x => x.Synchronized).Returns(false);
            Configuration.Schema = schema.Object;

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is NothingAction);

        }

        [Fact]
        public void InitialReadingNeeded()
        {
            // arrange
            SettingsUpdateOperation operation = new SettingsUpdateOperation();
            dsEvent = new SettingsUpdateEvent(operation);
            Initialize();

            switchService.Setup(x => x.InitialReadSettingsNeeded).Returns(true);

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is NothingAction);

        }

        [Fact]
        public void ExecuteFollowUpAction()
        {
            // arrange
            SettingsUpdateOperation operation = new SettingsUpdateOperation();
            dsEvent = new SettingsUpdateEvent(operation);
            Initialize();

            dsEvent.Trigger = TriggerType.Scheduled;

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is CylinderPressureResetAction);
        }

        [Fact]
        public void GetSettingsReadActionAsFollowUpActionForSettingsUpdate()
        {
            // arrange
            SettingsUpdateOperation operation = new SettingsUpdateOperation();
            dsEvent = new SettingsUpdateEvent(operation);
            Initialize();

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is SettingsReadAction);
        }

        [Fact]
        public void GetSettingsReadActionAsFollowUpActionForCylinderPressureReset()
        {
            // arrange
            CylinderPressureResetOperation operation = new CylinderPressureResetOperation();
            dsEvent = new CylinderPressureResetEvent(operation);
            Initialize();

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is SettingsReadAction);
        }

        [Fact]
        public void GetInstrumentDatalogClearActionAsFollowUpActionForInstrumentDatalogDownloadToClearInstrumentSessions()
        {
            // arrange
            InstrumentDatalogDownloadEvent dsEvent = new InstrumentDatalogDownloadEvent();
            dsEvent.InstrumentSessions.Add(new DatalogSession());
            Initialize();

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentDatalogClearAction);
        }

        [Fact]
        public void GetInstrumentDatalogClearActionAsFollowUpActionForInstrumentDatalogDownloadToClearErrors()
        {
            // arrange
            InstrumentDatalogDownloadEvent dsEvent = new InstrumentDatalogDownloadEvent();
            dsEvent.Errors.Add(new DockingStationError("Test Error!"));
            Initialize();

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentDatalogClearAction);
        }

        [Fact]
        public void GetInstrumentAlarmEventClearActionAsFollowUpActionForInstrumentAlarmEventDownloadToClearInstrumentAlarmEvents()
        {
            // arrange
            InstrumentAlarmEventsDownloadOperation operation = new InstrumentAlarmEventsDownloadOperation();
            InstrumentAlarmEventsDownloadEvent dsEvent = new InstrumentAlarmEventsDownloadEvent(operation);
            dsEvent.AlarmEvents = new AlarmEvent[] { new AlarmEvent() };
            Initialize();

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentAlarmEventsClearAction);
        }
        [Fact]
        public void GetInstrumentAlarmEventClearActionAsFollowUpActionForInstrumentAlarmEventDownloadToClearErrors()
        {
            // arrange
            InstrumentAlarmEventsDownloadOperation operation = new InstrumentAlarmEventsDownloadOperation();
            InstrumentAlarmEventsDownloadEvent dsEvent = new InstrumentAlarmEventsDownloadEvent(operation);
            dsEvent.Errors.Add(new DockingStationError("Test Error!"));
            Initialize();

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentAlarmEventsClearAction);
        }

        [Fact]
        public void GetInstrumentManualOperationsClearActionAsFollowUpActionForInstrumentManualOperationsDownloadToClearGasReponses()
        {
            // arrange
            InstrumentManualOperationsDownloadAction action = new InstrumentManualOperationsDownloadAction();
            InstrumentManualOperationsDownloadOperation operation = new InstrumentManualOperationsDownloadOperation(action);
            InstrumentManualOperationsDownloadEvent dsEvent = new InstrumentManualOperationsDownloadEvent(operation);
            dsEvent.GasResponses.Add(new SensorGasResponse());

            Initialize();

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentManualOperationsClearAction);
        }
        [Fact]
        public void GetInstrumentManualOperationsClearActionAsFollowUpActionForInstrumentManualOperationsDownloadToClearErrors()
        {
            // arrange
            InstrumentManualOperationsDownloadAction action = new InstrumentManualOperationsDownloadAction();
            InstrumentManualOperationsDownloadOperation operation = new InstrumentManualOperationsDownloadOperation(action);
            InstrumentManualOperationsDownloadEvent dsEvent = new InstrumentManualOperationsDownloadEvent(operation);
            dsEvent.Errors.Add(new DockingStationError("Test Error!"));

            Initialize();

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentManualOperationsClearAction);
        }

        [Fact]
        public void GetNothingActionDueToInstrumentCriticalError()
        {
            // arrange
            InstrumentDiagnosticAction action = new InstrumentDiagnosticAction();
            InstrumentDiagnosticOperation operation = new InstrumentDiagnosticOperation(action);
            InstrumentDiagnosticEvent dsEvent = new InstrumentDiagnosticEvent(operation);
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            dsEvent.InstrumentInCriticalError = true;

            Initialize();
            CreateMasterForTest();
            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is NothingAction);
        }

        [Fact]
        public void InstrumentInSystemAlarm()
        {
            // arrange
            SettingsReadOperation operation = new SettingsReadOperation();
            dsEvent = new SettingsReadEvent(operation);
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);
            Initialize();

            Configuration.DockingStation = dockingStation;

            switchService.Setup(x => x.IsInstrumentInSystemAlarm).Returns(true);

            CreateMasterForTest();

            // act and assert
            Xunit.Assert.Throws<InstrumentSystemAlarmException>(() => scheduler.GetNextAction(dsEvent));
        }

        [Fact]
        public void InstrumentRequiresOperatorAction()
        {
            // arrange           
            SettingsReadOperation operation = new SettingsReadOperation();
            dsEvent = new SettingsReadEvent(operation);

            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);

            Initialize();

            Sensor sensor = new Sensor();
            sensor.Enabled = true;
            sensor.CalibrationStatus = Status.Failed;
            sensor.Type.Code = SensorCode.O3;

            InstalledComponent installedComponent = new InstalledComponent();
            installedComponent.Position = 1;
            installedComponent.Component = sensor;
            instrument.InstalledComponents.Add(installedComponent);

            Configuration.DockingStation = dockingStation;

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert           
            Xunit.Assert.True(nextAction is ManualCalibrationRequiredAction);
        }

        [Fact]
        public void GetNextForcedAction()
        {
            // arrange 
            SettingsReadOperation operation = new SettingsReadOperation();
            dsEvent = new SettingsReadEvent(operation);

            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);

            Initialize();

            // ScheduledNow nowSched = new ScheduledNow(DomainModelConstant.NullId, DomainModelConstant.NullId, string.Empty, EventCode.GetCachedCode(EventCode.Calibration), null, null, true);
            CreateMasterForTest();
            // act
            scheduler.ForceEvent(EventCode.Calibration, false);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert           
            Xunit.Assert.True(nextAction is InstrumentCalibrationAction);
        }

        [Fact]
        public void GetBadPumpTubingDetectedActionForCalibration()
        {
            // arrange 
            InstrumentManualOperationsDownloadAction action = new InstrumentManualOperationsDownloadAction();
            InstrumentManualOperationsDownloadOperation operation = new InstrumentManualOperationsDownloadOperation(action);
            dsEvent = new InstrumentManualOperationsDownloadEvent(operation);
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            Initialize();

            switchService.Setup(x => x.BadPumpTubingDetectedDuringCal).Returns(true);

            CreateMasterForTest();
            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert 
            Xunit.Assert.True(nextAction is BadPumpTubingDetectedAction);
        }

        [Fact]
        public void GetBadPumpTubingDetectedActionForBump()
        {
            // arrange 
            InstrumentManualOperationsDownloadAction action = new InstrumentManualOperationsDownloadAction();
            InstrumentManualOperationsDownloadOperation operation = new InstrumentManualOperationsDownloadOperation(action);
            dsEvent = new InstrumentManualOperationsDownloadEvent(operation);
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            Initialize();

            switchService.Setup(x => x.BadPumpTubingDetectedDuringBump).Returns(true);

            CreateMasterForTest();

            // act
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert 
            Xunit.Assert.True(nextAction is BadPumpTubingDetectedAction);
        }

        [Fact]
        public void ReturnCalibrationFailureActionForInstrumentWithFailedSensor()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.VPRO, new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2 }, DeviceSubType.VentisPro4);

            foreach (InstalledComponent installedComponent in action.Instrument.InstalledComponents.Where(comp => comp.Component is Sensor))
            {
                Sensor sensor = installedComponent.Component as Sensor;
                sensor.CalibrationStatus = Status.Failed;
            }
            instrument = action.Instrument;
            Initialize();

            CreateMasterForTest();

            // act
            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);
            dsEvent = new InstrumentBumpTestEvent(operation);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert 
            Xunit.Assert.True(nextAction is CalibrationFailureAction);
        }

        [Fact]
        public void ReturnManualBumpTestRequiredActionForInstrumentWithBumpFailedSensor()
        {
            // arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.VPRO, new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O3 }, DeviceSubType.VentisPro4);

            foreach (InstalledComponent installedComponent in action.Instrument.InstalledComponents.Where(comp => comp.Component is Sensor))
            {
                Sensor sensor = installedComponent.Component as Sensor;
                sensor.BumpTestStatus = false;
            }
            instrument = action.Instrument;
            Initialize();

            CreateMasterForTest();

            // act
            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);
            dsEvent = new InstrumentBumpTestEvent(operation);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert 
            Xunit.Assert.True(nextAction is ManualBumpTestRequiredAction);
        }

        [Fact]
        public void GetForcedSettingsReadForDockingStation()
        {
            // arrange            
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            ScheduledNow scheduledNow = new ScheduledNow(0, 0, string.Empty, new EventCode(EventCode.SettingsRead, 1, EquipmentTypeCode.VDS, typeof(SettingsReadAction)), EquipmentTypeCode.VDS, null, false);
            Initialize();
            CreateMasterForTest();

            // act
            dsEvent = new NothingEvent();
            scheduler.StackForcedSchedule(scheduledNow);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is SettingsReadAction);
        }

        [Fact]
        public void GetCalibrationActionforForcedBumpTestOnCalFailedSensors()
        {
            // arrange            
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2, GasCode.CombustibleLEL });
            foreach (InstalledComponent installComp in installedComponents)
            {
                Sensor sensor = installComp.Component as Sensor;
                sensor.CalibrationStatus = Status.Failed;
            }

            instrument.InstalledComponents.AddRange(installedComponents);
            ScheduledNow scheduledNow = new ScheduledNow(0, 0, string.Empty, new EventCode(EventCode.BumpTest, 1, EquipmentTypeCode.Instrument, typeof(InstrumentBumpTestAction)), EquipmentTypeCode.VDS, null, false);
            scheduledNow.SerialNumbers.Add(instrument.SerialNumber);

            Initialize();
            CreateMasterForTest();

            // act
            dsEvent = new InstrumentBumpTestEvent();
            scheduler.StackForcedSchedule(scheduledNow);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is CalibrationFailureAction);
        }

        [Fact]
        public void GetScheduledInstrumentDiagnosticsAction()
        {
            // arrange   
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);
            InstrumentSettingsUpdateOperation operation = new InstrumentSettingsUpdateOperation();
            
            Initialize();
            
            // act
            schema.Setup(x => x.Activated).Returns(true);
            dsEvent = new InstrumentSettingsUpdateEvent(operation);
            CreateMasterForTest();
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentDiagnosticAction);
        }

        [Fact]
        public void GetNothingActionIfNoEventIsScheduled()
        {
            // arrange   
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);
            InstrumentAlarmEventsClearOperation operation = new InstrumentAlarmEventsClearOperation();

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));

            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);

            Initialize();

            // act
            schema.Setup(x => x.Activated).Returns(true);
            dsEvent = new InstrumentAlarmEventsClearEvent(operation);
            CreateMasterForTest();
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is NothingAction);
        }

        [Fact]
        public void GetScheduledInstrumentAlarmEventDownloadAction()
        {
            // arrange   
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);
            InstrumentAlarmEventsDownloadOperation operation = new InstrumentAlarmEventsDownloadOperation();

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.DownloadAlarmEvents), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));
            
            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);

            _scheduleDailyAccess.Setup(x => x.FindGlobalSchedules(It.IsAny<IDataAccessTransaction>())).Returns(new List<Schedule>() { new ScheduledDaily(DomainModelConstant.NullId, DomainModelConstant.NullId, string.Empty, EventCode.GetCachedCode(EventCode.DownloadAlarmEvents), EquipmentTypeCode.Instrument, string.Empty, true, true, 1, DateTime.Now.AddYears(-1), new TimeSpan(9, 0, 0)) });
            
            Initialize();

            // act
            schema.Setup(x => x.Activated).Returns(true);
            dsEvent = new InstrumentAlarmEventsDownloadEvent(operation);
            CreateMasterForTest();
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentAlarmEventsDownloadAction);
        }
    
        [Fact]
        public void ForceCylinderResetEvent()
        {
            // arrange
            dockingStation = Helper.GetDockingStationForTest(DeviceType.SC);
            instrument = Helper.GetInstrumentForTest(DeviceType.SC);
            Initialize();

            // act
            dsEvent = new NothingEvent();
            CreateMasterForTest();
            scheduler.ForceEvent(EventCode.CylinderPressureReset, true);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert 
            Xunit.Assert.True(nextAction is CylinderPressureResetAction);
            Xunit.Assert.True(nextAction.Trigger == TriggerType.Forced);
        }

        [Fact]
        public void ReforceBumpTest()
        {
            // arrange
            dockingStation = Helper.GetDockingStationForTest(DeviceType.TX1);
            instrument = Helper.GetInstrumentForTest(DeviceType.TX1);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.CO });
            instrument.InstalledComponents.AddRange(installedComponents);
            Initialize();

            // act
            dsEvent = new NothingEvent();
            CreateMasterForTest();
            scheduler.ReForceEvent(new InstrumentBumpTestAction() { Trigger = TriggerType.Forced });
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert 
            Xunit.Assert.True(nextAction is InstrumentBumpTestAction);
            Xunit.Assert.True(nextAction.Trigger == TriggerType.Forced);
        }

        [Fact]
        public void ReforceCalibration()
        {
            // arrange
            dockingStation = Helper.GetDockingStationForTest(DeviceType.GBPRO);
            instrument = Helper.GetInstrumentForTest(DeviceType.GBPRO);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO});
            instrument.InstalledComponents.AddRange(installedComponents);
            Initialize();

            // act
            dsEvent = new NothingEvent();
            CreateMasterForTest();
            scheduler.ReForceEvent(new InstrumentCalibrationAction() { Trigger = TriggerType.Forced });
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert 
            Xunit.Assert.True(nextAction is InstrumentCalibrationAction);
            Xunit.Assert.True(nextAction.Trigger == TriggerType.Forced);
        }

        [Fact]
        public void GetCalibrationActionIfNoJournalsExists()
        {
            // arrange   
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2, GasCode.CombustibleLEL });
            instrument.InstalledComponents.AddRange(installedComponents);
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);
            InstrumentManualOperationsClearAction action = new InstrumentManualOperationsClearAction();
            InstrumentManualOperationsClearOperation operation = new InstrumentManualOperationsClearOperation(action);

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));

            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            
            Initialize();

            // act
            schema.Setup(x => x.Activated).Returns(true);
            dsEvent = new InstrumentManualOperationsClearEvent(operation);
            CreateMasterForTest();
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentCalibrationAction);
        }

        [Fact]
        public void GetBumpTestActionIfNoJournalsExists()
        {
            // arrange   
            InstrumentCalibrationAction action = new InstrumentCalibrationAction();
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            List<InstalledComponent> installedComponents=Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2, GasCode.CombustibleLEL });
            instrument.InstalledComponents.AddRange(installedComponents);
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);           

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));
            installedComponents.ForEach(comp => eventJournals.Add(new EventJournal(EventCode.Calibration,comp.Component.Uid, instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true,comp.Position, instrument.SoftwareVersion)));

            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            
            Initialize();

            // act
            CreateMasterForTest();
            schema.Setup(x => x.Activated).Returns(true);
            InstrumentCalibrationOperation operation = new InstrumentCalibrationOperation(action);
            dsEvent = new InstrumentCalibrationEvent(operation);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentBumpTestAction);
        }

        [Fact]
        public void GetCalibrationActionForSensorPositionChange()
        {
            // arrange   
            InstrumentCalibrationAction action = new InstrumentCalibrationAction();
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2, GasCode.CombustibleLEL });
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));
            installedComponents.ForEach(comp => eventJournals.Add(new EventJournal(EventCode.Calibration, comp.Component.Uid, instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, comp.Position, instrument.SoftwareVersion)));
            installedComponents.ForEach(comp => comp.Position++);
            instrument.InstalledComponents.AddRange(installedComponents);
            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            
            Initialize();

            // act
            CreateMasterForTest();
            schema.Setup(x => x.Activated).Returns(true);
            InstrumentCalibrationOperation operation = new InstrumentCalibrationOperation(action);
            dsEvent = new InstrumentCalibrationEvent(operation);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentCalibrationAction);
        }

        [Fact]
        public void GetCalibrationActionForSensorRemoved()
        {
            // arrange   
            InstrumentCalibrationAction action = new InstrumentCalibrationAction();
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2, GasCode.CombustibleLEL });
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));
            installedComponents.ForEach(comp => eventJournals.Add(new EventJournal(EventCode.Calibration, comp.Component.Uid, instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, comp.Position, instrument.SoftwareVersion)));
            installedComponents.Remove(installedComponents.Last());
            instrument.InstalledComponents.AddRange(installedComponents);
            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);

            Initialize();

            // act
            CreateMasterForTest();
            schema.Setup(x => x.Activated).Returns(true);
            InstrumentCalibrationOperation operation = new InstrumentCalibrationOperation(action);
            dsEvent = new InstrumentCalibrationEvent(operation);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentCalibrationAction);
        }

        [Fact]
        public void GetCalibrationActionForSensorAdded()
        {
            // arrange   
            InstrumentCalibrationAction action = new InstrumentCalibrationAction();
            instrument = Helper.GetInstrumentForTest(DeviceType.VPRO, DeviceSubType.VentisPro4);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2, GasCode.CombustibleLEL });
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX4);

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));
            for (int i = 0; i < 2; i++)
            {
                InstalledComponent comp = installedComponents[i];
                eventJournals.Add(new EventJournal(EventCode.Calibration, comp.Component.Uid, instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, comp.Position, instrument.SoftwareVersion));
            } 
            instrument.InstalledComponents.AddRange(installedComponents);
            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            
            Initialize();

            // act
            CreateMasterForTest();
            schema.Setup(x => x.Activated).Returns(true);
            InstrumentCalibrationOperation operation = new InstrumentCalibrationOperation(action);
            dsEvent = new InstrumentCalibrationEvent(operation);
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentCalibrationAction);
        }

        [Fact]
        public void GetInstrumentFirmwareUpgradeAction()
        {
            // arrange   
            instrument = Helper.GetInstrumentForTest(DeviceType.MX6);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2 });
            instrument.InstalledComponents.AddRange(installedComponents);
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX6);
            dockingStation.SoftwareVersion = "7.6.0.1";

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));
            installedComponents.ForEach(installComp=> eventJournals.Add(new EventJournal(EventCode.Calibration, installComp.Component.Uid, instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, installComp.Position, instrument.SoftwareVersion)));
            installedComponents.ForEach(installComp => eventJournals.Add(new EventJournal(EventCode.BumpTest, installComp.Component.Uid, instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, installComp.Position, instrument.SoftwareVersion)));

            List<Schedule> schedules = new List<Schedule>();
            Schedule instrumentFirmwareUpgrade = new ScheduledOnce(DomainModelConstant.NullId, DomainModelConstant.NullId, string.Empty, EventCode.GetCachedCode(EventCode.InstrumentFirmwareUpgrade), instrument.Type.ToString(), null, true, DateTime.Now.AddYears(-1), new TimeSpan(9, 0, 0));
            instrumentFirmwareUpgrade.ScheduleProperties.Add(new ScheduleProperty(DomainModelConstant.NullId, ScheduleProperty.FirmwareUpgradeVersion, 0, "2.20.01"));
            schedules.Add(instrumentFirmwareUpgrade);
            _scheduleUponDockingAccess.Setup(x => x.FindGlobalTypeSpecificSchedules(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(schedules);
            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);

            Initialize();

            // act
            schema.Setup(x => x.Activated).Returns(true);
            schema.Setup(x => x.AccountNum).Returns("12345");
            schema.Setup(x => x.ServiceCode).Returns("REPAIR");
            controllerWrapper.Setup(x => x.FirmwareVersion).Returns("7.6.0.1");

            CreateMasterForTest();
            dsEvent=new NothingEvent();
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is InstrumentFirmwareUpgradeAction);
        }

        [Fact]
        public void GetDockingStationFirmwareUpgradeAction()
        {
            // arrange   
            instrument = Helper.GetInstrumentForTest(DeviceType.MX6);
            List<InstalledComponent> installedComponents = Helper.GetSensorsForTest(new List<string>() { GasCode.CO, GasCode.H2S, GasCode.O2 });
            instrument.InstalledComponents.AddRange(installedComponents);
            dockingStation = Helper.GetDockingStationForTest(DeviceType.MX6);
            dockingStation.SoftwareVersion = "7.6.0.1";

            List<EventJournal> eventJournals = new List<EventJournal>();
            eventJournals.Add(new EventJournal(EventCode.GetCachedCode(EventCode.InstrumentDiagnostics), instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, instrument.SoftwareVersion));
            installedComponents.ForEach(installComp => eventJournals.Add(new EventJournal(EventCode.Calibration, installComp.Component.Uid, instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, installComp.Position, instrument.SoftwareVersion)));
            installedComponents.ForEach(installComp => eventJournals.Add(new EventJournal(EventCode.BumpTest, installComp.Component.Uid, instrument.SerialNumber, DateTime.Now.AddMonths(-1), DateTime.Now.AddMonths(-1), true, installComp.Position, instrument.SoftwareVersion)));

            List<Schedule> schedules = new List<Schedule>();
            Schedule firmwareUpgrade = new ScheduledOnce(DomainModelConstant.NullId, DomainModelConstant.NullId, string.Empty, EventCode.GetCachedCode(EventCode.FirmwareUpgrade), dockingStation.Type.ToString(), null, true, DateTime.Now.AddYears(-1), new TimeSpan(9, 0, 0));
            firmwareUpgrade.ScheduleProperties.Add(new ScheduleProperty(DomainModelConstant.NullId, ScheduleProperty.FirmwareUpgradeVersion, 0, "7.6.2.1"));
            schedules.Add(firmwareUpgrade);
            _scheduleUponDockingAccess.Setup(x => x.FindGlobalTypeSpecificSchedules(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(schedules);
            _eventJournalDataAccess.Setup(x => x.FindBySerialNumbers(It.IsAny<string[]>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _eventJournalDataAccess.Setup(x => x.FindLastEventByInstrumentSerialNumber(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDataAccessTransaction>())).Returns(eventJournals);
            _queueDataAccess.Setup(x => x.GetCount()).Returns(0);

            Initialize();

            // act
            schema.Setup(x => x.Activated).Returns(true);
            schema.Setup(x => x.AccountNum).Returns("12345");
            schema.Setup(x => x.ServiceCode).Returns("REPAIR");
            controllerWrapper.Setup(x => x.FirmwareVersion).Returns("7.6.0.1");

            CreateMasterForTest();
            dsEvent = new NothingEvent();
            nextAction = scheduler.GetNextAction(dsEvent);

            // assert
            Xunit.Assert.True(nextAction is FirmwareUpgradeAction);
        }
        #endregion

    }
}
