using System;
using System.Collections.Generic;
using System.Text;


namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Gas codes for gases that are commonly referred to within source code.
    /// </summary>
    public class GasCode
    {
        // Special
        public const string FreshAir = "G9999";
        public const string Uninstalled = "G0000";
        // Toxics
        public const string CO = "G0001"; // Carbon Monoxide   
        public const string H2S = "G0002"; // Hydrogen Sulfide  
        public const string SO2 = "G0003"; // Sulfur Dioxide  
        public const string NO2 = "G0004"; // Nitrogen Dioxide   
        public const string Cl2 = "G0005"; // Chlorine  
        public const string ClO2 = "G0006"; // Chlorine Dioxide  
        public const string HCN = "G0007"; // Hydrogen Cyanide  
        public const string PH3 = "G0008"; // Phosphine  
        public const string H2 = "G0009"; // Hydrogen 
        public const string CO2 = "G0011"; // Carbon Dioxide    
        public const string NO = "G0012"; // Nitric Oxide   
        public const string NH3 = "G0013"; // Ammonia  
        public const string HCl = "G0014"; // Hydrogen Chloride  
        public const string O3 = "G0015"; // Ozone   
        public const string Phosgene = "G0016"; // COCl2
        public const string HF = "G0017"; // Hydrogen Flouride   
        // Oxygen
        public const string O2 = "G0020"; // Oxygen   
        // Combustibles
        public const string CombustiblePPM = "G0019"; // Combustible PPM  (PPM)   
        public const string Methane = "G0021"; // CH4 
        public const string CombustibleLEL = "G0022"; // Combustible LEL  (%LEL)   
        public const string Hexane = "G0023"; // C6H14
        public const string Pentane = "G0026"; // C5H12
        public const string Propane = "G0027"; // C3H8 
        // PIDs
        public const string Benzene = "G0059";
        public const string EthylBenzene = "G0066";

        // Note: Ethylene Oxide used to be only treated as a PID-detected gas.
        // GasBadgePro instrument can now detect it as a toxic.
        public const string EthyleneOxide = "G0081";
        public const string Heptane = "G0084";  // C7H16
        public const string Isobutylene = "G0091";
        public const string XyleneM = "G0104";
        public const string XyleneO = "G0107";
        public const string XyleneP = "G0113";
        public const string Toluene = "G0123";
        public const string N2 = "G0130"; // Nitrogen
        public const string Butadiene = "G0132";  // C4H6
        public const string Isobutane = "G0133";  // C4H10
        // The following are duplicate gas codes that are only returned by the VX500 (PID Sensor)
        //public const string VX500_AMMONIA          = "G0057";  // NH3
        //public const string VX500_HEXANE           = "G0085";  // C6H14
        //public const string VX500_HYDROGEN_SULFIDE = "G0083";  // H2S
        //public const string VX500_PHOSPHINE        = "G0110";
        //public const string VX500_BENZENE          = "G0128";

        /// <summary>
        /// Logged by instrument in its alarm events logs whenever a Proximity alarm occurs.
        /// </summary>
        public const string PROXIMITY = "G0245";

		public const string Hydrocarbon = "G0248"; // Introduced with VPRO (INS-6102,INS-6103), 04Aug2015

        public const string CustomResponseFactor1 = "G0250";
        public const string CustomResponseFactor2 = "G0251";
        public const string CustomResponseFactor3 = "G0252";
        public const string CustomResponseFactor4 = "G0253";
        public const string CustomResponseFactor5 = "G0254";

        public static bool IsCustomResponseFactor( DeviceType deviceType, string gasCode )
        {
            if ( deviceType != DeviceType.MX6 )
                return false;

            return gasCode.CompareTo( CustomResponseFactor1 ) >= 0 && gasCode.CompareTo( CustomResponseFactor5 ) <= 0;
        }

		/// <summary>
		/// Uses the gas code of a sensor to determine if the sensor is eligible for STEL/TWA readings.
		/// This code was ported from iNet.
		/// </summary>
		/// <param name="code">the gas code to evaluate</param>
		/// <returns>true - if eligible for STEL/TWA</returns>
		public static bool IsStelTwaEligible(string code)
		{
			return code != string.Empty
				   && code != O2
				   && code != Methane
				   && code != CombustiblePPM
				   && code != CombustibleLEL
				   && code != Hexane
				   && code != "G0024" // what gas is this?
				   && code != "G0025" // what gas is this?
				   && code != Pentane
				   && code != Propane
				   && code != Isobutane
				   && code != Hydrocarbon;
		}

        
        //public static double GetLELMultiplier( string gasCode )
        //{
        //    // The following gases are the only gases allowed for calibration of LEL sensors.

        //    if ( gasCode == GasCode.H2 ) return 0.0025; // 0.01 / 4.0 (%VOL @ 100 %LEL)
        //    if ( gasCode == GasCode.Methane ) return 0.002;  // 0.01 / 5.0 (%VOL @ 100 %LEL)
        //    if ( gasCode == GasCode.Hexane ) return 0.0085; // 0.01 / 1.1 (%VOL @ 100 %LEL)
        //    if ( gasCode == GasCode.Pentane ) return 0.0071; // 0.01 / 1.4 (%VOL @ 100 %LEL)
        //    if ( gasCode == GasCode.Propane ) return 0.0047; // 0.01 / 2.1 (%VOL @ 100 %LEL)
        //    if ( gasCode == GasCode.Butadiene ) return 0.005;  // 0.01 / 2.0 (%VOL @ 100 %LEL)

        //    return 0.0;  // default
        //}

        // <summary>
        /// When bumping with a gas that is not the same as the sensor is set for,
        /// a correlation factor must be used to adjust the reading.
        /// 
        /// NOTE: Source is GDME, pg 108.
        /// </summary>
        /// <param name="sensor"></param>
        /// <param name="gasCode"></param>
        /// <returns></returns>
        // Commented out because returned value does't appear to be used anywhere - JMP, v4.0
        //	public double GetSensorReadingModifier( Sensor sensor , string gasCode ) 
        //	{
        //		// LEL-CH4 sensor
        //		if ( sensor.Type.Code == SensorCodes.COMBUSTIBLE_CH4  &&  gasCode == GasCodes.PENTANE )
        //			return 0.5;
        //		return 1.0;
        //	}

        /// <summary>
        /// Private ctor - can't instantate; this class is static attributes only.
        /// </summary>
        private GasCode() { }


    }
}
