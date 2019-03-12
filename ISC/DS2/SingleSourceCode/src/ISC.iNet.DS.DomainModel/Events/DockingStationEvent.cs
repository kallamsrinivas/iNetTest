using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides base functionality to classes defining a docking station event.
	/// </summary>
	public abstract class DockingStationEvent : ICloneable
	{
		#region Fields

        string _name;

		private DockingStation _dockingStation;
        private EventCode _eventCode;

		private List<DockingStationError> _errors;
		private string _details;

        private TriggerType _triggerType = TriggerType.Unscheduled;

        private Schedule _schedule;

        private DateTime _time = DateTime.UtcNow;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of DockingStationEvent class.
		/// </summary>
        public DockingStationEvent()
        {
        }

        public DockingStationEvent( IOperation operation )
        {
            this.Trigger = operation.Trigger;
            // there's no real need to clone Schedules since they're pretty much immutable.
            Schedule = operation.Schedule;
        }

		#endregion

		#region Properties

        /// <summary>
        /// Returns the name of this event.  The 'Name' is the name of the class,
        /// minus the namespace info. e.g., if the class is ISC.iNet.DS.DomainModel.SomeEvent,
        /// then the Name is "SomeEvent".
        /// </summary>
        public string Name
        {
            get
            {
                if ( _name == null )
                {
                    // The Name is the the class's full name minues the prefixed namespace info.
                    _name = this.GetType().ToString();
                    _name = _name.Substring( _name.LastIndexOf( '.' ) + 1 );
                }
                return _name;
            }
        }

		/// <summary>
		/// Gets or sets the docking station that is associated with the event.
		/// </summary>
		public DockingStation DockingStation
		{
			get
			{
				if ( _dockingStation == null )
					_dockingStation = new DockingStation();

				return _dockingStation;
			}
			set
			{
                _dockingStation = value;
			}
		}

        /// <summary>
        /// If this returns null, make sure the EventCode has been initialized in the DockingStationEvent subclass's constructor.
        /// </summary>
        public EventCode EventCode
        {
            get { return _eventCode; }
            protected set { _eventCode = value; }
        }

        /// <summary>
        /// The date/time this event was instantiated (set to DateTimeKind.UTC).  Since events are typically created
        /// at the very beginning of IOperation.Execute() calls, this time is typically
        /// the time the event was run.
        /// </summary>
        public DateTime Time
        {
            get { return _time; }
        }

		/// <summary>
		/// Gets or sets the Details
		/// </summary>
		public string Details
		{
			get
			{
				if ( _details == null )
					_details = string.Empty;

				return _details;
			}
			set
			{
                _details = value;
			}
		}

		/// <summary>
		/// Gets or sets the list of errors sent by the docking station.
		/// </summary>
		public List<DockingStationError> Errors
		{
			get
			{
				if ( _errors == null )
                    _errors = new List<DockingStationError>();

				return _errors;
			}
		}

        public virtual TriggerType Trigger
        {
            get
            {
                return _triggerType;
            }
            set
            {
                _triggerType = value;
            }
        }

        /// <summary>
        /// The Schedule that caused this event to occur. May be null.
        /// </summary>
        public Schedule Schedule
        {
            get { return _schedule; }
            set { _schedule = value; }
        }

		#endregion

		#region Methods

        /// <summary>
        /// Returns the Name of this class (See 'Name' property).
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string s = this.Name;
            if ( this.Trigger != TriggerType.Scheduled )
                s += string.Format( " ({0})", Trigger.ToString() ); ;
            return s;
        }

		/// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
		/// </summary>
		/// <param name="dockingStationEvent">Docking station event to be copied to</param>
		protected virtual void CopyTo( DockingStationEvent dsEvent )
		{
			dsEvent.DockingStation = ( DockingStation ) this.DockingStation.Clone();

            // Don't copy over top of the EventCode. We consider that property 'immutable'.
            //dsEvent.EventCode = this.EventCode;

			// Loop through error list calling clone for each one to fill the empty list.
            dsEvent.Errors.Clear();
            foreach ( DockingStationError error in this.Errors )
                dsEvent.Errors.Add( (DockingStationError)error.Clone() );

            dsEvent.Schedule = this.Schedule;
		}

        public virtual object Clone()
        {
            // First, do a shallow clone.
            DockingStationEvent dsEvent = (DockingStationEvent)this.MemberwiseClone();

            // Next, do a deep copy.  Note that CopyTo has a side effect of also doing a shallow copy
            // which we just did with the MemberwiseClone call.  So, this Clone implementation actually
            // causes us to shallow-copy everything twice which is arguably a bit wasteful.  But this 
            // allows us to not have to implement Clone() in each and every subclass, and the subclaasses
            // don't generally have many member variables anyways.

            this.CopyTo( dsEvent );

            return this;
        }

		#endregion

	}

}
