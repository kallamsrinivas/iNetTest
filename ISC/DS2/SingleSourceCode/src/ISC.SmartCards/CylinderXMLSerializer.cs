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
	/// Implements the ISerializer interface for serializing Cylinders to XML.
	/// </summary>
	public class CylinderXMLSerializer : ISerializer
	{

		#region Constructors

		/// <summary>
		/// Initialize the object.
		/// </summary>
		public CylinderXMLSerializer()
		{
			// Nothing to do.
		}

		#endregion

		#region Methods

		/// <summary>
		/// Serializes the Cylinder to an XML string.
		/// </summary>
		/// <param name="theObject">The cylinder to serialize.</param>
		/// <returns>The XML representing the Cylinder.</returns>
		/// <exception cref="InvalidCylinderException">
		/// If the serialization failed for some reason.
		/// </exception>
		public string Serialize( object theObject )
		{
			XmlDocument xmlDoc;
			Cylinder cylinder;
			StringWriter stringWriter;

			cylinder = ( Cylinder ) theObject;

			try
			{
				// Serialize the Cylinder into an XML document.
				xmlDoc = SerializeToXML( cylinder );

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
				throw new InvalidCylinderException( e );
			}
		}

		/// <summary>
		/// Deserializes a Cylinder from XML source.
		/// </summary>
		/// <param name="source">
		/// The XML source to retrieve the cylinder from.
		/// </param>
		/// <returns>The newly reconstructed Cylinder.</returns>
		/// <exception cref="InvalidCylinderException">
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
			catch ( Exception e )
			{
				throw new InvalidCylinderException( e );
			}
		}

		/// <summary>
		/// Converts a DateTime into an ISO conformant Date and Time string.
		/// </summary>
		/// <param name="dateTime">The DateTime to convert.</param>
		/// <returns>The ISO conformant Date and Time representation.</returns>
		protected string DateTimeToISO( DateTime dateTime )
		{
			return ( "" + dateTime.Year + "-" + dateTime.Month + "-" +
				dateTime.Day );
			/*
			return ( "" + dateTime.Year + "-" + dateTime.Month + "-" +
				dateTime.Day + " " + dateTime.Hour + ":" + dateTime.Minute +
				":" + dateTime.Second );
				*/
		}

		/// <summary>
		/// Serializes a Cylinder into an XmlDocument.
		/// </summary>
		/// <param name="cylinder">The Cylinder to serialize.</param>
		/// <returns>An XmlDocument representing the Cylinder.</returns>
		protected XmlDocument SerializeToXML( Cylinder cylinder )
		{
			XmlDocument xmlDoc;
			XmlNode rootNode;
			XmlAttribute nodeAttribute;

			// Make the root node.
			xmlDoc = new XmlDocument();
			rootNode = xmlDoc.CreateNode( XmlNodeType.Element , "c" , string.Empty );
			
			// Add the part number attribute.
			nodeAttribute = xmlDoc.CreateAttribute( "pn" );
			nodeAttribute.Value = cylinder.PartNumber;
			rootNode.Attributes.Append( nodeAttribute );

			// Add the expiration date attribute.
			nodeAttribute = xmlDoc.CreateAttribute( "ed" );
			nodeAttribute.Value = DateTimeToISO( cylinder.ExpirationDate );
			rootNode.Attributes.Append( nodeAttribute );

			// Add the refill date attribute.
			nodeAttribute = xmlDoc.CreateAttribute( "rd" );
			nodeAttribute.Value = DateTimeToISO( cylinder.RefillDate );
			rootNode.Attributes.Append( nodeAttribute );

			// Add the factory id attribute.
			nodeAttribute = xmlDoc.CreateAttribute( "fid" );
			nodeAttribute.Value = cylinder.FactoryId;
			rootNode.Attributes.Append( nodeAttribute );

			// Put the root node at the top of the document.
			xmlDoc.AppendChild( rootNode );

			return xmlDoc;
		}

		/// <summary>
		/// Create a Cylinder from the information contained in an XmlDocument.
		/// </summary>
		/// <param name="xmlDoc">The document to process.</param>
		/// <returns>A new Cylinder built from the XmlDocument.</returns>
		protected Cylinder DeserializeXML( XmlDocument xmlDoc )
		{
			Cylinder cylinder;
			XmlNode rootNode;
			XmlAttribute attrNode;
			XmlNodeList nodeList;

			cylinder = new Cylinder();

			// Get the root node.
			rootNode = xmlDoc.DocumentElement;
			if ( rootNode == null )
			{
				return cylinder;
			}

			// Get the cylinder node.
			nodeList = xmlDoc.GetElementsByTagName( "c" );
			if ( nodeList.Count > 0 )
			{
				// Get the part number attribute.
				attrNode = ( XmlAttribute ) nodeList[ 0 ].Attributes.GetNamedItem( "pn" );
				if ( attrNode != null )
				{
					cylinder.PartNumber = attrNode.Value;
				}

				// Get the factory id attribute.
				attrNode = ( XmlAttribute ) nodeList[ 0 ].Attributes.GetNamedItem( "fid" );
				if ( attrNode != null )
				{
					cylinder.FactoryId = attrNode.Value;
				}

				// Get the expiration date attribute.
				attrNode = ( XmlAttribute ) nodeList[ 0 ].Attributes.GetNamedItem( "ed" );
				if ( attrNode != null )
				{
                    // DEBUG HINT (EXPIRED CYLINDER)
                    // If you want to use a cylinder that has expired, but you want the cylinder to appear
                    // as being unexpired, place a break point on the following line.  When you attach
                    // the cylinder to the VDS, this break point will mature.  Modify the attrNode.Value
                    // to be a date in the future, and then allow execution to continue.
                    //
					cylinder.ExpirationDate = DateTime.Parse( attrNode.Value );
				}

				// Get the refill date attribute.
				attrNode = ( XmlAttribute ) nodeList[ 0 ].Attributes.GetNamedItem( "rd" );
				if ( attrNode != null )
				{
					cylinder.RefillDate = DateTime.Parse( attrNode.Value );
				}
			}

			return cylinder;
		}

		#endregion
		
	}

	#region Exceptions

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// The exception that is thrown when a Cylinder object is invalid.
	/// </summary>
	public class InvalidCylinderException : ApplicationException
	{
		/// <summary>
		/// Initializes a new instance of the InvalidCylinderException class by
		/// setting the exception message and name of the invalid property.
		/// </summary>
		/// <param name="e">The exception to incapsulate.</param>
		public InvalidCylinderException( Exception e ) : base ( "Invalid Cylinder!" )
		{
			// Do nothing
		}

		/// <summary>
		/// Initializes a new instance of the InvalidCylinderException class by
		/// setting the exception message.
		/// </summary>
		public InvalidCylinderException() : base ( "Invalid Cylinder!" )
		{
			// Do nothing
		}
	}

	#endregion

#if DEBUG
	#region TestClass

	///////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Implements the unit tests for the CylinderXMLSerializerTester class.
	/// </summary>
	public class CylinderXMLSerializerTester
	{
	    /// <summary>
	    /// The unit tests.
	    /// </summary>
		public static void Test()
		{
			try 
			{
				CylinderXMLSerializer serial;
				Cylinder cylinder , newCylinder;
				string cylinderXML , properXML;

				serial = new CylinderXMLSerializer();
				cylinder = new Cylinder( "1002-3440", "AIRLE" );

				cylinder.FactoryId = "18-6789-23";
				cylinder.ExpirationDate = DateTime.Parse( "10/23/1969" );
				cylinder.RefillDate = DateTime.Parse( "09/16/1969" );

				cylinderXML = serial.Serialize( cylinder );
				//Console.WriteLine( "@ cylinderXML: '" + cylinderXML + "'." );

				//string theXML = "<?xml version=\"1.0\" encoding=\"utf-8\"?><cylinder partNumber=\"1002-3440\" expirationDate=\"1969-10-23 0:0:0\" factoryID=\"18-6789-23\" />";
				properXML = "<cylinder partNumber=\"1002-3440\" expirationDate=\"1969-10-23 0:0:0\" factoryID=\"18-6789-23\" />";
				Debug.Assert( cylinderXML.CompareTo( properXML ) == 0 ,
					"Cylinder XML Persistence failed." );

				newCylinder = ( Cylinder ) serial.Deserialize( cylinderXML );
				
				Debug.Assert( cylinder.FactoryId.CompareTo( newCylinder.FactoryId ) == 0 ,
					"Cylinder XML building did not extract FactoryID properly." );
				Debug.Assert( cylinder.PartNumber.CompareTo( newCylinder.PartNumber ) == 0 ,
					"Cylinder XML building did not extract PartNumber properly." );
				Debug.Assert( cylinder.ExpirationDate.CompareTo( newCylinder.ExpirationDate ) == 0 ,
					"Cylinder XML building did not extract ExpirationDate properly." );
				Debug.Assert( cylinder.RefillDate.CompareTo( newCylinder.RefillDate ) == 0 ,
					"Cylinder XML building did not extract RefillDate properly." );
			}
			catch ( Exception e )
			{
				Console.WriteLine( "Exception in CylinderPersistenceXMLTester.Test(): {0}" ,
								   e.ToString() );
			}
		}
	}

	#endregion
#endif // DEBUG
}
