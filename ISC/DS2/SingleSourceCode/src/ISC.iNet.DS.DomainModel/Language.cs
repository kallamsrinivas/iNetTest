using System;
using System.Globalization;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a language.
	/// </summary>
	public class Language : ICloneable
	{
        public const string English = "ENGUS";
        public const string French = "FRNFR";
        public const string German = "GRMGR";

        /// <summary>
        /// Prior to v5.7, this was the language code used for Portuguese
        /// </summary>
        private const string PortugueseBrazil_Obsolete = "PRTBR"; // JFC  26-Sep-2013  INS-4248
        public const string PortugueseBrazil = "PORTUGUESEBRAZIL"; // JFC  26-Sep-2013  INS-4248
        public const string Spanish = "SPNSP";

        // JFC  26-Sep-2013  INS-4248
        public const string BahasaIndonesia = "BAHASAINDONESIA";
        public const string Chinese = "CHINESE";
        public const string Czech = "CSECZ";
        //public const string Danish = "DANISH";
        public const string Dutch = "DUTCH";
        //public const string EnglishUK = "ENGUK";
        public const string FrenchCanada = "FRENCHCANADA";
        public const string Italian = "ITALIAN";
        //public const string Japanese = "JAPANESE";
        public const string Polish = "PLNPL";
        public const string Russian = "RUSRU";

		#region Fields

		private string _code;
        private CultureInfo _cultureInfo;

        //private string _description;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a Language class.
		/// </summary>
		public Language()
		{
			// Do nothing
		}

		/// <summary>
		/// Creates a new instance of a Language class when its code is provided.
		/// </summary>
		/// <param name="code">Language code</param>
		public Language( string code )
		{
			Code = code;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the language code.
		/// </summary>
		public string Code
		{
			get
			{
				if ( _code == null )
				{
					_code = string.Empty;
				}

				return _code;
			}
			set
			{
				if ( value == null )
				{
					_code = null;
				}
				else
				{
                    // JFC  26-Sep-2013  INS-4248
                    // Mapping obsolete DS Portuguese code to iNet's Portuguese code.  
                    // This code can be removed once all customer iNet DS's are above v5.7.
                    // This prevents pre-v5.7 Portuguese docking stations and their instruments  
                    // from changing to English after upgrading to v5.7 or later.
                    if (value.Trim().ToUpper() == PortugueseBrazil_Obsolete)
                    {
                        value = PortugueseBrazil;
                    }

 					_code = value.Trim().ToUpper();
				}

                // JFC 26-Sep-2013  INS-4248
                // New languages added to support setting all instrument languages will default
                // to have a culture of en-US.  This is okay as only the DockingStation uses the
                // Culture property.
                if (_code == French)
                    _cultureInfo = new CultureInfo("fr-FR");
                else if (_code == German)
                    _cultureInfo = new CultureInfo("de-DE");
                else if (_code == PortugueseBrazil)
                    _cultureInfo = new CultureInfo("pt-BR");  // SGF  1-Oct-2012  INS-1656
                else if (_code == Spanish)
                    _cultureInfo = new CultureInfo("es-ES");
                else // default is English            
                    _cultureInfo = new CultureInfo("en-US");
			}
		}

        /// <summary>
        /// Returns a CultureInfo which is based on the current value of the Code property.
        /// </summary>
        public CultureInfo Culture
        {
            get
            {
                if ( _cultureInfo == null )
                    _cultureInfo = new CultureInfo( "en" ); // default to english

                return _cultureInfo;
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
			return Code;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a Language object.
		/// </summary>
		/// <returns>Language object</returns>
		public virtual object Clone()
		{
            Language language = (Language)this.MemberwiseClone();
            language._cultureInfo = (CultureInfo)this.Culture.Clone();
            return language;
		}

		#endregion

	}

}
