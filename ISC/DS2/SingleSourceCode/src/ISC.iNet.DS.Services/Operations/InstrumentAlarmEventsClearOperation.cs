using System.Diagnostics;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
	/// <summary>
	/// Provides functionality to clear an instrument's "alarm event" data.
	/// </summary>
	public class InstrumentAlarmEventsClearOperation : InstrumentAlarmEventsClearAction , IOperation
	{		
		#region Fields

		#endregion

		#region Constructors
		
		/// <summary>
		///	Constructor
		/// </summary>
        public InstrumentAlarmEventsClearOperation() {}

        public InstrumentAlarmEventsClearOperation( InstrumentAlarmEventsClearAction instrumentAlarmEventsClearAction )
            : base( instrumentAlarmEventsClearAction ) { }

		#endregion

		#region Methods

		/// <summary>
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            Stopwatch stopwatch = Log.TimingBegin("ALARM EVENT CLEAR");

            InstrumentAlarmEventsClearEvent clearEvent = new InstrumentAlarmEventsClearEvent( this );

            clearEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();
            clearEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();

            using ( InstrumentController instrumentController = Master.Instance.SwitchService.InstrumentController )
            {
                instrumentController.Initialize();
                Log.Debug( "Clearing alarm events" );
                instrumentController.ClearAlarmEvents();

            } // end-using

            Log.TimingEnd("ALARM EVENT CLEAR",stopwatch);

			return clearEvent;
		}
	
		#endregion
	}
}
