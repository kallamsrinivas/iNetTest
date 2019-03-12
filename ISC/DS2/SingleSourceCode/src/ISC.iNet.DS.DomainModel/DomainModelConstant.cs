using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    public class DomainModelConstant
    {
        public const long NullLong = long.MinValue;
        public const long NullId = NullLong;
        public static readonly DateTime NullDateTime = DateTime.MinValue;
        public static readonly TimeSpan NullTimeSpan = TimeSpan.MinValue;
        public const int NullInt = int.MinValue;
		public const ushort NullUShort = ushort.MaxValue; // can't use MinValue since that would be zero, which could easily be a valid data value.
        public const short NullShort = short.MinValue;
        public const float NullFloat = float.MinValue;
        public const double NullDouble = double.MinValue;
        public const decimal NullDecimal = decimal.MinusOne;
    }
}
