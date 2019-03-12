using System.Diagnostics;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    using ISC.iNet.DS.DomainModel; // puting this here avoids compiler's confusion of DomainModel.Instrument vs Instrument.Driver.

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to read instrument settings.
	/// </summary>
	public class InstrumentSettingsReadOperation : InstrumentSettingsReadAction , IOperation
	{
		
		#region Fields

		#endregion		
		
		#region Constructors

		/// <summary>
		/// Creates a new instance of InstrumentSettingsReadOperation class.
		/// </summary>
        public InstrumentSettingsReadOperation() {}

        public InstrumentSettingsReadOperation( InstrumentSettingsReadAction instrumentSettingsReadAction )
            : base( instrumentSettingsReadAction ) { }

		#endregion

		#region Methods

		/// <summary>
		/// Executes an instrument settings read operation.
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            Stopwatch stopwatch = Log.TimingBegin("INSTRUMENT SETTINGS READ");

			InstrumentSettingsReadEvent instrumentSettingsReadEvent;

            if ( !Master.Instance.ControllerWrapper.IsDocked() ) // Check that instrument is still docked.
				throw new InstrumentNotDockedException();
			
			// Create the return event.
			instrumentSettingsReadEvent = new InstrumentSettingsReadEvent(this);

			// Retrieve the docking station's information.
            instrumentSettingsReadEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();
            instrumentSettingsReadEvent.DockedTime = Master.Instance.SwitchService.DockedTime;

			// Obtain a clone of the cached instrument information
			instrumentSettingsReadEvent.DockedInstrument = (Instrument)Master.Instance.SwitchService.Instrument.Clone();
			
            // NOTE: we are choosing to keep the InstrumentSettingsReadOperation object at this time to avoid having 
            // to rework the scheme where the saving and reporting of this operation cause the upload of information
            // to iNet.  Since ALL instrument information is obtained during discovery upon docking, we could, in time, 
            // figure out a way to upload that information then, which would allow for this action/operation/event to 
            // be eliminated. But, we are choosing not to take the re-plumbing step at this time.

            // If this ReadEvent is occurring after an Update Event, then SettngsRefId will contain
            // the ID of the settings used during the Update Event.  Otherwise, will be Nullid.
            // We place it into the read event's DockingStation in order to uploading it to iNet
            instrumentSettingsReadEvent.DockedInstrument.RefId = this.SettingsRefId;

            Log.TimingEnd("INSTRUMENT SETTINGS READ",stopwatch);

			return instrumentSettingsReadEvent;
		}

        #endregion			
					
	}
}