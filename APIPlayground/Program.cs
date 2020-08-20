using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Connections;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

#region System.Net.Connections

// This is an echo server using the server side connection abstractions
async Task RunServer(ConnectionListenerFactory connectionListenerFactory, EndPoint endPoint)
{
    await using ConnectionListener listener = await connectionListenerFactory.ListenAsync(endPoint);

    Console.WriteLine($"Server listening to {listener.LocalEndPoint}");

    while (true)
    {
        await using Connection connection = await listener.AcceptAsync();

        Task Echo()
        {
            return connection.Pipe.Input.CopyToAsync(connection.Pipe.Output);
        }

        // Run the echo server and go back to waiting for new connections
        _ = Task.Run(Echo);
    }
}

// This is an echo client using the client side connection abstraction
async Task RunClient(ConnectionFactory connectionFactory, EndPoint endPoint)
{
    await using Connection connection = await connectionFactory.ConnectAsync(endPoint);

    var input = Console.OpenStandardInput().CopyToAsync(connection.Stream);
    var output = connection.Stream.CopyToAsync(Console.OpenStandardOutput());

    await Task.WhenAll(input, output);
}

// This is an in memory transport implementation of both interfaces
var transport = new MemoryTransport();
var endpoint = new MemoryEndPoint("default");

var serverTask = RunServer(transport.ConnectionListenerFactory, endpoint);
var clientTask = RunClient(transport.ConnectionFactory, endpoint);

await Task.WhenAll(serverTask, clientTask);

#endregion

#region System.Net.Connections with HttpClient

async Task RunHttpServer(ConnectionListenerFactory connectionListenerFactory, EndPoint endPoint)
{
    await using var listener = await connectionListenerFactory.ListenAsync(endPoint);

    Console.WriteLine($"HTTP listening to {listener.LocalEndPoint}");

    while (true)
    {
        await using var connection = await listener.AcceptAsync();

        async Task DoHttp()
        {
            var input = connection.Pipe.Input;

            while (true)
            {
                // Assume things fit into a single buffer
                var result = await input.ReadAsync();
                var buffer = result.Buffer;

                // New API that turns an ReadOnlySequence<byte> into a string
                Console.WriteLine(Encoding.UTF8.GetString(buffer));

                input.AdvanceTo(buffer.End);

                // This is a new API that writes a ReadOnlySpan<char> into a IBufferWriter<byte>.
                // This lets us write data into the pipe writing allocating temporary buffers.
                Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: 11\r\nConnection: Keep-Alive\r\n\r\nHello World".AsSpan(), connection.Pipe.Output);
                await connection.Pipe.Output.FlushAsync();
            }
        }

        // Handle this connection's HTTP requests and accept more connections
        _ = Task.Run(DoHttp);
    }
}

// Tell the SocketsHttpHandler to use our custom ConnectionFactory instance
var socketsHandler = new SocketsHttpHandler
{
    ConnectionFactory = transport.ConnectionFactory
};

var httpServerTask = RunHttpServer(transport.ConnectionListenerFactory, endpoint);

using var client1 = new HttpClient(socketsHandler);
using var response1 = await client1.GetAsync("http://default/");
Console.WriteLine(response1);

await httpServerTask;

#endregion

#region Synchronous HttpClient

var client2 = new HttpClient();
var response2 = client2.Send(new HttpRequestMessage(HttpMethod.Get, "http://www.google.com"));
var stream = response2.Content.ReadAsStream();
Console.WriteLine(new StreamReader(stream).ReadToEnd());

#endregion

#region JSON

// Immutable types
var (name, age) = JsonSerializer.Deserialize<Person>("{\"Age\":33, \"Name\":\"David\"}");
Console.WriteLine((name, age));

var person2 = JsonSerializer.Deserialize<Person2>("{\"Age\":25, \"Name\":\"John\"}");
Console.WriteLine($"{person2.Name} is {person2.Age} years old");

// Fields
var student = JsonSerializer.Deserialize<Student>("{\"Name\": \"Scott\", \"GPA\": 4}", new JsonSerializerOptions { IncludeFields = true });
Console.WriteLine($"{student.Name} with a GPA of {student.GPA}");

// For Web
var s = JsonSerializer.Serialize(new Person("Damian", 29), new JsonSerializerOptions(JsonSerializerDefaults.Web));
Console.WriteLine(s);

// Reference Loop Handling
var janeEmployee = new Employee
{
    Name = "Jane Doe"
};

var johnEmployee = new Employee
{
    Name = "John Smith"
};

janeEmployee.Reports = new List<Employee> { johnEmployee };
johnEmployee.Manager = janeEmployee;

var options = new JsonSerializerOptions
{
    ReferenceHandler = ReferenceHandler.Preserve
};

string serialized = JsonSerializer.Serialize(janeEmployee, options);
Console.WriteLine(serialized);

Employee janeDeserialized = JsonSerializer.Deserialize<Employee>(serialized, options);
Console.WriteLine(janeDeserialized.Reports[0].Manager == janeDeserialized);

#endregion

#region HttpClient and JSON

using var client = new HttpClient();

// These are new overloads for getting and posting JSON over HTTP using System.Text.Json
var person = await client.GetFromJsonAsync<Person>("http://localhost:5000/");
var response = await client.PostAsJsonAsync("http://localhost:5000/", new { Name = "John", Age = 200 });
var personAgain = await response.Content.ReadFromJsonAsync<Person>();

#endregion

#region PEM support

// The PEM files aren't included in this project for obvious reasons :)

// Single cert
var certificate2 = new X509Certificate2("fullchain.pem");

// All certs in PEM format
var certs = new X509Certificate2Collection();
certs.ImportFromPemFile("fullchain.pem");

// Import the private key
var rsa = RSA.Create();
rsa.ImportFromPem(File.ReadAllText("key.pem"));
var cert = certs[0];
cert = cert.CopyWithPrivateKey(rsa);

// Cert with private key
using var certWithPrivateKey = X509Certificate2.CreateFromPemFile("cert.pem", "key.pem");
var onlyChain = new X509Certificate2Collection();
onlyChain.ImportFromPemFile("chain.pem");

// Create the SSL Stream context with the sslcert *and* the intermedite chain
var certficateContext = SslStreamCertificateContext.Create(certWithPrivateKey, onlyChain, offline: true);

async Task RunTlsServer(ConnectionListenerFactory connectionListenerFactory, EndPoint endPoint)
{
    await using var listener = await connectionListenerFactory.ListenAsync(endPoint);

    Console.WriteLine($"Server listening to {listener.LocalEndPoint}");

    while (true)
    {
        var connection = await listener.AcceptAsync();

        async Task DoTls()
        {
            // Make an SSLStream over the connection's data stream
            using var sslStream = new SslStream(connection.Stream, leaveInnerStreamOpen: true);

            // Do the handshake
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions()
            {
                ServerCertificateContext = certficateContext
            });

            // Make a new encrypted connection over the new SSLStream
            using var encryptedConnection = Connection.FromStream(sslStream, leaveOpen: true, connection.ConnectionProperties, connection.LocalEndPoint, connection.RemoteEndPoint);

            // Use the pipe to implement an echo server over the encrypted pipe
            await encryptedConnection.Pipe.Input.CopyToAsync(encryptedConnection.Pipe.Output);
        }

        _ = Task.Run(DoTls);
    }
}

async Task RunTlsClient(ConnectionFactory connectionFactory, EndPoint endPoint)
{
    await using var connection = await connectionFactory.ConnectAsync(endPoint);

    using var sslStream = new SslStream(connection.Stream);

    await sslStream.AuthenticateAsClientAsync("tls");

    using var encryptedConnection = Connection.FromStream(sslStream, leaveOpen: true, connection.ConnectionProperties, connection.LocalEndPoint, connection.RemoteEndPoint);

    var input = Console.OpenStandardInput().CopyToAsync(encryptedConnection.Stream);
    var output = encryptedConnection.Stream.CopyToAsync(Console.OpenStandardOutput());

    await Task.WhenAll(input, output);
}

var tlsTransport = new MemoryTransport();
var tlsEndpoint = new MemoryEndPoint("tls");

var tlsServerTask = RunTlsServer(tlsTransport.ConnectionListenerFactory, tlsEndpoint);
var tlsClientTask = RunTlsClient(tlsTransport.ConnectionFactory, tlsEndpoint);

await Task.WhenAll(tlsServerTask, tlsClientTask);

#endregion

#region Logging

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();

    #region JSON Console

    builder.AddJsonConsole(o =>
    {
        o.JsonWriterOptions = new JsonWriterOptions { Indented = true };
        o.IncludeScopes = true;
    });

    #endregion

    #region Single line formatting 

    // Single line console formatting console logs. This forces all log lines to be a single line.

    //builder.AddSimpleConsole(o =>
    //{
    //    o.SingleLine = true;
    //});

    #endregion

    #region Activity

    // This will make sure that all logs include the current activity information as part of the scope
    builder.Configure(o =>
    {
        o.ActivityTrackingOptions = ActivityTrackingOptions.ParentId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId;
    });

    #endregion
});

var op = new Activity("MyOperation");
op.Start();
var logger = loggerFactory.CreateLogger("ConsoleApp1");
logger.LogInformation("Hello World");
op.Stop();

#endregion

#region ActivitySource

// The listener lets consumers access activities that are started or stopped
using var listener = new ActivityListener();
listener.ActivityStarted = activity => { };
listener.ActivityStopped = activity => { };
listener.ShouldListenTo = activitySource => true;
listener.GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> context) => ActivityDataRequest.AllData;

ActivitySource.AddActivityListener(listener);

using var source = new ActivitySource("ConsoleApp1");

// Creating 2 nested activities
{
    using var activity = source.StartActivity("Request");
    Console.WriteLine(activity.Id);

    using var childActivty = source.StartActivity("Outgoing");
    Console.WriteLine(childActivty.Id);
}

// Creating another activity
{
    using var activity = source.StartActivity("Request");
    Console.WriteLine(activity.Id);
}
#endregion

#region Tasks

// New non-generic TaskCompletionSource
TaskCompletionSource tcs = new();
tcs.TrySetResult();

var vt = ValueTask.FromResult(new object());
var vtct = ValueTask.CompletedTask;
var vte = ValueTask.FromException(new Exception());
var vtc = ValueTask.FromCanceled(new CancellationToken(true));

#endregion

#region GC

// GC Allocate array is similar to new T[] but allows specifying special GC options
byte[] array = GC.AllocateArray<byte>(100);

// AllocateUninitializedArray will no zero the memory before returning the array to the caller
// this can be dangerous but if you're going to immediately fill the array then why spend time zeroing it??
var uncleanZeroedArray = GC.AllocateUninitializedArray<byte>(100);

// Allocate a large array as pinned on the new pinned object heap (POH)
var pinnedBuffer = GC.AllocateUninitializedArray<char>(1024 * 1024, pinned: true);

// Get all of the GC memory information
GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();
Console.WriteLine(memoryInfo);

#endregion

#region Sockets

// Does this platform support unix domain sockets?
Console.WriteLine(Socket.OSSupportsUnixDomainSockets);

// You can also set and get raw socket options
int SOL_SOCKET;
int SO_RCVBUF;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
    RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    SOL_SOCKET = 0xffff;
    SO_RCVBUF = 0x1002;
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    SOL_SOCKET = 1;
    SO_RCVBUF = 8;
}
else
{
    throw new NotSupportedException();
}

using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
const int SetSize = 8192;
int ExpectedGetSize =
    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? SetSize * 2 : // Linux kernel documented to double the size
    SetSize;

socket.SetRawSocketOption(SOL_SOCKET, SO_RCVBUF, BitConverter.GetBytes(SetSize));

var buffer = new byte[sizeof(int)];
var option = socket.GetRawSocketOption(SOL_SOCKET, SO_RCVBUF, buffer);



#endregion

record Person(string Name, int Age);

public class Person2
{
    // Ambiguity in constructors means we need to tell the JsonSerializer which one to use
    [JsonConstructor]
    public Person2(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public Person2(string firstname, string lastName, int age) : this(firstname + lastName, age)
    {

    }

    public string Name { get; }
    public int Age { get; }
}

public class Student
{
    public int GPA;
    public string Name;
}

interface IPlugin
{

}

class Employee
{
    public string Name { get; set; }

    public Employee Manager { get; set; }

    public List<Employee> Reports { get; set; }
}