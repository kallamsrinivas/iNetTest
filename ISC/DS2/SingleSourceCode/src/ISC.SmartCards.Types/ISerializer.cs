using System;

namespace ISC.SmartCards.Types
{
	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Interface that dictates what a class that serializes
	/// an object must implement.
	/// </summary>
	public interface ISerializer
	{

		#region Methods

		/// <summary>
		/// Serialize the object, return the serialized form.
		/// </summary>
		/// <param name="theObject">The object to serialize.</param>
		/// <returns>The serialized object.</returns>
		string Serialize( object theObject );

		/// <summary>
		/// Builds an object from a serialization.
		/// </summary>
		/// <param name="source">
		/// The source to deserialize the object from.
		/// </param>
		/// <returns>The newly reconstructed object.</returns>
		object Deserialize( string source );

		#endregion
		
	}
}
