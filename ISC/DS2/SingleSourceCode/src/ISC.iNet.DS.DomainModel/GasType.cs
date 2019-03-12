using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to define a gas type. Instances of GasType are immutable.
    /// </summary>
    public class GasType
    {
        #region Fields

        private static Dictionary<string,GasType> _cache;

        private string _code;
        private short _calOrder;
        private short _bumpOrder; // SGF  12-Oct-2010  CCB INS-1372
        private double _lelMultiplier;
        private string _symbol;
        private string _description;
        private bool _isCalGas;

        #endregion

        #region Constructors

        static GasType()
        {
            _cache = new Dictionary<string,GasType>();

            // SGF  26-Oct-2011  CCB INS-2335 -- updated calibration order
            // SGF  12-Oct-2010  CCB INS-1372 -- added bump order as the third argument
            _cache.Add("G0001", new GasType("G0001", 15, 15, 0, "CO", true, "Carbon Monoxide"));
            _cache.Add("G0002", new GasType("G0002", 14, 14, 0, "H2S", true, "Hydrogen Sulfide"));
            _cache.Add("G0003", new GasType("G0003",  9,  9, 0, "SO2", true, "Sulfur Dioxide"));
            _cache.Add("G0004", new GasType("G0004", 11, 11, 0, "NO2", true, "Nitrogen Dioxide"));
            _cache.Add("G0005", new GasType("G0005",  6,  6, 0, "Cl2", true, "Chlorine"));
            _cache.Add("G0006", new GasType("G0006",  3,  3, 0, "ClO2", true, "Chlorine Dioxide"));
            _cache.Add("G0007", new GasType("G0007",  7,  7, 0, "HCN", true, "Hydrogen Cyanide"));
            _cache.Add("G0008", new GasType("G0008",  8,  8, 0, "PH3", true, "Phosphine"));
            _cache.Add("G0009", new GasType("G0009", 16, 16, 0.0025, "H2", true, "Hydrogen"));
            // G0010 --> DO NOT USE
            _cache.Add("G0011", new GasType("G0011", 60, 60, 0, "CO2", true, "Carbon Dioxide"));
            _cache.Add("G0012", new GasType("G0012", 13, 13, 0, "NO", true, "Nitric Oxide"));
            _cache.Add("G0013", new GasType("G0013", 10, 10, 0, "NH3", true, "Ammonia"));
            _cache.Add("G0014", new GasType("G0014",  5,  5, 0, "HCL", true, "Hydrogen Chloride"));
            _cache.Add("G0015", new GasType("G0015", 12, 12, 0, "O3", true, "Ozone"));
            _cache.Add("G0016", new GasType("G0016", 24, 24, 0, "COCl2", true, "Phosgene"));
            _cache.Add("G0017", new GasType("G0017", 99, 99, 0, "HF", false, "Hydrogen Fluoride"));
            // G0018 --> DO NOT USE
            _cache.Add("G0019", new GasType("G0019", 99, 99, 0, "Combustible PPM", false, "Unidentified Combustible Gas"));
            _cache.Add("G0020", new GasType("G0020",  1, 100, 0, "O2", true, "Oxygen"));
            _cache.Add("G0021", new GasType("G0021", 22, 22, 0.002, "CH4", true, "Methane"));
            _cache.Add("G0022", new GasType("G0022", 99, 99, 0, "Combustible LEL", false, "Unidentified Combustible Gas"));
            _cache.Add("G0023", new GasType("G0023", 19, 19, 0.0085, "C6H14", true, "Hexane"));
            // G0024 --> DO NOT USE
            // G0025 --> DO NOT USE
            _cache.Add("G0026", new GasType("G0026", 20, 20, 0.0071, "C5H12", true, "Pentane"));
            _cache.Add("G0027", new GasType("G0027", 21, 21, 0.0047, "C3H8", true, "Propane"));
            _cache.Add("G0028", new GasType("G0028", 17, 17, 0, "C4H10O2", false, "1, 4-Butanediol"));
            _cache.Add("G0029", new GasType("G0029", 17, 17, 0, "C4H8O2", false, "1, 4-Dioxane"));
            _cache.Add("G0030", new GasType("G0030", 17, 17, 0, "C6H3(CH3)3", false, "1, 2, 4-Trimethyl benzene"));
            _cache.Add("G0031", new GasType("G0031", 17, 17, 0, "C6H3(CH3)3", false, "1, 2, 3-Trimethyl benzene"));
            _cache.Add("G0032", new GasType("G0032", 17, 17, 0, "CH2BR2", false, "1, 2-Dibromomethane"));
            _cache.Add("G0033", new GasType("G0033", 17, 17, 0, "C6H4CL2", false, "1, 2-Dichlorobenzene"));
            _cache.Add("G0034", new GasType("G0034", 17, 17, 0, "C6H3(CH3)3", false, "1, 3, 5-Trimethylbenzene"));
            _cache.Add("G0035", new GasType("G0035", 17, 17, 0, "CH3(CH2)2CH2OH", false, "1-Butanol"));
            _cache.Add("G0036", new GasType("G0036", 17, 17, 0, "CH3OCH2CHCH3", false, "1-Methoxy-2-propanol"));
            _cache.Add("G0037", new GasType("G0037", 17, 17, 0, "CH3CH2CH2OH", false, "1-Propanol"));
            _cache.Add("G0038", new GasType("G0038", 17, 17, 0, "CH3COOCH3", false, "Methyl acetate"));
            _cache.Add("G0039", new GasType("G0039", 17, 17, 0, "CH2:CHCOOCH3", false, "Methyl acrylate"));
            _cache.Add("G0040", new GasType("G0040", 17, 17, 0, "CH3COCH2CO2CH3", false, "Methyl acetoacetate"));
            _cache.Add("G0041", new GasType("G0041", 17, 17, 0, "C6H5COOCH3", false, "Methyl benzoate"));
            _cache.Add("G0042", new GasType("G0042", 17, 17, 0, "CH2:C(CH3)COOCH3", false, "Methyl methacrylate"));
            _cache.Add("G0043", new GasType("G0043", 17, 17, 0, "CH3COCH2CH3", false, "2-Butanone"));
            _cache.Add("G0044", new GasType("G0044", 17, 17, 0, "HCON(CH3)2", false, "1-Methyl formamide"));
            _cache.Add("G0045", new GasType("G0045", 17, 17, 0, "CH3OCH2CH2OH", false, "Methoxy ethanol"));
            _cache.Add("G0046", new GasType("G0046", 17, 17, 0, "CH3COCH2CH2CH3", false, "2-Pentanone"));
            _cache.Add("G0047", new GasType("G0047", 17, 17, 0, "C6H7N", false, "2-Picoline"));
            _cache.Add("G0048", new GasType("G0048", 17, 17, 0, "C6H15NO2", false, "2-Propanol"));
            _cache.Add("G0049", new GasType("G0049", 17, 17, 0, "HCONHCH3", false, "2-Methyl formamide"));
            _cache.Add("G0050", new GasType("G0050", 17, 17, 0, "(CH3)2NC:OCH3", false, "Dimethyl acetamide"));
            _cache.Add("G0051", new GasType("G0051", 17, 17, 0, "C6H7N", false, "3-Picoline"));
            _cache.Add("G0052", new GasType("G0052", 17, 17, 0, "CH3COCH2C(CH3)2OH", false, "Diacetone alcohol"));
            _cache.Add("G0053", new GasType("G0053", 17, 17, 0, "CH3CHO", false, "Acetaldehyde"));
            _cache.Add("G0054", new GasType("G0054", 17, 17, 0, "CH3COCH3", true, "Acetone"));
            _cache.Add("G0055", new GasType("G0055", 17, 17, 0, "C6H5COCH3", false, "Acetophenone"));
            _cache.Add("G0056", new GasType("G0056", 17, 17, 0, "CH2:CHCH2OH", false, "Allyl alcohol"));
            _cache.Add("G0057", new GasType("G0057", 10, 10, 0, "NH3", true, "Ammonia"));
            _cache.Add("G0058", new GasType("G0058", 17, 17, 0, "CH3COO(CH2)4CH3", false, "Amyl acetate"));
            _cache.Add("G0059", new GasType("G0059", 17, 17, 0, "C6H6", true, "Benzene"));
            _cache.Add("G0060", new GasType("G0060", 17, 17, 0, "CH3BR", false, "Bromomethane"));
            _cache.Add("G0061", new GasType("G0061", 99, 99, 0.005, "C4H6", false, "Butadiene"));
            _cache.Add("G0062", new GasType("G0062", 17, 17, 0, "CH3(CH2)3OCH2CH2OH", false, "Butoxy ethanol"));
            _cache.Add("G0063", new GasType("G0063", 17, 17, 0, "CH3COO(CH2)3CH3", false, "Butyl acetate"));
            _cache.Add("G0064", new GasType("G0064", 17, 17, 0, "C2CL4", false, "Tetrachloroethylene"));
            _cache.Add("G0065", new GasType("G0065", 17, 17, 0, "CH3CHCL2", false, "1, 1-Dichloroethane"));
            _cache.Add("G0066", new GasType("G0066", 17, 17, 0, "CH3CH2C6H5", true, "Ethyl benzene"));
            _cache.Add("G0067", new GasType("G0067", 17, 17, 0, "CLCH:CCL2", false, "Trichloroethylene"));
            _cache.Add("G0068", new GasType("G0068", 17, 17, 0, "C2H5CO2CH2COCH3", false, "Ethyl acetoacetate"));
            _cache.Add("G0069", new GasType("G0069", 17, 17, 0, "C6H5CL", false, "Chorobenzene"));
            _cache.Add("G0070", new GasType("G0070", 17, 17, 0, "C6H5CH(CH3)2", false, "Cumene"));
            _cache.Add("G0071", new GasType("G0071", 17, 17, 0, "C6H12", false, "Cylclohexane"));
            _cache.Add("G0072", new GasType("G0072", 17, 17, 0, "C6H10O", false, "Cyclohexanone"));
            _cache.Add("G0073", new GasType("G0073", 17, 17, 0, "C10H22", false, "Decane"));
            _cache.Add("G0074", new GasType("G0074", 17, 17, 0, "(C2H5)2NH", false, "Diethylamine"));
            _cache.Add("G0075", new GasType("G0075", 17, 17, 0, "CH3OCH2OCH3", false, "Dimethoxymethane"));
            _cache.Add("G0076", new GasType("G0076", 17, 17, 0, "C3H5OCL", false, "Epichlorohydrin"));
            _cache.Add("G0077", new GasType("G0077", 17, 17, 0, "CH3CH2OH", false, "Ethanol"));
            _cache.Add("G0078", new GasType("G0078", 17, 17, 0, "HOCH2CH2OH", false, "Ethylene glycol"));
            _cache.Add("G0079", new GasType("G0079", 17, 17, 0, "CH3COOC2H5", false, "Ethyl acetate"));
            _cache.Add("G0080", new GasType("G0080", 17, 17, 0.0037, "C2H4", true, "Ethylene")); //Suresh 04-OCT-2011 INS-2191 // SGF added LEL multiplier, based on review of DS2 code
            _cache.Add("G0081", new GasType("G0081",  4,  4, 0, "C2H4O", true, "Ethylene oxide"));
            _cache.Add("G0082", new GasType("G0082", 17, 17, 0, "CH2CH2CH2COO", false, "Butyrolactone"));
            _cache.Add("G0083", new GasType("G0083", 14, 14, 0, "H2S", true, "Hydrogen Sulfide"));
            _cache.Add("G0084", new GasType("G0084", 17, 17, 0, "C7H16", false, "Heptane"));
            _cache.Add("G0085", new GasType("G0085", 19, 19, 0, "C6H14", false, "Hexane"));
            _cache.Add("G0086", new GasType("G0086", 17, 17, 0, "H2NNH2", false, "Hydrazine"));
            _cache.Add("G0087", new GasType("G0087", 17, 17, 0, "CH3COOCH2CH2(CH3)2", false, "Isoamyl acetate"));
            _cache.Add("G0088", new GasType("G0088", 17, 17, 0, "(CH3)2CHN2", false, "Isopropylamine"));
            _cache.Add("G0089", new GasType("G0089", 17, 17, 0, "(CH3)2CHOCH(CH3)2", false, "Isopropyl ether"));
            _cache.Add("G0090", new GasType("G0090", 17, 17, 0, "C4H10O", false, "Isobutanol"));
            _cache.Add("G0091", new GasType("G0091", 17, 17, 0, "C4H8", true, "Isobutylene"));
            _cache.Add("G0092", new GasType("G0092", 17, 17, 0, "C8H18", false, "Isooctane"));
            _cache.Add("G0093", new GasType("G0093", 17, 17, 0, "C9H14O", false, "Isophorone"));
            _cache.Add("G0094", new GasType("G0094", 17, 17, 0, "C3H8O", false, "Isopropanol"));
            _cache.Add("G0095", new GasType("G0095", 17, 17, 0, "JET A FUEL", false, "Jet A Fuel"));
            _cache.Add("G0096", new GasType("G0096", 17, 17, 0, "JET A 1 FUEL", false, "Jet A1 Fuel"));
            _cache.Add("G0097", new GasType("G0097", 17, 17, 0, "JP 5 & JP 8", false, "JP-5 & JP-8 Fuel"));
            _cache.Add("G0098", new GasType("G0098", 17, 17, 0, "C8H16O4", false, "MEK (methylethyl ketone)"));
            _cache.Add("G0099", new GasType("G0099", 17, 17, 0, "C6H10O", false, "Mesityl oxide"));
            _cache.Add("G0100", new GasType("G0100", 17, 17, 0, "CH3COCH2CH(CH3)2", false, "MIBK (methyl-isobutyl ketone"));
            _cache.Add("G0101", new GasType("G0101", 17, 17, 0, "CH5N", false, "Monomethylamine"));
            _cache.Add("G0102", new GasType("G0102", 17, 17, 0, "C5H12O", false, "MTBE (Methyl-tertbutyl ether)"));
            _cache.Add("G0103", new GasType("G0103", 17, 17, 0, "CH3C6H4CH2OH", false, "Methylbenzyl alcohol"));
            _cache.Add("G0104", new GasType("G0104", 17, 17, 0, "C8H10", true, "m-Xylene"));
            _cache.Add("G0105", new GasType("G0105", 17, 17, 0, "C5H9NO", false, "n-Methylpyrrolidone"));
            _cache.Add("G0106", new GasType("G0106", 17, 17, 0, "C8H18", false, "Octane"));
            _cache.Add("G0107", new GasType("G0107", 17, 17, 0, "C6H10", false, "o-Xylene"));
            _cache.Add("G0108", new GasType("G0108", 17, 17, 0, "C8H10O", false, "Phenelethyl alcohol"));
            _cache.Add("G0109", new GasType("G0109", 17, 17, 0, "C6H5OH", false, "Phenol"));
            _cache.Add("G0110", new GasType("G0110",  8,  8, 0, "PH3", true, "Phosphine"));
            _cache.Add("G0111", new GasType("G0111", 17, 17, 0, "C3H6", false, "Propylene"));
            _cache.Add("G0112", new GasType("G0112", 17, 17, 0, "C3H6O", false, "Propylene oxide"));
            _cache.Add("G0113", new GasType("G0113", 17, 17, 0, "C8H10", false, "p-Xylene"));
            _cache.Add("G0114", new GasType("G0114", 17, 17, 0, "C5H5N", false, "Pryridine"));
            _cache.Add("G0115", new GasType("G0115", 17, 17, 0, "C9H7N", false, "Quinoline"));
            _cache.Add("G0116", new GasType("G0116", 17, 17, 0, "C8H8", false, "Styrene"));
            _cache.Add("G0117", new GasType("G0117", 17, 17, 0, "C4H8N", false, "tert-Butylamine"));
            _cache.Add("G0118", new GasType("G0118", 17, 17, 0, "CH2CL2", false, "Dichloromethane"));
            _cache.Add("G0119", new GasType("G0119", 17, 17, 0, "(CH3)3CSH", false, "tert-Butyl mercaptan"));
            _cache.Add("G0120", new GasType("G0120", 17, 17, 0, "(CH3)3COH", false, "tert-Butyl alcohol"));
            _cache.Add("G0121", new GasType("G0121", 17, 17, 0, "C4H8O", false, "THF (Tetrahydrofuran)"));
            _cache.Add("G0122", new GasType("G0122", 17, 17, 0, "C4H4S", false, "Thiophene"));
            _cache.Add("G0123", new GasType("G0123", 17, 17, 0, "C7H8", true, "Toluene"));
            _cache.Add("G0124", new GasType("G0124", 17, 17, 0, "C10H16", false, "Turpentine"));
            _cache.Add("G0125", new GasType("G0125", 17, 17, 0, "C8H12", false, "Vinylcylclohexene"));
            _cache.Add("G0126", new GasType("G0126", 17, 17, 0, "C4H6O", false, "Vinyl acetate"));
            _cache.Add("G0127", new GasType("G0127", 17, 17, 0, "C2H3CL", false, "Vinyl chloride"));
            _cache.Add("G0128", new GasType("G0128", 17, 17, 0, "C6H6", true, "Benzene Tube"));
            // G0129 --> DO NOT USE
            _cache.Add("G0130", new GasType("G0130", 99, 99, 0, "N2", false, "Nitrogen"));
            // G0131 --> DO NOT USE
            _cache.Add("G0132", new GasType("G0132", 99, 99, 0.005, "C4H6", false, "Butadiene"));
            _cache.Add("G0133", new GasType("G0133", 23, 23, 0.0056, "C4H10", true, "Isobutane")); // SGF  25-Oct-2012  INS-3066, INS-3454
            // G0134
            // to 
            // G0199
            _cache.Add("G0200", new GasType("G0200", 99, 99, 0, "C2H2", false, "Acetylene"));
            _cache.Add("G0201", new GasType("G0201", 99, 99, 0, "C4H10", false, "Butane"));
            _cache.Add("G0202", new GasType("G0202", 99, 99, 0, "C2H6", false, "Ethane"));
            _cache.Add("G0203", new GasType("G0203", 99, 99, 0, "CH3OH", false, "Methanol"));
            // G0204 --> DO NOT USE
            _cache.Add("G0205", new GasType("G0205", 99, 99, 0, "JP-4", false, "JP-4 fuel"));
            _cache.Add("G0206", new GasType("G0206", 99, 99, 0, "JP-5", false, "JP-5 fuel"));
            _cache.Add("G0207", new GasType("G0207", 99, 99, 0, "JP-8", false, "JP-8 fuel"));
            _cache.Add("G0208", new GasType("G0208", 99, 99, 0, "C2H4O2", false, "Acetic acid"));
            _cache.Add("G0209", new GasType("G0209", 99, 99, 0, "C2H4O", false, "Acetic Anhydrid"));
            _cache.Add("G0210", new GasType("G0210", 99, 99, 0, "AsH3", false, "Arsine"));
            _cache.Add("G0211", new GasType("G0211", 99, 99, 0, "Br2", false, "Bromine"));
            _cache.Add("G0212", new GasType("G0212", 99, 99, 0, "CS2", false, "Carbon disulfide"));
            _cache.Add("G0213", new GasType("G0213", 99, 99, 0, "C6H10", false, "Cyclohexene"));
            _cache.Add("G0214", new GasType("G0214", 99, 99, 0, "Diesel", false, "Diesel fuel"));
            _cache.Add("G0215", new GasType("G0215", 99, 99, 0, "C2H6OS", false, "Dimethyl sulfoxide"));
            _cache.Add("G0216", new GasType("G0216", 99, 99, 0, "C4H10O", false, "Ethyl ether"));
            _cache.Add("G0217", new GasType("G0217", 99, 99, 0, "I2", false, "Iodine"));
            _cache.Add("G0218", new GasType("G0218", 99, 99, 0, "CH4S", false, "Methyl mercaptan"));
            _cache.Add("G0219", new GasType("G0219", 99, 99, 0, "C10H8", false, "Naphthalene"));
            _cache.Add("G0220", new GasType("G0220", 99, 99, 0, "C6H5NO2", false, "Nitrobenzene"));
            _cache.Add("G0221", new GasType("G0221", 99, 99, 0, "C7H16O", false, "Methoxyethoxyethanol,2-"));
            _cache.Add("G0222", new GasType("G0222", 99, 99, 0.0125, "C9H20", true, "Nonane")); //Suresh 01-NOV-2011 INS-2292
			// hydrocarbon gas has no LEL multiplier, since it can't be used as a cal gas.
			_cache.Add("G0248", new GasType("G0248", 99, 99, 0, "Hydrocarbon", false, "Hydrocarbon" ) ); // Instroduced with VPRO (INS-6102,INS-6103), 04Aug2015
            _cache.Add("G9999", new GasType("G9999",  0,  0, 0, "Fresh Air", false, "Fresh Air"));

            #region Old Calibration Order
            //// SGF  12-Oct-2010  CCB INS-1372 -- added bump order as the third argument
            //_cache.Add( "G0001", new GasType( "G0001", 15, 15, 0, "CO", true, "Carbon Monoxide" ) );
            //_cache.Add( "G0002", new GasType( "G0002", 14, 14, 0, "H2S", true, "Hydrogen Sulfide" ) );
            //_cache.Add( "G0003", new GasType( "G0003", 9, 9, 0, "SO2", true, "Sulfur Dioxide" ) );
            //_cache.Add( "G0004", new GasType( "G0004", 10, 10, 0, "NO2", true, "Nitrogen Dioxide" ) );
            //_cache.Add( "G0005", new GasType( "G0005", 6, 6, 0, "Cl2", true, "Chlorine" ) );
            //_cache.Add( "G0006", new GasType( "G0006", 18, 18, 0, "ClO2", true, "Chlorine Dioxide" ) );
            //_cache.Add( "G0007", new GasType( "G0007", 7, 7, 0, "HCN", true, "Hydrogen Cyanide" ) );
            //_cache.Add( "G0008", new GasType( "G0008", 12, 12, 0, "PH3", true, "Phosphine" ) );
            //_cache.Add( "G0009", new GasType( "G0009", 16, 16, 0.0025, "H2", true, "Hydrogen" ) );
            //_cache.Add( "G0011", new GasType( "G0011", 60, 60, 0, "CO2", true, "Carbon Dioxide" ) );
            //_cache.Add( "G0012", new GasType( "G0012", 13, 13, 0, "NO", true, "Nitric Oxide" ) );
            //_cache.Add( "G0013", new GasType( "G0013", 8, 8, 0, "NH3", true, "Ammonia" ) );
            //_cache.Add( "G0014", new GasType( "G0014", 5, 5, 0, "HCL", true, "Hydrogen Chloride" ) );
            //_cache.Add( "G0015", new GasType( "G0015", 11, 11, 0, "O3", true, "Ozone" ) );
            //_cache.Add( "G0016", new GasType( "G0016", 24, 24, 0, "COCl2", true, "Phosgene" ) );
            //_cache.Add( "G0017", new GasType( "G0017", 99, 99, 0, "HF", false, "Hydrogen Fluoride" ) );
            //_cache.Add( "G0019", new GasType( "G0019", 99, 99, 0, "Combustible PPM", false, "Unidentified Combustible Gas" ) );
            //_cache.Add( "G0020", new GasType( "G0020", 3, 100, 0, "O2", true, "Oxygen" ) );
            //_cache.Add( "G0021", new GasType( "G0021", 22, 22, 0.002, "CH4", true, "Methane" ) );
            //_cache.Add( "G0022", new GasType( "G0022", 99, 99, 0, "Combustible LEL", false, "Unidentified Combustible Gas" ) );
            //_cache.Add( "G0023", new GasType( "G0023", 19, 19, 0.0085, "C6H14", true, "Hexane" ) );
            //_cache.Add( "G0026", new GasType( "G0026", 20, 20, 0.0071, "C5H12", true, "Pentane" ) );
            //_cache.Add( "G0027", new GasType( "G0027", 21, 21, 0.0047, "C3H8", true, "Propane" ) );
            //_cache.Add( "G0028", new GasType( "G0028", 4, 4, 0, "C4H10O2", false, "1, 4-Butanediol" ) );
            //_cache.Add( "G0029", new GasType( "G0029", 4, 4, 0, "C4H8O2", false, "1, 4-Dioxane" ) );
            //_cache.Add( "G0030", new GasType( "G0030", 4, 4, 0, "C6H3(CH3)3", false, "1, 2, 4-Trimethyl benzene" ) );
            //_cache.Add( "G0031", new GasType( "G0031", 4, 4, 0, "C6H3(CH3)3", false, "1, 2, 3-Trimethyl benzene" ) );
            //_cache.Add( "G0032", new GasType( "G0032", 4, 4, 0, "CH2BR2", false, "1, 2-Dibromomethane" ) );
            //_cache.Add( "G0033", new GasType( "G0033", 4, 4, 0, "C6H4CL2", false, "1, 2-Dichlorobenzene" ) );
            //_cache.Add( "G0034", new GasType( "G0034", 4, 4, 0, "C6H3(CH3)3", false, "1, 3, 5-Trimethylbenzene" ) );
            //_cache.Add( "G0035", new GasType( "G0035", 4, 4, 0, "CH3(CH2)2CH2OH", false, "1-Butanol" ) );
            //_cache.Add( "G0036", new GasType( "G0036", 4, 4, 0, "CH3OCH2CHCH3", false, "1-Methoxy-2-propanol" ) );
            //_cache.Add( "G0037", new GasType( "G0037", 4, 4, 0, "CH3CH2CH2OH", false, "1-Propanol" ) );
            //_cache.Add( "G0038", new GasType( "G0038", 4, 4, 0, "CH3COOCH3", false, "Methyl acetate" ) );
            //_cache.Add( "G0039", new GasType( "G0039", 4, 4, 0, "CH2:CHCOOCH3", false, "Methyl acrylate" ) );
            //_cache.Add( "G0040", new GasType( "G0040", 4, 4, 0, "CH3COCH2CO2CH3", false, "Methyl acetoacetate" ) );
            //_cache.Add( "G0041", new GasType( "G0041", 4, 4, 0, "C6H5COOCH3", false, "Methyl benzoate" ) );
            //_cache.Add( "G0042", new GasType( "G0042", 4, 4, 0, "CH2:C(CH3)COOCH3", false, "Methyl methacrylate" ) );
            //_cache.Add( "G0043", new GasType( "G0043", 4, 4, 0, "CH3COCH2CH3", false, "2-Butanone" ) );
            //_cache.Add( "G0044", new GasType( "G0044", 4, 4, 0, "HCON(CH3)2", false, "1-Methyl formamide" ) );
            //_cache.Add( "G0045", new GasType( "G0045", 4, 4, 0, "CH3OCH2CH2OH", false, "Methoxy ethanol" ) );
            //_cache.Add( "G0046", new GasType( "G0046", 4, 4, 0, "CH3COCH2CH2CH3", false, "2-Pentanone" ) );
            //_cache.Add( "G0047", new GasType( "G0047", 4, 4, 0, "C6H7N", false, "2-Picoline" ) );
            //_cache.Add( "G0048", new GasType( "G0048", 4, 4, 0, "C6H15NO2", false, "2-Propanol" ) );
            //_cache.Add( "G0049", new GasType( "G0049", 4, 4, 0, "HCONHCH3", false, "2-Methyl formamide" ) );
            //_cache.Add( "G0050", new GasType( "G0050", 4, 4, 0, "(CH3)2NC:OCH3", false, "Dimethyl acetamide" ) );
            //_cache.Add( "G0051", new GasType( "G0051", 4, 4, 0, "C6H7N", false, "3-Picoline" ) );
            //_cache.Add( "G0052", new GasType( "G0052", 4, 4, 0, "CH3COCH2C(CH3)2OH", false, "Diacetone alcohol" ) );
            //_cache.Add( "G0053", new GasType( "G0053", 4, 4, 0, "CH3CHO", false, "Acetaldehyde" ) );
            //_cache.Add( "G0054", new GasType( "G0054", 4, 4, 0, "CH3COCH3", true, "Acetone" ) );
            //_cache.Add( "G0055", new GasType( "G0055", 4, 4, 0, "C6H5COCH3", false, "Acetophenone" ) );
            //_cache.Add( "G0056", new GasType( "G0056", 4, 4, 0, "CH2:CHCH2OH", false, "Allyl alcohol" ) );
            //_cache.Add( "G0057", new GasType( "G0057", 8, 8, 0, "NH3", true, "Ammonia" ) );
            //_cache.Add( "G0058", new GasType( "G0058", 4, 4, 0, "CH3COO(CH2)4CH3", false, "Amyl acetate" ) );
            //_cache.Add( "G0059", new GasType( "G0059", 4, 4, 0, "C6H6", true, "Benzene" ) );
            //_cache.Add( "G0060", new GasType( "G0060", 4, 4, 0, "CH3BR", false, "Bromomethane" ) );
            //_cache.Add( "G0061", new GasType( "G0061", 99, 99, 0.005, "C4H6", false, "Butadiene" ) );
            //_cache.Add( "G0062", new GasType( "G0062", 4, 4, 0, "CH3(CH2)3OCH2CH2OH", false, "Butoxy ethanol" ) );
            //_cache.Add( "G0063", new GasType( "G0063", 4, 4, 0, "CH3COO(CH2)3CH3", false, "Butyl acetate" ) );
            //_cache.Add( "G0064", new GasType( "G0064", 4, 4, 0, "C2CL4", false, "Tetrachloroethylene" ) );
            //_cache.Add( "G0065", new GasType( "G0065", 4, 4, 0, "CH3CHCL2", false, "1, 1-Dichloroethane" ) );
            //_cache.Add( "G0066", new GasType( "G0066", 4, 4, 0, "CH3CH2C6H5", true, "Ethyl benzene" ) );
            //_cache.Add( "G0067", new GasType( "G0067", 4, 4, 0, "CLCH:CCL2", false, "Trichloroethylene" ) );
            //_cache.Add( "G0068", new GasType( "G0068", 4, 4, 0, "C2H5CO2CH2COCH3", false, "Ethyl acetoacetate" ) );
            //_cache.Add( "G0069", new GasType( "G0069", 4, 4, 0, "C6H5CL", false, "Chorobenzene" ) );
            //_cache.Add( "G0070", new GasType( "G0070", 4, 4, 0, "C6H5CH(CH3)2", false, "Cumene" ) );
            //_cache.Add( "G0071", new GasType( "G0071", 4, 4, 0, "C6H12", false, "Cylclohexane" ) );
            //_cache.Add( "G0072", new GasType( "G0072", 4, 4, 0, "C6H10O", false, "Cyclohexanone" ) );
            //_cache.Add( "G0073", new GasType( "G0073", 4, 4, 0, "C10H22", false, "Decane" ) );
            //_cache.Add( "G0074", new GasType( "G0074", 4, 4, 0, "(C2H5)2NH", false, "Diethylamine" ) );
            //_cache.Add( "G0075", new GasType( "G0075", 4, 4, 0, "CH3OCH2OCH3", false, "Dimethoxymethane" ) );
            //_cache.Add( "G0076", new GasType( "G0076", 4, 4, 0, "C3H5OCL", false, "Epichlorohydrin" ) );
            //_cache.Add( "G0077", new GasType( "G0077", 4, 4, 0, "CH3CH2OH", false, "Ethanol" ) );
            //_cache.Add( "G0078", new GasType( "G0078", 4, 4, 0, "HOCH2CH2OH", false, "Ethylene glycol" ) );
            //_cache.Add( "G0079", new GasType( "G0079", 4, 4, 0, "CH3COOC2H5", false, "Ethyl acetate" ) );
            //_cache.Add( "G0080", new GasType( "G0080", 4, 4, 0, "C2H4", true, "Ethylene")); //Suresh 04-OCT-2011 INS-2191
            //_cache.Add( "G0081", new GasType( "G0081", 4, 4, 0, "C2H4O", true, "Ethylene oxide" ) );
            //_cache.Add( "G0082", new GasType( "G0082", 4, 4, 0, "CH2CH2CH2COO", false, "Butyrolactone" ) );
            //_cache.Add( "G0083", new GasType( "G0083", 14, 14, 0, "H2S", true, "Hydrogen Sulfide" ) );
            //_cache.Add( "G0084", new GasType( "G0084", 4, 4, 0, "C7H16", false, "Heptane" ) );
            //_cache.Add( "G0085", new GasType( "G0085", 19, 19, 0, "C6H14", false, "Hexane" ) );
            //_cache.Add( "G0086", new GasType( "G0086", 4, 4, 0, "H2NNH2", false, "Hydrazine" ) );
            //_cache.Add( "G0087", new GasType( "G0087", 4, 4, 0, "CH3COOCH2CH2(CH3)2", false, "Isoamyl acetate" ) );
            //_cache.Add( "G0088", new GasType( "G0088", 4, 4, 0, "(CH3)2CHN2", false, "Isopropylamine" ) );
            //_cache.Add( "G0089", new GasType( "G0089", 4, 4, 0, "(CH3)2CHOCH(CH3)2", false, "Isopropyl ether" ) );
            //_cache.Add( "G0090", new GasType( "G0090", 4, 4, 0, "C4H10O", false, "Isobutanol" ) );
            //_cache.Add( "G0091", new GasType( "G0091", 4, 4, 0, "C4H8", true, "Isobutylene" ) );
            //_cache.Add( "G0092", new GasType( "G0092", 4, 4, 0, "C8H18", false, "Isooctane" ) );
            //_cache.Add( "G0093", new GasType( "G0093", 4, 4, 0, "C9H14O", false, "Isophorone" ) );
            //_cache.Add( "G0094", new GasType( "G0094", 4, 4, 0, "C3H8O", false, "Isopropanol" ) );
            //_cache.Add( "G0095", new GasType( "G0095", 4, 4, 0, "JET A FUEL", false, "Jet A Fuel" ) );
            //_cache.Add( "G0096", new GasType( "G0096", 4, 4, 0, "JET A 1 FUEL", false, "Jet A1 Fuel" ) );
            //_cache.Add( "G0097", new GasType( "G0097", 4, 4, 0, "JP 5 & JP 8", false, "JP-5 & JP-8 Fuel" ) );
            //_cache.Add( "G0098", new GasType( "G0098", 4, 4, 0, "C8H16O4", false, "MEK (methylethyl ketone)" ) );
            //_cache.Add( "G0099", new GasType( "G0099", 4, 4, 0, "C6H10O", false, "Mesityl oxide" ) );
            //_cache.Add( "G0100", new GasType( "G0100", 4, 4, 0, "CH3COCH2CH(CH3)2", false, "MIBK (methyl-isobutyl ketone" ) );
            //_cache.Add( "G0101", new GasType( "G0101", 4, 4, 0, "CH5N", false, "Monomethylamine" ) );
            //_cache.Add( "G0102", new GasType( "G0102", 4, 4, 0, "C5H12O", false, "MTBE (Methyl-tertbutyl ether)" ) );
            //_cache.Add( "G0103", new GasType( "G0103", 4, 4, 0, "CH3C6H4CH2OH", false, "Methylbenzyl alcohol" ) );
            //_cache.Add( "G0104", new GasType( "G0104", 4, 4, 0, "C8H10", true, "m-Xylene" ) );
            //_cache.Add( "G0105", new GasType( "G0105", 4, 4, 0, "C5H9NO", false, "n-Methylpyrrolidone" ) );
            //_cache.Add( "G0106", new GasType( "G0106", 4, 4, 0, "C8H18", false, "Octane" ) );
            //_cache.Add( "G0107", new GasType( "G0107", 4, 4, 0, "C6H10", false, "o-Xylene" ) );
            //_cache.Add( "G0108", new GasType( "G0108", 4, 4, 0, "C8H10O", false, "Phenelethyl alcohol" ) );
            //_cache.Add( "G0109", new GasType( "G0109", 4, 4, 0, "C6H5OH", false, "Phenol" ) );
            //_cache.Add( "G0110", new GasType( "G0110", 12, 12, 0, "PH3", true, "Phosphine" ) );
            //_cache.Add( "G0111", new GasType( "G0111", 4, 4, 0, "C3H6", false, "Propylene" ) );
            //_cache.Add( "G0112", new GasType( "G0112", 4, 4, 0, "C3H6O", false, "Propylene oxide" ) );
            //_cache.Add( "G0113", new GasType( "G0113", 4, 4, 0, "C8H10", false, "p-Xylene" ) );
            //_cache.Add( "G0114", new GasType( "G0114", 4, 4, 0, "C5H5N", false, "Pryridine" ) );
            //_cache.Add( "G0115", new GasType( "G0115", 4, 4, 0, "C9H7N", false, "Quinoline" ) );
            //_cache.Add( "G0116", new GasType( "G0116", 4, 4, 0, "C8H8", false, "Styrene" ) );
            //_cache.Add( "G0117", new GasType( "G0117", 4, 4, 0, "C4H8N", false, "tert-Butylamine" ) );
            //_cache.Add( "G0118", new GasType( "G0118", 4, 4, 0, "CH2CL2", false, "Dichloromethane" ) );
            //_cache.Add( "G0119", new GasType( "G0119", 4, 4, 0, "(CH3)3CSH", false, "tert-Butyl mercaptan" ) );
            //_cache.Add( "G0120", new GasType( "G0120", 4, 4, 0, "(CH3)3COH", false, "tert-Butyl alcohol" ) );
            //_cache.Add( "G0121", new GasType( "G0121", 4, 4, 0, "C4H8O", false, "THF (Tetrahydrofuran)" ) );
            //_cache.Add( "G0122", new GasType( "G0122", 4, 4, 0, "C4H4S", false, "Thiophene" ) );
            //_cache.Add( "G0123", new GasType( "G0123", 4, 4, 0, "C7H8", true, "Toluene" ) );
            //_cache.Add( "G0124", new GasType( "G0124", 4, 4, 0, "C10H16", false, "Turpentine" ) );
            //_cache.Add( "G0125", new GasType( "G0125", 4, 4, 0, "C8H12", false, "Vinylcylclohexene" ) );
            //_cache.Add( "G0126", new GasType( "G0126", 4, 4, 0, "C4H6O", false, "Vinyl acetate" ) );
            //_cache.Add( "G0127", new GasType( "G0127", 4, 4, 0, "C2H3CL", false, "Vinyl chloride" ) );
            //_cache.Add( "G0128", new GasType( "G0128", 4, 4, 0, "C6H6", true, "Benzene Tube" ) );
            //_cache.Add( "G0130", new GasType( "G0130", 99, 99, 0, "N2", false, "Nitrogen" ) );
            //_cache.Add( "G0132", new GasType( "G0132", 99, 99, 0.005, "C4H6", false, "Butadiene" ) );
            //_cache.Add( "G0200", new GasType( "G0200", 99, 99, 0, "C2H2", false, "Acetylene" ) );
            //_cache.Add( "G0201", new GasType( "G0201", 99, 99, 0, "C4H10", false, "Butane" ) );
            //_cache.Add( "G0202", new GasType( "G0202", 99, 99, 0, "C2H6", false, "Ethane" ) );
            //_cache.Add( "G0203", new GasType( "G0203", 99, 99, 0, "CH3OH", false, "Methanol" ) );
            //_cache.Add( "G0205", new GasType( "G0205", 99, 99, 0, "JP-4", false, "JP-4 fuel" ) );
            //_cache.Add( "G0206", new GasType( "G0206", 99, 99, 0, "JP-5", false, "JP-5 fuel" ) );
            //_cache.Add( "G0207", new GasType( "G0207", 99, 99, 0, "JP-8", false, "JP-8 fuel" ) );
            //_cache.Add( "G0208", new GasType( "G0208", 99, 99, 0, "C2H4O2", false, "Acetic acid" ) );
            //_cache.Add( "G0209", new GasType( "G0209", 99, 99, 0, "C2H4O", false, "Acetic Anhydrid" ) );
            //_cache.Add( "G0210", new GasType( "G0210", 99, 99, 0, "AsH3", false, "Arsine" ) );
            //_cache.Add( "G0211", new GasType( "G0211", 99, 99, 0, "Br2", false, "Bromine" ) );
            //_cache.Add( "G0212", new GasType( "G0212", 99, 99, 0, "CS2", false, "Carbon disulfide" ) );
            //_cache.Add( "G0213", new GasType( "G0213", 99, 99, 0, "C6H10", false, "Cyclohexene" ) );
            //_cache.Add( "G0214", new GasType( "G0214", 99, 99, 0, "Diesel", false, "Diesel fuel" ) );
            //_cache.Add( "G0215", new GasType( "G0215", 99, 99, 0, "C2H6OS", false, "Dimethyl sulfoxide" ) );
            //_cache.Add( "G0216", new GasType( "G0216", 99, 99, 0, "C4H10O", false, "Ethyl ether" ) );
            //_cache.Add( "G0217", new GasType( "G0217", 99, 99, 0, "I2", false, "Iodine" ) );
            //_cache.Add( "G0218", new GasType( "G0218", 99, 99, 0, "CH4S", false, "Methyl mercaptan" ) );
            //_cache.Add( "G0219", new GasType( "G0219", 99, 99, 0, "C10H8", false, "Naphthalene" ) );
            //_cache.Add( "G0220", new GasType( "G0220", 99, 99, 0, "C6H5NO2", false, "Nitrobenzene" ) );
            //_cache.Add( "G0221", new GasType( "G0221", 99, 99, 0, "C7H16O", false, "Methoxyethoxyethanol,2-" ) );
            //_cache.Add( "G9999", new GasType( "G9999", 0, 0, 0, "Fresh Air", false, "Fresh Air" ) );
            #endregion
        }

		/// <summary>
		/// Creates a new instance of GasType class.
		/// </summary>
		/// <param name="code"></param>
		/// <param name="calOrder"></param>
		/// <param name="bumpOrder"></param>
		/// <param name="lelMultiplier">Should be zero if not a cal gas.</param>
		/// <param name="symbol"></param>
		/// <param name="isCalGas"></param>
		/// <param name="description"></param>
        public GasType( string code, short calOrder, short bumpOrder, double lelMultiplier, string symbol, bool isCalGas, string description )  // SGF  12-Oct-2010  CCB INS-1372 -- added bumpOrder
        {
            _code = code;
            _calOrder = calOrder;
            _bumpOrder = bumpOrder;
            _lelMultiplier = lelMultiplier;
            _symbol = symbol;
            _isCalGas = isCalGas;
            _description = description;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the gas type code.
        /// </summary>
        public string Code
        {
            get
            {
                if ( _code == null )
                    _code = string.Empty;

                return _code;
            }
        }

        /// <summary>
        /// Gets or sets calibration order of the gas.
        /// </summary>
        public short CalOrder
        {
            get
            {
                return _calOrder;
            }
        }

        /// <summary>
        /// Gets or sets bump order of the gas.
        /// </summary>
        public short BumpOrder
        {
            get
            {
                return _bumpOrder;
            }
        }

        /// <summary>
        /// Gets or sets LEL multiplier of the gas. Should be zero if not a cal-gas and not a LEL gas.
        /// </summary>
        public double LELMultiplier
        {
            get
            {
                return _lelMultiplier;
            }
        }

        /// <summary>
        /// Gets or sets scientific symbol of the gas.
        /// </summary>
        public string Symbol
        {
            get
            {
                if ( _symbol == null )
                    _symbol = string.Empty;
                return _symbol;
            }
        }

        /// <summary>
        /// Gets or sets the gas type description.
        /// </summary>
        public string Description
        {
            get
            {
                if ( _description == null )
                    _description = string.Empty;

                return _description;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the gas is a calibration gas.
        /// </summary>
        public bool IsCalGas
        {
            get
            {
                return _isCalGas;
            }
            set
            {
                _isCalGas = value;
            }
        }

        public static IDictionary<string, GasType> Cache
        {
            get { return GasType._cache; }
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

        #endregion

    }
}
