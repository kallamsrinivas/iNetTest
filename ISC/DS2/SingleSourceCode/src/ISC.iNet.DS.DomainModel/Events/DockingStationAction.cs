using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;

namespace ISC.iNet.DS.DomainModel
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides base functionality for classes defining a docking station action.
	/// </summary>
	public abstract class DockingStationAction
	{
		
		#region Fields

        private string _name;

        // We always default to Unscheduled.
        private TriggerType _triggerType = TriggerType.Unscheduled;

        private Schedule _schedule;  // Schedule that caused this action to occur.  May be null.
        List<string> _messages;

        private DockingStation _dockingStation;
        
		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of generic DockingStationAction class.
		/// </summary>
		public DockingStationAction()
		{
		}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="dockingStationAction"></param>
        public DockingStationAction( DockingStationAction dockingStationAction )
        {
            this.DockingStation = dockingStationAction.DockingStation;

            this.Trigger = dockingStationAction.Trigger;

            // there's no real need to clone Schedules since they're pretty much immutable.
            Schedule = dockingStationAction.Schedule;

            foreach ( string msg in dockingStationAction.Messages )
                this.Messages.Add( msg );
        }

		#endregion

		#region Properties

        /// <summary>
        /// Returns the name of this action.  The 'Name' is the name of the class,
        /// minus the namespace info.
        /// e.g., if the class is ISC.iNet.DS.DomainModel.SomeAction, then the Name
        /// is "SomeAction".
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
        /// Messages sent by the server to the IDS for it to display
        /// on its LCD when executing this action.
        /// </summary>
        public List<string> Messages
        {
            get
            {
                if ( _messages == null )
                    _messages = new List<string>();
                return _messages;
            }
            set
            {
                _messages = value;
            }
        }

        /// <summary>
        /// Schedule that caused the action to occur.  May be null.
        /// </summary>
        public Schedule Schedule
        {
            get { return _schedule; }
            set { _schedule = value; }
        }

        /// <summary>
        /// Default is TriggerType.Unscheduled
        /// </summary>
        public TriggerType Trigger
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

        public DockingStation DockingStation
        {
            get
            {
                return _dockingStation;
            }
            set
            {
                _dockingStation = value;
            }
        }


		#endregion

		#region Methods

        /// <summary>
        /// Returns the Name of this class (See 'Name' property).
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string s = this.Name ;
            if ( this.Trigger != TriggerType.Scheduled )
                s += string.Format( " ({0})", Trigger.ToString() ); ;
            return s;
        }

		#endregion

	}  // end-class DockingStationAction

    public enum TriggerType
    {
        /// <summary>
        /// Performed automatically for various reasons by the docking station.
        /// Such as automatically reading the settings when an instrument is docked,
        /// or a cylinder is installed/removed.
        /// </summary>
        Unscheduled = 0,  
        /// <summary>
        /// Performed due to a specific Schedule.
        /// </summary>
        Scheduled = 1,
        /// <summary>
        /// Performed due to being forced by the user, either via the keypad or forced through iNet Control
        /// </summary>
        Forced = 2,
        /// <summary>
        /// Performed manually on the instrument by the user. (Used for manual calibrations/bump events.)
        /// </summary>
        Manual = 3
    }
}  // end-namespace
