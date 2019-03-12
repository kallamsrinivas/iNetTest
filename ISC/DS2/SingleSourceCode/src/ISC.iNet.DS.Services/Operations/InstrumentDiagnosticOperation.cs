using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
	/// <summary>
	/// Provides functionality to perform instrument diagnostic tests.
	/// </summary>
	public class InstrumentDiagnosticOperation : InstrumentDiagnosticAction,  IOperation
	{
		/// <summary>
		/// Creates an instance of a generic InstrumentDiagnosticOperation object.
		/// </summary>
		public InstrumentDiagnosticOperation() {}

        /// <summary>
        /// Creates an instance of a generic InstrumentDiagnosticOperation object.
        /// </summary>
        public InstrumentDiagnosticOperation( InstrumentDiagnosticAction instrumentDiagnosticAction ) : base( instrumentDiagnosticAction  ) {}

		/// <summary>
		/// Executes an instrument diagnostic operation.
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            Stopwatch stopwatch = Log.TimingBegin("INSTRUMENT DIAGNOSTICS");

			InstrumentDiagnosticEvent instrumentDiagnosticEvent = new InstrumentDiagnosticEvent(this);
            instrumentDiagnosticEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();
            instrumentDiagnosticEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();
			// Open the serial port connection needed to communicate with the instrument.
            using ( InstrumentController instrumentController = Master.Instance.SwitchService.InstrumentController )
            {
                instrumentController.Initialize( InstrumentController.Mode.Batch );

                // Cache the diagnostic start time and instrument serial numbers,
                // these will be used during each diagnostic object's instantiation.
                DateTime diagnosticTime = DateTime.UtcNow;
                string instSn = instrumentDiagnosticEvent.DockedInstrument.SerialNumber;

                // Download the "General diagnostics" from the instrument.
                // The diagnostics that are returned by the instrument will be different and based on the instrument's type.	
                GeneralDiagnostic generalDiagnostic = new GeneralDiagnostic( instSn, diagnosticTime );

                generalDiagnostic.Items = instrumentController.GetGeneralDiagnosticProperties();

                foreach ( GeneralDiagnosticProperty gdp in generalDiagnostic.Items )
                    Log.Debug( "GeneralDiagnosticProperty " + gdp.Name + "=" + gdp.Value );

                instrumentDiagnosticEvent.Diagnostics.Add( generalDiagnostic );

                // Download instrument error log.

                ErrorDiagnostic[] errors =  instrumentController.GetInstrumentErrors();

                List<CriticalError> criticalErrors = new List<CriticalError>();

                // we don't need to bother querying the database if there were no errors on
                // the instrument that need checked against the database.
                if ( errors.Length > 0 )
                {
                    // INS-4236, 9/10/2014 - only populate criticalErrors list for MX6 instruments.
                    // MX6 instruments due not have a "current error" register that can be read when it's
                    // docked to determine if it's currently in a error state.  But other instruments do.
                    // For those instruments that do have this register, we read the register during discovery
                    // and the docking station will go to "instrument error" state if it's no zero.
                    // Since we can't do that for MX6, the best we can do is read its error log containing
                    // historical errors.  For each error in the log, we compare to list of errors we
                    // have in our database that are considered "critical".  If we find a match,
                    // then we set a flag which will cause the docking station to go the "instrument error"
                    // state when this operation returns the event.

                    //INS-7715- Need to check if the DS belongs to the Service account, if so fetch all configured errors and compare with the instrument errors.
                    //If any matches with the errors, then set instrument marked as in critical error.
                    if ( ( instrumentDiagnosticEvent.DockedInstrument.Type == DeviceType.MX6 ) || Configuration.IsRepairAccount() )
                    {
                        criticalErrors = criticalErrorsList;
                        //criticalErrors = new CriticalErrorDataAccess().FindAll();

                        Log.Debug( string.Format( "{0} {1} critical errors loaded from database.",
                            criticalErrors.Count, instrumentDiagnosticEvent.DockedInstrument.Type ) );
                    }
                }

                bool foundCrticalErrorInInstrument = false;
                string criticalErroCodeIdentified = string.Empty;       // INS-8446 RHP v7.6

                foreach ( ErrorDiagnostic error in errors )
                {
					// these values are needed so they can be uploaded to iNet
					error.SerialNumber = instSn;
					error.Time = diagnosticTime;

                    instrumentDiagnosticEvent.Diagnostics.Add( error );

                    Log.Debug( "InstrumentError " + error.Code + " on " + Log.DateTimeToString( error.ErrorTime ) );

                    // Errors that are logged in the instrument will be compared to the list of critical errors
                    // downloaded from iNet. If any error code matches then instrument marked as in critical error.
                    // This list will default to empty for non-MX6 instruments, so it will never find anything, on purpose.
                    // Exception to that it loads all critical errors for Service accounts for any instrument types - INS-7715.
                    if ( criticalErrors.Exists ( ce => ce.Code == error.Code) )
					{
						foundCrticalErrorInInstrument = true;
                        criticalErroCodeIdentified = error.Code.ToString();
						Log.Warning( string.Format( "CRITICAL ERROR {0} LOGGED BY INSTRUMENT ON {1}", error.Code, Log.DateTimeToString( error.ErrorTime ) ) );
					}
                }

                if ( foundCrticalErrorInInstrument )
                {
                    //if critical errors found then errors will NOT be cleared from the instrument. Setting the "InstrumentInCriticalError" 
                    //property to TRUE will make the docking station to set to "Instrument Error" state.
                    instrumentDiagnosticEvent.InstrumentInCriticalError = true;
                    // Setting the "InstrumentCriticalErrorCode" property to hold the Error Code will make the docking station 
                    // display the Error Code on its LCD during "Instrument Error" state. INS-8446 RHP v7.6
                    instrumentDiagnosticEvent.InstrumentCriticalErrorCode = criticalErroCodeIdentified;
                }
                else
                {
                    //if no crtical error found then we will clear the errors from the instrument.
                    instrumentController.ClearInstrumentErrors();
                }
            } // end-using instrumentController

            Log.TimingEnd("INSTRUMENT DIAGNOSTICS",stopwatch);

			return instrumentDiagnosticEvent;
		}

	} // end-class InstrumentDiagnosticOperation
}
