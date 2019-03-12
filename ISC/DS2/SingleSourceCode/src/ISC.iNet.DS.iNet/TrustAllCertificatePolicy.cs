using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;


namespace ISC.iNet.DS.iNet
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// <para>
    /// NOTE: The following is all from http://weblogs.asp.net/jan/archive/2003/12/04/41154.aspx ...
    /// </para>
    /// <para>
    /// CONSUMING WEBSERVICES OVER HTTPS (SSL)
    /// </para>
    /// <para>
    /// "When Webservices are used, a common concern is security: SOAP messages are transferred in plain text over
    /// the network, so anyone with a sniffer could intercept the SOAP message and read it. In my opinion this could 
    /// happen also to binary data, but probably it requires a little bit more hacker skills. So a solution is to 
    /// use HTTPS (SSL) instead of HTTP, so the communication is encrypted. To accomplish this, you need to get
    /// and install a certificate (issued by a Certificate Authority) on your webserver. In a production environment
    /// you would buy a certificate from Verisign or another well known CA, or you would install your own CA, 
    /// which is a component of Windows Server. If you only want to play with HTTPS, SSL and certificates or your 
    /// project is in the development phase, you can also generate a test certificate using the MakeCert.exe tool
    /// (included in the .NET Framework SDK). After that you have to add this certificate to a website in IIS, 
    /// and set a port which HTTPS should use.
    /// </para>
    /// <para>When you browse to a HTTPS site, you probably get a dialog window asking you if you want to
    /// trust the certificate provided by the webserver. So the responsibility of accepting the certificate
    /// is handled by the user. Let's get back to the webservice scenario, if you want to invoke a webservice
    /// located on a webserver which uses SSL and HTTPS there is a problem. When you make the call from code,
    /// there is no dialog window popping up, and asking if you trust the certificate (luckily because this
    /// would be pretty ugly in server-side scenarios); probably you'll get following exception: 
    /// An unhandled exception of type 'System.Net.WebException' occurred in system.dll
    /// </para>
    /// <para>
    /// Additional information: The underlying connection was closed: Could not establish trust relationship with remote server.
    /// </para>
    /// <para>
    /// But there is a solution for this problem, you can solve this in your code by creating your own CertificatePolicy
    /// class (which implements the ICertificatePolicy interface). In this class you will have to write your own
    /// CheckValidationResult function that has to return true or false, like you would press yes or no in the dialog
    /// window. For development purposes I've created the following class which accepts all certificates, so you won't
    /// get the nasty WebException anymore:
    /// </para>
    /// <code>
    /// public class TrustAllCertificatePolicy : System.Net.ICertificatePolicy
    /// {
    /// public TrustAllCertificatePolicy() {}
    ////
    /// public bool CheckValidationResult(ServicePoint sp, X509Certificate cert,WebRequest req, int problem)
    ///     {
    ///         return true;
    ///     }
    /// }
    /// </code>
    /// <para>
    /// As you can see the CheckValidationResult function always returns true, so all certificates will be trusted.
    /// If you want to make this class a little bit more secure, you can add additional checks using the
    /// X509Certificate parameter for example. To use this CertificatePolicy, you'll have to tell the
    /// ServicePointManager to use it:
    /// </para>
    /// <code>
    /// System.Net.ServicePointManager.CertificatePolicy = new TrustAllCertificatePolicy();
    /// </code>
    /// <para>
    ///  This must be done (one time during the application life cycle) before making the call to your webservice."
    /// </para>
    /// </remarks>
    internal class TrustAllCertificatePolicy : System.Net.ICertificatePolicy
    {
        public TrustAllCertificatePolicy() {}

        public bool CheckValidationResult( ServicePoint sp, X509Certificate cert, WebRequest req, int problem )
        {
            return true;
        }
    }
}
