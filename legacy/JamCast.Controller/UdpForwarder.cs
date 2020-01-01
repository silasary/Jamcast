using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JamCast.Controller
{
    public class UdpForwarder
    {
        private readonly UdpClient _localClient;
        private readonly UdpClient _targetClient;

        public UdpForwarder(int port, int targetPort)
        {
            _localClient = new UdpClient(port);
            _targetClient = new UdpClient();
            _targetClient.Connect(new IPEndPoint(IPAddress.Loopback, targetPort));

            _targetClient.Client.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoDelay, true);
            //_localClient.Client.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.ReuseAddress, true);

            var thread = new Thread(Run);
            thread.Name = "UDP Forwarder " + port + " -> " + targetPort;
            thread.IsBackground = true;
            thread.Start();

            ExpectedIpAddress = IPAddress.Loopback;
        }

        private void Run()
        {
            while (true)
            {
                try
                {
                    IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, 9090);
                    var pkt = _localClient.Receive(ref endpoint);
                    if (pkt != null)
                    {
                        if (IPAddress.Equals(endpoint.Address, ExpectedIpAddress))
                        {
                            _targetClient.Send(pkt, pkt.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        OnError?.Invoke(ex);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public IPAddress ExpectedIpAddress { get; set; }

        public Action<Exception> OnError { get; set; }
    }
}
