using System;
using System.Collections.Generic;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a gas cylinder.
	/// </summary>
	public class Cylinder : FactoryCylinder, ICloneable
	{
        #region Fields

		private string _factoryId;
        private int _volume = int.MinValue; // milliters remaining.
		private DateTime _refillDate;
		private DateTime _expirationDate;
		private PressureLevel _pressure;

		#endregion
		
		#region Constructors

        public Cylinder() : base( string.Empty, string.Empty )
        {
        }

		/// <summary>
		/// Creates a new instance of Cylinder class.
		/// </summary>
		public Cylinder( string partNumber, string manufacturerCode ) : base( partNumber, manufacturerCode )
		{
			// Do nothing
		}

        /// <summary>
        /// Copy ctor: Copies the PartNumber, ManufacturerCode, and GasConcentrations out 
        /// of the passed-in factory cylinder into the new instance.
        /// </summary>
        /// <param name="factoryCylinder"></param>
        public Cylinder( FactoryCylinder factoryCylinder )
            : base( factoryCylinder.PartNumber, factoryCylinder.ManufacturerCode )
        {
            if ( factoryCylinder != null )
            {
                foreach ( GasConcentration gc in factoryCylinder.GasConcentrations )
                    GasConcentrations.Add( (GasConcentration)gc.Clone() );
            }
        }

		#endregion

		#region Properties

        // Base class doesn't have a 'setter', but we need one in this derived class.
        new public string PartNumber
        {
            get { return base.PartNumber; }
            set { _partNumber = value; }
        }

        // Base class doesn't have a 'setter', but we need one in this derived class.
        new public string ManufacturerCode
        {
            get { return base.ManufacturerCode; }
            set { _manufacturerCode = value; }
        }


		/// <summary>
		/// Gets or sets the gas cylinder factory ID.
		/// </summary>
		public string FactoryId
		{
			get
			{
				if ( _factoryId == null )
					_factoryId = string.Empty;

				return _factoryId;
			}
			set
			{
				if ( value == null )
				{
					_factoryId = null;
				}
				else
				{
					_factoryId = value.Trim().ToUpper();
				}
			}
		}

		/// <summary>
		/// Gets or sets the gas cylinder refill date.
		/// </summary>
		public DateTime RefillDate
		{
			get
			{
				if ( IsFreshAir )
				{
					return DateTime.MaxValue.AddDays( -1 );
				}

				return _refillDate;
			}
			set
			{
				if ( ! IsFreshAir )
				{
					_refillDate = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the gas cylinder expiration date.
		/// </summary>
		public DateTime ExpirationDate
		{
			get
			{
				if ( IsFreshAir )
				{
					return DateTime.MaxValue.AddDays( -1 );
				}

				return _expirationDate;
			}
			set
			{
				if ( ! IsFreshAir )
				{
					_expirationDate = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the pressure level for the cylinder.
		/// </summary>
		public PressureLevel Pressure
		{
			get
			{
				return _pressure;
			}
			set
			{
				_pressure = value;
			}
		}

		/// <summary>
		/// The amount of gas (in milliters) remaining in this cylinder.
		/// </summary>
        public int Volume
        {
            get
            {
                return _volume;
            }
            set
            {
                _volume = value;

                // Don't let volume go negative.
                if ( _volume < 0 && _volume != int.MinValue )
                    _volume = 0;
            }
        }

		/// <summary>
		/// Gets or sets a value indicating whether this is a fresh air cylinder.
		/// </summary>
		public bool IsFreshAir
		{		
			get
			{
                // For a cylinder to be considered fresh air, it needs to contain
                // fresh air and ONLY fresh air.
				return ( GasConcentrations.Count <= 1 ) && ContainsGas( GasCode.FreshAir );
			}
		}

		/// <summary>
		/// Gets a value indicating whether this is zeroing gas cylinder.
		/// </summary>
		public bool IsZeroAir
		{		
			get
			{
                // For a cylinder to be considered zero air, it needs to contain
                // zero air and ONLY zero air.
                return ( GasConcentrations.Count <= 1 ) && ContainsGas( GasCode.O2 , 209000, MeasurementType.Unknown );
			}
		}

		#endregion

		#region Methods

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return FactoryId;
		}

		/// <summary>
		/// Searches for a given gas within the list of the GasConcentrations associated with the cylinder.
		/// </summary>
		/// <param name="gasCode">Code of the gas that is to be searched</param>
		/// <returns>True if GasConcentrations list contains given gas, false otherwise</returns>
		public bool ContainsGas( string gasCode )
        {
            string code = gasCode.Trim().ToUpper();

            // Loop through all gases and check if any of gas codes matches.
            foreach ( GasConcentration gasConcentration in GasConcentrations )
            {
                if ( gasConcentration.Type.Code == code )
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns whether or not this cylinder contains only the specified gas and no other.
        /// </summary>
        /// <param name="gasCode"></param>
        /// <returns></returns>
        public bool ContainsOnlyGas( string gasCode )
		{
            return  GasConcentrations.Count == 1 && GasConcentrations.Find( gc => gc.Type.Code == gasCode ) != null;
	    }

		/// <summary>
		/// Searches for a given gas with a specified concentration within the list of the GasConcentrations associated with the cylinder.
		/// </summary>
		/// <param name="gasCode">Code of the gas that is to be searched</param>
		/// <param name="sensorConcentration">
		/// Gas concentration of the gas that is to be searched.
		/// THE SENSOR CONCENTRATION PASSED IN TO THIS ROUTINE IS ALWAYS ASSUMED TO BE IN PPM.
        /// <para>
        /// NOTE: If DomainModelConstant.NullDouble is specified, then a match for gas concentration is not performed.
        /// </para>
		/// </param>
		/// <param name="measurementType">measurement units of the passed in concentration</param>
		/// <returns>True if GasConcentrations list contains given gas with the specified concentration, false otherwise</returns>
		public bool ContainsGas( string gasCode , double sensorConcentration, MeasurementType measurementType )
		{
			string code = gasCode.Trim().ToUpper();

			// Loop through all gases and check if any of gas codes and its concentration matches.
			foreach ( GasConcentration gasConcentration in GasConcentrations )
			{
                if ( gasConcentration.Type.Code != code )
                    continue;

                if ( sensorConcentration != DomainModelConstant.NullDouble )
                {
                    double cylinderConcentration = gasConcentration.Concentration;

                    // For LEL and VOL, we need to round the cylinder 
                    // concentration to the nearest hundred..  e.g., if cylinder's concentration
                    // is 3928, we want to round it to 3900.
                    if ( measurementType == MeasurementType.LEL || measurementType == MeasurementType.VOL )
                    {
                        // DO NOT DO THE FOLLOWING COMMENTED OUT LINE!  IT NEEDS TO BE KEPT
                        // SEPRATED AS THREE SEPARATE LINES, OTHERWISE IT MIGHT CRASH WINCE;
                        // PROBABLY DUE TO SOME BUG IN COMPACT FRAMEWORK?
                        //cylinderConcentration = Math.Round( cylinderConcentration / 100.0, 0 ) * 100.0;
                        cylinderConcentration /= 100.0;
                        cylinderConcentration = Math.Round( cylinderConcentration, 0 );
                        cylinderConcentration *= 100.0;
                    }
                    // For LEL sensors, round their concentration 
                    // to the nearest 100, just like we did with the cylinder just above.
                    if ( measurementType == MeasurementType.LEL )
                    {
                        //DONT DO THIS - ALREADY ASSUMED TO BE IN PPM --> sensorConcentration /= GasCodes.GetLELMultiplier( gasCode );

                        // DO NOT DO THE FOLLOWING COMMENTED OUT LINE!  IT NEEDS TO BE KEPT
                        // SEPRATED AS THREE SEPARATE LINES, OTHERWISE IT MIGHT CRASH WINCE;
                        // PROBABLY DUE TO SOME BUG IN COMPACT FRAMEWORK?
                        // sensorConcentration = Math.Round( sensorConcentration / 100.0, 0 ) * 100.0;
                        sensorConcentration /= 100.0;
                        sensorConcentration = Math.Round( sensorConcentration, 0 );
                        sensorConcentration *= 100.0;
                    }

                    if ( cylinderConcentration == sensorConcentration )
                        return true;
                }
			}

			return false;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a Cylinder object.
		/// </summary>
		/// <returns>Cylinder object</returns>
		public virtual object Clone()
		{
            Cylinder cylinder = (Cylinder)this.MemberwiseClone();

            cylinder._gases = new List<GasConcentration>( this.GasConcentrations.Count );

            foreach ( GasConcentration gasConcentration in GasConcentrations )
                cylinder.GasConcentrations.Add( (GasConcentration)gasConcentration.Clone() );

			return cylinder;
		}

		#endregion

	} // end-class Cylinder

	/// <summary>
	/// Defines cylinder pressure level. 
	/// </summary>
	public enum PressureLevel
	{
		Full,
		Low,
		Empty
	}
}
