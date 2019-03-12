using System;
using ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Classes that provide docking station action related services must implement this interface.
	/// </summary>
	public interface IOperation
	{

		/// <summary>
		/// Implementation to execute a docking station action.
		/// </summary>
		/// <returns>A docking station event object</returns>
		DockingStationEvent Execute();

        TriggerType Trigger { get; set; }

        Schedule Schedule { get; set; }

        string Name { get; }
	}
}
