using System;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
	/// <summary>
	/// Summary description for InstrumentAlarmEventsDownloadOperation.
	/// </summary>
	public class InstrumentAlarmEventsDownloadOperation : InstrumentAlarmEventsDownloadAction, IOperation
	{
        #region Constructors

        /// <summary>
        /// Creates a new instance of InstrumentAlarmEventsDownloadOperation class.
        /// </summary>
        public InstrumentAlarmEventsDownloadOperation() {}

        public InstrumentAlarmEventsDownloadOperation( InstrumentAlarmEventsDownloadAction instrumentAlarmEventsDownloadAction )
            : base( instrumentAlarmEventsDownloadAction ) { }

        #endregion

        #region Methods

        /// <summary>
        /// Executes an instrument read settings operation.
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            Stopwatch stopwatch = Log.TimingBegin("ALARM EVENT DOWNLOAD");

            InstrumentAlarmEventsDownloadEvent instrumentAlarmEventsDownloadEvent = new InstrumentAlarmEventsDownloadEvent(this);
            instrumentAlarmEventsDownloadEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();
            instrumentAlarmEventsDownloadEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();

            // Retrieve the events.
            using ( InstrumentController instrumentController = Master.Instance.SwitchService.InstrumentController )
            {
                // Open the serial port connection needed to communicate with the instrument.
                instrumentController.Initialize();

                Log.Debug( "ALARM EVENTS: Downloading" );

                instrumentAlarmEventsDownloadEvent.AlarmEvents = instrumentController.GetAlarmEvents();

                Log.Debug( "ALARM EVENTS: " + instrumentAlarmEventsDownloadEvent.AlarmEvents.Length + " events downloaded." );

            } // end-using
            
            // Need to fill in the instrument serial number on our own.
            // At the same time, format up details for each alarm event.
            foreach ( AlarmEvent alarmEvent in instrumentAlarmEventsDownloadEvent.AlarmEvents )
            {
                alarmEvent.InstrumentSerialNumber = instrumentAlarmEventsDownloadEvent.DockedInstrument.SerialNumber;
            }

            Log.TimingEnd("ALARM EVENT DOWNLOAD",stopwatch);

            return instrumentAlarmEventsDownloadEvent;  // Return the populated event.
        }
			
        #endregion

	} // end-class

} // end-namespace
