using System;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides base functionality to classes defining an instrument event.	
	/// </summary>
	public abstract class InstrumentEvent : DockingStationEvent
	{
		
		#region Fields

		private Instrument _dockedInstrument;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a generic InstrumentEvent class.
		/// </summary>
        public InstrumentEvent()
        {
            // Do nothing
        }

        public InstrumentEvent( IOperation operation )
            : base( operation )
        {
        }

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the instrument detail associated with the event.
		/// </summary>
		public Instrument DockedInstrument
		{
			get
			{
				if ( _dockedInstrument == null )
					_dockedInstrument = new Instrument();

				return _dockedInstrument;
			}

			set
			{
				if ( value == null )
					_dockedInstrument = null;
				else
					_dockedInstrument = value;
			}
		}

		#endregion

		#region Methods

		#endregion

	}

}
