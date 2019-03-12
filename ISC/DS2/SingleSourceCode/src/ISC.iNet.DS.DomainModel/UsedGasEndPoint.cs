using System;

namespace ISC.iNet.DS.DomainModel
{
	public class UsedGasEndPoint : GasEndPoint
	{
		/// <summary>
		/// Amount of time, in minutes, that this cylinder was in use.
		/// </summary>
		public int DurationInUse { get; set; }

		/// <summary>
		/// Flow rate (ml/min) used for this cylinder.
		/// </summary>
		public int FlowRate { get; set; }

		public CylinderUsage Usage { get; set; }

		public short GasOperationGroup { get; set; }

		private void Init()
		{
			DurationInUse = int.MinValue;
			Usage = CylinderUsage.Unknown;
			FlowRate = int.MinValue;
			GasOperationGroup = short.MinValue;
		}

		/// <summary>
		/// Creates a new instance of InstalledCylinder class.
		/// </summary>
		public UsedGasEndPoint()
			: base()
		{
			Init();
		}

		/// <summary>
		/// Creates a new instance of InstalledCylinder class when cylinder, position and install time is provided.
		/// </summary>
		/// <param name="cylinder">Cylinder</param>
		/// <param name="position">Position in which cylinder is located</param>
		public UsedGasEndPoint( Cylinder cylinder, int position, Type installationType )
			: base( cylinder, position, installationType )
		{
			Init();
		}

		public UsedGasEndPoint( GasEndPoint gasEndPoint )
			: base( (Cylinder)gasEndPoint.Cylinder.Clone(), gasEndPoint.Position, gasEndPoint.InstallationType )
		{
			Init();
		}

		public UsedGasEndPoint( GasEndPoint gasEndPoint, CylinderUsage cylinderUsage, TimeSpan durationInUse )
			: this( gasEndPoint, cylinderUsage, durationInUse, DomainModelConstant.NullInt, -1 )
		{
		}

		public UsedGasEndPoint( GasEndPoint gasEndPoint, CylinderUsage cylinderUsage, TimeSpan durationInUse, short gasOperationGroup )
			: this( gasEndPoint, cylinderUsage, durationInUse, DomainModelConstant.NullInt, gasOperationGroup )
		{
		}

		/// <summary>
		/// Create instance using passed-in GasEndPoint
		/// </summary>
		/// <param name="gasEndPoint">Contents will be cloned by this method</param>
		/// <param name="cylinderUsage"></param>
		/// <param name="durationInUse">Seconds</param>
		/// <param name="flowRate">ml/Sec</param>
		/// <param name="gasOperationGroup"></param>
		/// <returns></returns>
		public UsedGasEndPoint( GasEndPoint gasEndPoint, CylinderUsage cylinderUsage, TimeSpan durationInUse, int flowRate, short gasOperationGroup )
			: this( gasEndPoint )
		{
			this.Usage = cylinderUsage;
			this.DurationInUse = Convert.ToInt32( durationInUse.TotalSeconds );
			this.FlowRate = flowRate;
			this.GasOperationGroup = gasOperationGroup;
		}

		/// <summary>
		/// Amount of gas used (in milliters).
		/// </summary>
		/// <remarks>
		/// <para>
		/// VolumeUsed is a computed property... VolumeUsed = FlowRate * DurationInUse.
		/// </para>
		/// </remarks>
        [Obsolete( "WARNING: FlowRate is currently never set by anything, so VolumeUsed is never accurate.", false )]
		public int VolumeUsed
		{
			get
			{
				// FlowRate only available from v6.0 and later docking stations.
				if ( FlowRate == int.MinValue )
					return int.MinValue;

				// We NEED to do the cast to double below, and multiply by 60.0 instead of
				// just 60 so that we do floating point arithmetic not integer arithmetic.
				// Otherwise, things like ( 87 / 60 ) * 500 equates to 500 which is wrong.
				return (int)( ( (double)this.DurationInUse / 60.0 ) * this.FlowRate );
			}
		}
	}

	/// <summary>
	/// The enumerated type that indicates what the calibration status was, as reported
	/// by the instrument, at the end of the calibration.
	/// </summary>
	public enum CylinderUsage
	{
		Unknown = 0,
		Zero,
		Precondition,
		Bump,
		BumpHigh,
		Calibration,
		PreZero,
		Purge
	}
}
