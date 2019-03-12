using System;
using System.Collections.Generic;


namespace ISC.iNet.DS.DomainModel
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides classes with base functionality needed to define an instrument action.
	/// </summary>
	public abstract class InstrumentAction : DockingStationAction
	{
		#region Constructors

		/// <summary>
		/// Creates a new instance of a generic InstrumentAction class.
		/// </summary>
		public InstrumentAction()
		{
			// Do nothing
		}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentAction"></param>
        public InstrumentAction( InstrumentAction instrumentAction) : base( instrumentAction )
        {
            this.ScheduleProperties = instrumentAction.ScheduleProperties;
            this.Instrument = instrumentAction.Instrument;
        }

		#endregion

		#region Properties
        private List<ScheduleProperty> _scheduleProperties;
        private int _bumpThreshold;
        private int _bumpTimeout;

        /// <summary>
        /// Gets or sets the list of scheduled properties that are to be exercised in this action.
        /// </summary>
        public List<ScheduleProperty> ScheduleProperties
        {
            get
            {
                if ( _scheduleProperties == null )
                {
                    _scheduleProperties = new List<ScheduleProperty>();
                }
                return _scheduleProperties;
            }
            set { _scheduleProperties = value; }
        }

        /// <summary>
        /// Specifies what gas reading (% of concentration) that instrument needs to
        /// see in order for a bump test to pass.
        /// </summary>
        public int BumpThreshold
        {
            get
            {
                return _bumpThreshold;
            }
            set
            {
                _bumpThreshold = value;
            }
        }

        /// <summary>
        /// Specifies maximum amount of time a bump test may take
        /// before it times out and fails.
        /// </summary>
        public int BumpTimeout
        {
            get
            {
                return _bumpTimeout;
            }
            set
            {
                _bumpTimeout = value;
            }
        }

        private ISC.iNet.DS.DomainModel.Instrument _instrument;
        public ISC.iNet.DS.DomainModel.Instrument Instrument
        {
            get
            {
                return _instrument;
            }
            set
            {
                _instrument = value;
            }
        }
		#endregion

		#region Methods


		#endregion

	}

}
