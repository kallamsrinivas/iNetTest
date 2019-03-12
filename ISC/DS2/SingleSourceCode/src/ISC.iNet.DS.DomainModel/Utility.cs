using System;
using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
	public static class Utility
	{
		/// <summary>
		/// When iNet sends multi-line string values to be set on the instrument 
		/// (such as alarm action messages or company message), each line within 
		/// the string value will be separated by this character.
		/// </summary>
		private const char SEPARATOR = '|';

		/// <summary>
		/// Takes a string value and splits it into a list of strings based upon
		/// the line separator character.
		/// 
		/// iNet -> instrument
		/// </summary>
		public static List<string> SplitString( string value )
		{
			if ( value == null )
				return new List<string>();

			return new List<string>(value.Split( SEPARATOR ));
		}

		/// <summary>
		/// Takes a list of strings and joins them together into a single string.
		/// The line separator character is used to distinguish between lines.
		/// iNet does not want/need the last character to be the separator so 
		/// we trim those from the end.
		/// 
		/// instrument - > iNet
		/// 
		/// WARNING: A StringBuilder is NOT used to join the list of strings.
		/// </summary>
		public static string JoinStrings( List<string> values )
		{
			if ( values == null || values.Count == 0 )
				return string.Empty;

			string value = ReplaceNull( values[0] );

			for ( int i = 1; i < values.Count; i++ )
			{
				value += SEPARATOR + ReplaceNull( values[i] );
			}

			// iNet wants trailing separator characters trimmed
			return value.TrimEnd( new char[] { SEPARATOR } );			
		}

		/// <summary>
		/// Helper method to replace nulls with empty strings.
		/// </summary>
		private static string ReplaceNull( string value )
		{
			if ( value == null )
				return string.Empty;

			return value;
		}
	}
}
