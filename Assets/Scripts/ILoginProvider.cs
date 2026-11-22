using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Duende.IdentityModel.OidcClient;

public abstract class ILoginProvider
{
    public abstract Task<AuthenticationResult> LoginAsync();

    protected static int GetFreePort()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();

        int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();

        return port;
    }
}
