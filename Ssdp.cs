using System;
using System.Net;

using Rssdp;

public class SSDP {

    private static SsdpDevicePublisher _DevicePublisher;
    private static HttpListener _HttpServer;

    public static void Publish( string localIP, UInt32 localPort ) {
        if ( _DevicePublisher != null ) {
            _DevicePublisher.Dispose();
        }

        if ( _HttpServer != null ) {
            _HttpServer.Close();
        }

        // Create Publisher
        _DevicePublisher = new SsdpDevicePublisher();

        // For Compat
        _DevicePublisher.StandardsMode = SsdpStandardsMode.Relaxed;

        // Root SSDP Device to Publish
        // References IP:Port from GDB Server to advertise IP:Port for client
        var url = new Uri( "http://" + localIP + ":" + localPort );

        var rootDevice = new SsdpRootDevice() {
            CacheLifetime = TimeSpan.FromMinutes( 30 ),
            Manufacturer = "PSX",
            FriendlyName = "NOTPSXSERIAL",
            ModelName = "NOTPSXSERIAL",
            ModelDescription = localIP + ":" + localPort,
            Uuid = System.Guid.NewGuid().ToString(),

            UrlBase = url
        };

        // Set location field with URL and Port
        rootDevice.Location = new Uri( url, "nops" );

        // Start HTTP server for serving Device Description Document (XML)
        StartHttpServerForDdd( rootDevice, url );

        // Add root device to publisher
        _DevicePublisher.AddDevice( rootDevice );

        Console.WriteLine( "Starting SSDP Publisher" );
    }

    private static void StartHttpServerForDdd( SsdpRootDevice rootDevice, Uri url ) {
        _HttpServer = new HttpListener();
        var t = new System.Threading.Thread( ServeDeviceDescriptionDocument );
        t.IsBackground = true;
        t.Start( rootDevice );

        rootDevice.UrlBase = url;
        _HttpServer.Prefixes.Add( "http://+:8181/" );
        try {
            _HttpServer.Start();
            Console.WriteLine( "Starting HTTP Server for SSDP" );
        } catch ( Exception ) {
            Console.WriteLine( "Permission denied starting http listener. Please run app as elevated admin, or run the following command from elevated command prompt, changing domain\\user to be a valid Windows user account" );
            Console.WriteLine( "netsh http add urlacl url=http://+:8181/MyUri user=DOMAIN\\user" );
        }
    }

    private static void ServeDeviceDescriptionDocument( object rootDevice ) {
        try {
            var device = rootDevice as SsdpRootDevice;
            var dddBuffer = System.Text.UTF8Encoding.UTF8.GetBytes( device.ToDescriptionDocument() );

            while ( device != null && _HttpServer != null ) {
                var context = _HttpServer.GetContext();
                context.Response.OutputStream.Write( dddBuffer, 0, dddBuffer.Length );
                context.Response.OutputStream.Flush();
                context.Response.OutputStream.Close();
            }
        } catch { }
    }

}