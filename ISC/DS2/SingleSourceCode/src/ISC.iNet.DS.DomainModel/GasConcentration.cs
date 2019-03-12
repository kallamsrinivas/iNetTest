using System;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a gas concentration.
	/// </summary>
	public class GasConcentration : ICloneable
	{
	
		#region Fields

		private GasType _type;
        private double _concentration;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of GasConcentration class when gas type code is provided.
		/// </summary>
		public GasConcentration( string code, double concentration )
		{
            _type = GasType.Cache[ code ];

            _concentration = concentration;
		}

        public GasConcentration( GasType gasType, double concentration )
        {
            _type = gasType;

            _concentration = concentration;
        }

		#endregion

		#region Properties

		/// <summary>
		/// Gets gas type.
		/// </summary>
		public GasType Type
		{
			get
			{
				return _type;
			}
		}

		/// <summary>
		/// Gets or sets concentration;
		/// </summary>
        public double Concentration
		{
			get
			{
				return _concentration;
			}
            set
            {
                _concentration = value;
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
			return Type.Code + " (" + Concentration.ToString() + ")";;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a GasConcentration object.
		/// </summary>
		/// <returns>GasConcentration object</returns>
        public virtual object Clone()
        {
            // note that we don't need to clone the GasType as it's immutable.
            return this.MemberwiseClone();
        }

		#endregion

	}

}