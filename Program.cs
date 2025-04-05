using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Tester
{
    internal class Program
    {
 
        static void Main(string[] args)
        {
            TcpServer tcpServer = new TcpServer();
            tcpServer.OnConnected += TcpServerConnectedHandler;
            tcpServer.OnDisconnected += TcpServerClientDisconnectedHandler;
            tcpServer.Start(IPAddress.Any, 8080);
            Console.WriteLine("Listening...");
            Console.ReadKey(true);
            Console.WriteLine("Stopping sever...");
            tcpServer.Stop();
        }

        private static void TcpServerClientDisconnectedHandler(object? sender, EventArgs e)
        {
            Console.WriteLine("Client disconnected");
        }

        private static void TcpServerConnectedHandler(object? sender, EventArgs e)
        {
            Console.WriteLine("Client connected");
        }
    }
}
