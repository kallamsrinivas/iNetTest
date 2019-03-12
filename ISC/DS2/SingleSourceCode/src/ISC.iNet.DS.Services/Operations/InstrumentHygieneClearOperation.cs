using System;
using System.Diagnostics;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to clear datalog data.
	/// </summary>
	public class InstrumentDatalogClearOperation : InstrumentDatalogClearAction , IOperation
	{		
		#region Fields

		#endregion

		#region Constructors
		
		/// <summary>
		///	Constructor
		/// </summary>
        public InstrumentDatalogClearOperation() {}

        public InstrumentDatalogClearOperation( InstrumentDatalogClearAction instrumentDatalogClearAction )
            : base( instrumentDatalogClearAction ) { }

		#endregion

		#region Methods

		/// <summary>
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            Stopwatch stopwatch = Log.TimingBegin("DATALOG CLEAR");

            InstrumentDatalogClearEvent datalogClearEvent = new InstrumentDatalogClearEvent(this);
            datalogClearEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();
            datalogClearEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();

            using ( InstrumentController instrumentController = Master.Instance.SwitchService.InstrumentController )
            {
                instrumentController.Initialize();
                datalogClearEvent.SessionsCleared = 0;
                datalogClearEvent.SessionsCleared = instrumentController.ClearDatalog();
            } // end-using

            Log.TimingEnd("DATALOG CLEAR", stopwatch);

			return datalogClearEvent;
		}
	
		#endregion
	}
}