using System;
using System.Collections.Generic;
using System.Text;


namespace ISC.iNet.DS.DomainModel
{
    public class FactoryCylinder
    {
        public const string FRESH_AIR_PART_NUMBER = "FRESH AIR";

        protected string _partNumber;
        protected string _manufacturerCode;

        protected List<GasConcentration> _gases;

        public FactoryCylinder( string partNumber, string manufacturerCode )
        {
            _partNumber = partNumber;
            _manufacturerCode = manufacturerCode;

            _gases = new List<GasConcentration>();
        }

        public string PartNumber
        {
            get { return _partNumber == null ? string.Empty : _partNumber; }
        }

        public string ManufacturerCode
        {
            get { return _manufacturerCode == null ? string.Empty : _manufacturerCode; }
        }

        public List<GasConcentration> GasConcentrations
        {
            get
            {
                return _gases;
            }
        }
    }
}
