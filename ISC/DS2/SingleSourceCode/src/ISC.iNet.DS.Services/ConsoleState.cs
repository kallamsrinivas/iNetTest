using System;


namespace ISC.iNet.DS.Services
{
    /// <summary>
    /// Enumerates the states of the display console.
    /// </summary>
    public enum ConsoleState
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // WHENEVER ANY NEW STATES ARE ADDED HERE, REMEMBER TO ALSO DECIDE IF THE NEW STATE IS //
        // CONSIDERED A "BUSY" STATE, AND UPDATE ConsoleService.IsBusyState APPROPRIATELY.     //
        /// /////////////////////////////////////////////////////////////////////////////////////
        None,
        NotSerialized,
        UnsupportedInstrument,
        UnsupportedSoftware,
        Starting,
        Ready,
        Menu,
        Discovering,
        Diagnosing,
        UpdatingData,
        Reset,
        Unavailable,
        LeakUnavailable,
        UnavailableGas,
        UnsupportedCylinder1,
        UnsupportedCylinder2,
        UnsupportedCylinder3,
        CheckingGas,
        CalibratingInstrument,
        BumpingInstrument,
        DownloadingInstrumentDatalog,
        ClearingInstrumentDatalog,
        ClearingInstrumentAlarmEvents,
        DiagnosingInstrument,
        UpdatingInstrumentData,
        CalibrationFailure,
        BumpFailure,
        BumpStoppedCheckTubing,
        CalibrationStoppedCheckTubing,
        UndockedInstrument,
        DownloadingInstrumentAlarmEvents,
        DownloadingInstrumentManualGasOperations,
        ClearingInstrumentManualGasOperations,
        ReadingData,
        ReadingInstrumentData,
        InteractiveDiagnostics,
        UnserializedInstrument,
        LidError,
        ConfigurationError,
        SensorError,
		SensorMissing,
        UnregisteredInstrument,
        UpgradingFirmware,
        UpgradingInstrumentFirmware,
        UpgradingInstrumentError,
        UploadingDebugLog,
        UploadingDatabase,
        PleaseTurnOn,
        InstrumentSystemAlarm,
        Synchronization,
        SynchronizationError,
        PerformingMaintenance,
        ManualCalibrationRequired,
        ManualBumpTestRequired,
        MfgNotConnected,
        MfgConnected, 
        FlipperAndLidError,
        HardwareConfigError,
        DataDownloadPause,
        NoEnabledSensors,
		Troubleshoot,
		CylinderPressureReset,
		ReturnInstrument,
		ReturnDisabledInstrument,
		ReturnDockingStation,
		PrinterError,
		IGasError,
        InstrumentNotReady,
        ContactISCCode1011,             // INS-8446 RHP v7.6 System Error Code 0x01
        ContactISCCode1012,             // INS-8446 RHP v7.6 System Error Code 0x02
        ContactISCCode1014,             // INS-8446 RHP v7.6 System Error Code 0x04
        ContactISCCode1018,             // INS-8446 RHP v7.6 System Error Code 0x08
        ContactISCCode10110,            // INS-8446 RHP v7.6 System Error Code 0x10
        ContactISCCode10160,            // INS-8446 RHP v7.6 System Error Code 0x60
        ConnectZeroAirToPort1,
        ConnectFreshAirToPort1,
        ConnectFreshAirOrZeroAirToPort1,
        ConnectToZeroAirCylinder,
        ReplaceCylinder,
        ExpiredCylinder,
        LowCylinder,
        CheckCylinderConnections,
        BumpFailureCheckGasConnection
        /////////////////////////////////////////////////////////////////////////////////////////
        // WHENEVER ANY NEW STATES ARE ADDED HERE, REMEMBER TO ALSO DECIDE IF THE NEW STATE IS //
        // CONSIDERED A "BUSY" STATE, AND UPDATE ConsoleService.IsBusyState APPROPRIATELY.     //
        /// /////////////////////////////////////////////////////////////////////////////////////
    }
}
