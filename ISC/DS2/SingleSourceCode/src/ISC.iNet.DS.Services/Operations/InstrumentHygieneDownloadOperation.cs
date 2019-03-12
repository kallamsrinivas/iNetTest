using System;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to perform an instrument datalog download operation.
	/// </summary>
	public class InstrumentDatalogDownloadOperation : InstrumentDatalogDownloadAction , IOperation
	{
		#region Fields			
		
	    private InstrumentDatalogDownloadEvent _returnEvent;	

		#endregion

		#region Constructors

        private void Init()
        {
            _returnEvent = new InstrumentDatalogDownloadEvent( this );
        }

		/// <summary>
		/// </summary>
		/// <param name="commandTable">Command table</param>
        public InstrumentDatalogDownloadOperation()
        {
            Init();    
        }

        public InstrumentDatalogDownloadOperation( InstrumentDatalogDownloadAction instrumentDatalogDownloadAction )
            : base( instrumentDatalogDownloadAction )
        {
            Init();
        }

		#endregion

		#region Methods

		/// <summary>
		/// Executes an instrument datalog download operation.
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            Stopwatch stopwatch = Log.TimingBegin("DATALOG DOWNLOAD");

            _returnEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();
            _returnEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();

            DownloadDatalog();

            Log.TimingEnd( "DATALOG DOWNLOAD", stopwatch );

			return _returnEvent;		
		}

        private void DownloadDatalog()
        {
            using ( InstrumentController instrumentController = Master.Instance.SwitchService.InstrumentController)
            {
                instrumentController.Initialize( InstrumentController.Mode.Batch );

                DateTime startTime = DateTime.UtcNow;

                bool corruptDatalogDetected = false;

                _returnEvent.InstrumentSessions = instrumentController.GetDatalog( out corruptDatalogDetected );

                TimeSpan elapsedTime = DateTime.UtcNow - startTime;

                int corruptSessionsCount = 0;

                // For each corrupted session, upload an error to inet.
                foreach ( DatalogSession session in _returnEvent.InstrumentSessions )
                {
                    if ( session.CorruptionException != null )
                    {
                        corruptSessionsCount++;
                        // DO NOT CHANGE THE FOLLOWING STRING. THE SERVER IS
                        // PARSING UPLOADED ERRORS LOOKING FOR THIS PHRASING....
                        string msg = string.Format( "Corrupt hygiene encountered! - Session {0}\n{1}", Log.DateTimeToString( session.Session ), session.CorruptionException );
                        Log.Warning( msg );
                        _returnEvent.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning, _returnEvent.DockedInstrument.SerialNumber) );
                    }
                }

                Log.Debug( string.Format( "DATALOG: {0} sessions successful downloaded from instrument.", _returnEvent.InstrumentSessions.Count ) );
                Log.Debug( string.Format( "DATALOG: {0} of those are partial sessions due to corruption.", corruptSessionsCount ) );
                Log.Debug( string.Format( "DATALOG: Corruption detected: " + corruptDatalogDetected.ToString() ) );

                // If we have zero sessions marked as corrupted, but corruptHygieneDetected is set to true,
                // that means the datalog was so corrupted, that a session object couldn't even be created out of it.
                // Upload a error.
                if ( corruptSessionsCount == 0 && corruptDatalogDetected == true )
                {
                    Log.Debug( "DATALOG: One or more sessions were completely lost due to corruption. " );
                    // DO NOT CHANGE THE FOLLOWING STRING. THE SERVER IS
                    // PARSING UPLOADED ERRORS LOOKING FOR THIS PHRASING....
                    _returnEvent.Errors.Add( new DockingStationError( "Corrupt hygiene encountered!", DockingStationErrorLevel.Warning, _returnEvent.DockedInstrument.SerialNumber ) );
                }

                Log.Debug( string.Format( "DATALOG: Overall time to download & process: {0}.", elapsedTime ) );

            }  // end-using
        }

		#endregion		

	}  // end-class

}  // end-namespace