using System;


namespace ISC.iNet.DS.DomainModel
{
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define an installed cylinder.
	/// </summary>
	public class GasEndPoint : ICloneable
	{
		/// <summary>
		/// Type of installation.
		/// </summary>
		public enum Type : short
		{
			Manual = 0,
			iGas = 1,
			Manifold = 2
		}

		public enum ChangeType
		{
			Uninstalled = -1,
			NoChange = 0,
			Installed = 1,
			PressureChanged
		}

		#region Fields

		private Cylinder _cylinder;

		/// <summary>
		/// Gets or sets position of the cylinder.
		/// </summary>
		public int Position { get; set; }

		/// <summary>
		/// Type of installation (iGas vs Manifold vs Manual, etc.)
		/// </summary>
		public Type InstallationType { get; set; }

		/// <summary>
		/// Used by IDS v4.1 and newer to indicate what occured
		/// to the iGas card.
		/// </summary>
		public ChangeType GasChangeType { get; set; }

		/// <summary>
		/// <para>True if the cylinder's part number is known (i.e., we have a
		/// FactoryCylinder in the database with a  matching part number).
		/// </para>
		/// <para>False if its part number is unknown.
		/// </para>null if we do not yet know either way.
		/// </summary>
		public bool? Supported { get; set; }

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of InstalledCylinder class.
		/// </summary>
		public GasEndPoint()
		{
			// Do nothing
		}

		/// <summary>
		/// Creates a new instance of InstalledCylinder class when cylinder, position and install time is provided.
		/// </summary>
		/// <param name="cylinder">Cylinder</param>
		/// <param name="position">Position in which cylinder is located</param>
		/// <param name="cylinderType">Type of installation (iGas versus manifold, etc.)</param>
		public GasEndPoint( Cylinder cylinder, int position, GasEndPoint.Type installationType )
		{
			Cylinder = cylinder;
			Position = position;
			InstallationType = installationType;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the cylinder.
		/// </summary>
		public Cylinder Cylinder
		{
			get
			{
				if ( _cylinder == null )
					_cylinder = new Cylinder();
				return _cylinder;
			}
			set
			{
				_cylinder = value;
			}
		}

		#endregion

		#region Methods

		public static GasEndPoint CreateFreshAir( int position )
		{
			GasConcentration gc = new GasConcentration( GasType.Cache[ GasCode.FreshAir ], DomainModelConstant.NullDouble );

			Cylinder cyl = new Cylinder( FactoryCylinder.FRESH_AIR_PART_NUMBER, string.Empty );
			cyl.GasConcentrations.Add( gc );
			cyl.Pressure = PressureLevel.Full;

			return new GasEndPoint( cyl, position, Type.Manual );
		}

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return Position.ToString();
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of an InstalledCylinder object.
		/// </summary>
		/// <returns>InstalledCylinder object</returns>
		public virtual object Clone()
		{
			GasEndPoint gasEndPoint = (GasEndPoint)this.MemberwiseClone();
			gasEndPoint.Cylinder = (Cylinder)Cylinder.Clone();
			return gasEndPoint;
		}

		#endregion

	} // end-class GasEndPoint

    /// <summary>
    /// Exception thrown when the flow failed during bump / calibration.
    /// </summary>
    public class FlowFailedException : ApplicationException
    {
		public GasEndPoint GasEndPoint { get; private set; }

		public FlowFailedException( GasEndPoint gasEndPoint )
            : base( "The flow failed at position " + gasEndPoint.Position.ToString() + "." )
        {
			GasEndPoint = (GasEndPoint)gasEndPoint.Clone();
        }
    }
}
