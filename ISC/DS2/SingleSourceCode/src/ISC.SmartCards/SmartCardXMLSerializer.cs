using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using ISC.SmartCards.Types;
using ISC.iNet.DS.DomainModel;

namespace ISC.SmartCards
{
	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Implements the ISerializer interface for serializing SmartCards to XML.
	/// </summary>
	public class SmartCardXMLSerializer : ISerializer
	{

		#region Constructors

		/// <summary>
		/// Initialize the object.
		/// </summary>
		public SmartCardXMLSerializer()
		{
			// Nothing to do.
		}

		#endregion

		#region Methods

		/// <summary>
		/// Serializes the SmartCard and its contents to an XML string.
		/// </summary>
		/// <param name="theObject">The card to serialize.</param>
		/// <returns>The XML representing the SmartCard.</returns>
		/// <exception cref="InvalidSmartCardException">
		/// If the serialization failed for some reason.
		/// </exception>
		public string Serialize( object theObject )
		{
			XmlDocument xmlDoc;
			SmartCard card;
			StringWriter stringWriter;

			card = ( SmartCard ) theObject;
			try
			{
				// Serialize the SmartCard into an XML document.
				xmlDoc = SerializeToXML( card );

				// Add XML version and encoding to the XML file.
				// xmlDoc.InsertBefore( xmlDoc.CreateXmlDeclaration( "1.0" ,
				// 		"utf-8" , null ) , xmlDoc.DocumentElement );
			
				// create the string writer
				stringWriter = new StringWriter();

				// save the document to it.
				xmlDoc.Save( stringWriter );

				return xmlDoc.OuterXml;
			}
			catch ( Exception e )
			{
				throw new InvalidSmartCardException( e );
			}
		}

		/// <summary>
		/// Deserializes a SmartCard from XML source.
		/// </summary>
		/// <param name="source">
		/// The XML source to deserialize the card from.
		/// </param>
		/// <returns>The newly reconstructed SmartCard.</returns>
		/// <exception cref="InvalidSmartCardException">
		/// If the deserialization failed for some reason.
		/// </exception>
		public object Deserialize( string source )
		{
			XmlDocument xmlDoc;
			
			// Deserialize the configuration information from the Xml document.
			try
			{
				xmlDoc = new XmlDocument();

				// Load the Xml source into an Xml document format.
				xmlDoc.LoadXml( source );
				
				return DeserializeXML( xmlDoc );
			}
			catch ( InvalidSmartCardException e )
			{
				throw new InvalidSmartCardException( e );
			}
			catch ( Exception ex )
			{
				throw new Exception( ex.ToString() );
			}
		}

		/// <summary>
		/// Converts a DateTime into an ISO conformant Date and Time string.
		/// </summary>
		/// <param name="dateTime">The DateTime to convert.</param>
		/// <returns>The ISO conformant Date and Time representation.</returns>
		protected string DateTimeToISO( DateTime dateTime )
		{
			return ( "" + dateTime.Year + "-" + dateTime.Month + "-" + dateTime.Day );
			/*
			return ( "" + dateTime.Year + "-" + dateTime.Month + "-" +
				dateTime.Day + " " + dateTime.Hour + ":" + dateTime.Minute +
				":" + dateTime.Second );
				*/
		}

		/// <summary>
		/// Serializes a SmartCard into an XmlDocument.
		/// </summary>
		/// <param name="card">The SmartCard to serialize.</param>
		/// <returns>An XmlDocument representing the SmartCard.</returns>
		/// <exception cref="InvalidSmartCardException">
		/// If the serialization of the smartcard failed for some reason.
		/// </exception>
		protected XmlDocument SerializeToXML( SmartCard card )
		{
			XmlDocument xmlDoc;
			XmlNode rootNode;
			XmlAttribute nodeAttribute;
			ISerializer serialize;
			object content;
			string objectXML;
			XmlNode childNode;

			// Make the root node.
			xmlDoc = new XmlDocument();
			rootNode = xmlDoc.CreateNode( XmlNodeType.Element , "sc" , string.Empty );
			
			// Add the partnumber attribute.
			nodeAttribute = xmlDoc.CreateAttribute( "pn" );
			nodeAttribute.Value = card.PartNumber;
			rootNode.Attributes.Append( nodeAttribute );

			// Add the programdate attribute.
			nodeAttribute = xmlDoc.CreateAttribute( "pd" );
			nodeAttribute.Value = DateTimeToISO( card.ProgramDate );
			rootNode.Attributes.Append( nodeAttribute );

			try
			{
			    // Add all of the items.
			    for ( int i = 0 ; i < card.ContentCount ; i++ )
				{
					// Get the content's serializer.
					serialize = card.GetSerializer( i );
					content = card.GetContent( i );

					// Serialize it.
					objectXML = serialize.Serialize( content );
					childNode = xmlDoc.CreateNode( XmlNodeType.Element , "i" , string.Empty );

					// Add its XML to the child's.
					childNode.InnerXml = objectXML;
					rootNode.AppendChild( childNode );
				}
			}
			catch ( Exception e )
			{
			    throw new InvalidSmartCardException( e );
			}

			// Put the root node at the top of the document.
			xmlDoc.AppendChild( rootNode );

			return xmlDoc;
		}

		/// <summary>
		/// Create a SmartCard from the information contained in an XmlDocument.
		/// </summary>
		/// <param name="xmlDoc">The document to process.</param>
		/// <returns>A new SmartCard built from the XmlDocument.</returns>
		/// <exception cref="InvalidSmartCardException">
		/// If the deserialization of the smartcard failed for some reason.
		/// </exception>
		protected SmartCard DeserializeXML( XmlDocument xmlDoc )
		{
			SmartCard smartCard;
			XmlNode rootNode;
			XmlNodeList nodeList;
			XmlAttribute attrNode;
			ISerializer serialize;
			object content;

			// Make the new smart card.
			smartCard = new Types.SmartCard();

			// Get the root node.
			rootNode = xmlDoc.DocumentElement;
			if ( rootNode == null )
			{
				return smartCard;
			}

			// Get the smart card node.
			nodeList = xmlDoc.GetElementsByTagName( "sc" );
			if ( nodeList.Count > 0 )
			{
				// Get the part number attribute.
				attrNode = ( XmlAttribute ) nodeList[ 0 ].Attributes.GetNamedItem( "pn" );
				if ( attrNode != null )
				{
					smartCard.PartNumber = attrNode.Value;
				}

				// Get the program date attribute.
				attrNode = ( XmlAttribute ) nodeList[ 0 ].Attributes.GetNamedItem( "pd" );
				if ( attrNode != null )
				{
					smartCard.ProgramDate = DateTime.Parse( attrNode.Value );
				}
			}

			try
			{
				// Get all of the cylinder content nodes.
				nodeList = xmlDoc.GetElementsByTagName( "c" );
				foreach ( XmlNode child in nodeList )
				{
					// Deserialize all of the cylinder children.
					serialize = new CylinderXMLSerializer();
					content = serialize.Deserialize( child.OuterXml );
					smartCard.Add( content , serialize );
				}
			}
			catch ( Exception e )
			{
				throw new InvalidSmartCardException( e );
			}

			return smartCard;
		}

		#endregion

	}

#if DEBUG
	#region TestClass

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The unit test class for the SmartCardXMLSerializer class.
	/// </summary>
	public class SmartCardXMLSerializerTester
	{
		/// <summary>
	    /// The unit tests for the SmartCardXMLSerializer class.
	    /// </summary>
		public static void Test()
		{
			try 
			{
				SmartCardXMLSerializer serial;
				SmartCard card , newCard;
				Cylinder cylinder;
				string cardXML , properXML;

				serial = new SmartCardXMLSerializer();
				card = new SmartCard();
                cylinder = new Cylinder( "1002-3440", "AIRLE" );

				// Fill out the cylinder properties.
				cylinder.FactoryId = "18-6789-23";
				cylinder.ExpirationDate = DateTime.Parse( "10/23/1969" );

				// Add the cylinder and its serializer
				card.Add( cylinder , new CylinderXMLSerializer() );
				
				// Set the card's properties.
				card.PartNumber = "1002-3440";
				card.ProgramDate = DateTime.Parse( "10/23/1969" );
				// Console.WriteLine( "@ Set properties on the SmartCard." );

				cardXML = serial.Serialize( card );
				// Console.WriteLine( "@ Serialized the SmartCard." );
				// Console.WriteLine( "@ SmartCard XML: '" + cardXML + "'." );

				properXML = "<smartCard partNumber=\"1002-3440\" programDate=\"1969-10-23 0:0:0\"><item><cylinder partNumber=\"1002-3440\" expirationDate=\"1969-10-23 0:0:0\" factoryID=\"18-6789-23\" /></item></smartCard>";
				Debug.Assert( cardXML.CompareTo( properXML ) == 0 ,
					"SmartCard XML Serialization failed." );
				// Console.WriteLine( "@ XML Comparison succeeded." );
				
				newCard = ( SmartCard ) serial.Deserialize( cardXML );
				// Console.WriteLine( "@ Built the SmartCard." );

				Debug.Assert( card.PartNumber.CompareTo( newCard.PartNumber ) == 0 ,
					"SmartCard XML deserializing did not extract PartNumber properly." );
				Debug.Assert( card.ProgramDate.CompareTo( newCard.ProgramDate ) == 0 ,
					"SmartCard XML deserializing did not extract ProgramDate properly." );

				Cylinder newcylinder = (Cylinder ) newCard.GetContent( 0 );
				Debug.Assert( cylinder.FactoryId.CompareTo( newcylinder.FactoryId ) == 0 ,
					"Cylinder XML building did not extract FactoryID properly." );
				Debug.Assert( cylinder.PartNumber.CompareTo( newcylinder.PartNumber ) == 0 ,
					"Cylinder XML building did not extract PartNumber properly." );
				Debug.Assert( cylinder.ExpirationDate.CompareTo( newcylinder.ExpirationDate ) == 0 ,
					"Cylinder XML building did not extract ExpirationDate properly." );
			}
			catch ( Exception e )
			{
				Console.WriteLine( "Exception in SmartCardSerializerXMLTester.Test(): {0}" ,
								   e.ToString() );
			}
		}
	}

	#endregion
#endif // DEBUG
}
