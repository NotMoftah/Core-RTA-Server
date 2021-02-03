using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreRTA
{
    public class TcpServer
    {
        private TcpListener tcpListener;
        private readonly int tokenSize = 64;

        public bool IsStarted { get; private set; }
        public bool IsAccepting { get; private set; }
        public IPEndPoint Endpoint { get; private set; }
        public Action<Session> OnSessionReady { get; set; }

        public TcpServer(IPAddress address, int port) : this(new IPEndPoint(address, port)) { }
        public TcpServer(string address, int port) : this(new IPEndPoint(IPAddress.Parse(address), port)) { }
        public TcpServer(IPEndPoint endpoint)
        {
            Endpoint = endpoint;
            tcpListener = new TcpListener(endpoint);
        }


        public void Start()
        {
            if (IsStarted)
                return;

            // Create a new acceptor socket
            tcpListener.Start(32);
            IsAccepting = true;
            IsStarted = true;

            System.Console.WriteLine("Server Started!");

            while (IsAccepting)
            {
                // Accept new connection
                var connection = tcpListener.AcceptSocket();

                // Run the connection on seprate thread
                ThreadPool.QueueUserWorkItem<Socket>(SessionFactoryTask, connection, true);
            }
        }

        private void SessionFactoryTask(Socket socket)
        {
            if (socket != null)
            {
                try
                {
                    int offset = 0;
                    byte[] buffer = new byte[tokenSize];

                    // Wait til the session gives you the token.
                    while (offset < buffer.Length)
                        offset += socket.Receive(buffer, offset, buffer.Length - offset, SocketFlags.Partial);

                    // Creat the session with token.
                    string token = Encoding.UTF8.GetString(buffer);
                    Session session = new Session(socket, token);

                    // Notify Listeners
                    if (OnSessionReady != null)
                        OnSessionReady(session);
                }
                catch (System.Exception e)
                {
                    System.Console.WriteLine(string.Format("ERROR: {0} -> {1}", "SessionFactory", e.Message));
                }
            }
        }
    }
}
