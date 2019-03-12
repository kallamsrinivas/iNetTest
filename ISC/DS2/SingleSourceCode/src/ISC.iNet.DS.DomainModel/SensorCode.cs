using System;

using System.Collections.Generic;
using System.Text;


namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Sensor codes for sensors that are commonly referred to within source code.
    /// </summary>
    public class SensorCode
    {
        public const string CO = "S0001"; // Carbon Monoxide
        public const string H2S = "S0002"; // Hydrogen Sulfide
        public const string SO2 = "S0003"; // Sulfur Dioxide
        public const string NO2 = "S0004"; // Nitrogen Dioxide
        public const string Cl2 = "S0005"; // Chlorine  
        public const string ClO2 = "S0006"; // Chlorine Dioxide 
        public const string HCN = "S0007"; // Hydrogen Cyanide  
        public const string PH3 = "S0008"; // Phosphine  
        public const string H2 = "S0009"; // Hydrogen
        public const string CO2 = "S0011"; // Carbon Dioxide
        public const string NO = "S0012"; // Nitric Oxide
        public const string NH3 = "S0013"; // Ammonia
        public const string HCl = "S0014"; // Hydrogen Chloride
        public const string O3 = "S0015"; // Ozone
        public const string Phosgene = "S0016"; // Phosgene
        public const string HF = "S0017"; // Hydrogen Flouride
        public const string O2 = "S0020"; // Oxygen

        public const string MethaneIR = "S0018"; // CH4
        public const string CombustiblePPM = "S0019";
        public const string CombustibleCH4 = "S0021";
        public const string CombustibleLEL = "S0022";
        public const string MethaneIRLEL = "S0024"; //IR-LEL (CH4) //Suresh 19-OCTOBER-2011 INS-2354
        //public const string Pentane = "S0026"; there is no such thing as a "pentane sensor".
        public const string Propane = "S0027";
        public const string PID = "S0050";
        public const string EtO = "S0081"; // Ethylene Oxide
		public const string Hydrocarbon = "S0248"; // Introduced with VPRO (INS-6102,INS-6103), 04Aug2015

        private const int LEL_MAX_ALARM = 60;

        /// <summary>
        /// Private ctor - can't instantate; this class is static attributes only.
        /// </summary>
        private SensorCode() { }

        public static bool IsCombustible( string sensorCode )
        {
            return sensorCode == CombustibleLEL || sensorCode == CombustibleCH4 || sensorCode == SensorCode.CombustiblePPM;
        }
    }
}
