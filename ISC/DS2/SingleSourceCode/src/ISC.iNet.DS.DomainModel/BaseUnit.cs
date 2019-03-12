using System;
using System.Collections.Generic;


namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
	/// Provides functionality to define a base unit that an instrument module 
	/// would be connected to while in use.
	/// </summary>
	public class BaseUnit : Device
	{
		#region Properties
		// Serial Number, Part Number, Type, Setup Date and Operation Minutes are defined on Device.

		/// <summary>
		/// Gets or sets the time the instrument module was turned on in the base unit.
		/// </summary>
		public DateTime InstallTime { get; set; }

		#endregion

		#region Methods

		public override object Clone()
		{
			// Serial Number, Part Number, Type, Setup Date, Operation Minutes and Event Time
			// are the only thing we care about being cloned properly.
			BaseUnit baseUnit = (BaseUnit)this.MemberwiseClone();

			return baseUnit;
		}

		#endregion
	}
}
