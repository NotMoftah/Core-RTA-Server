using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using System.Net;

namespace CoreRTA
{
    public class RealtimeApp
    {
        private TcpServer server;

        public TimeSpan ClientTimeout { get; private set; }


        public RealtimeApp(IPAddress address, int port, TimeSpan clientTimeout)
        {
            // Setup variables
            this.ClientTimeout = clientTimeout;

            // Init the server
            server = new TcpServer(address, port);
            server.OnSessionReady += OnSessionReady;
        }

        public void Run()
        {
            ServerMonitoringLoop();

            server.Start();
        }


        #region Clients and Sessions Managment
        public List<Client> Clients { get; private set; } = new List<Client>();

        private async void ServerMonitoringLoop()
        {
            // Initial delay
            await Task.Delay(TimeSpan.FromSeconds(10));

            // Serving loop
            while (server.IsAccepting)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                var removedQueue = new List<Client>();

                // Check All Clients
                foreach (var client in Clients.FindAll(x => x.IsConnected == false))
                {
                    if ((DateTime.Now - client.LastUpdateTime).Duration() > ClientTimeout)
                    {
                        removedQueue.Add(client);
                        client.Terminate();
                    }
                }

                // Remove all inactive clients from the list
                Clients.RemoveAll(x => removedQueue.Contains(x));
            }
        }

        private Client GetClient(string deviceId)
        {
            // If client alreay exist. just bring it
            var client = Clients.Find(x => x.DeviceID == deviceId);

            // Create New Client and Add it to Clients List
            if (client == null)
            {
                client = new Client(deviceId);
                Clients.Add(client);

                // Init hubs for the new client
                foreach (var kvp in hubRoutesDict)
                {
                    var hubObj = new HubObject(kvp.Key, kvp.Value, client);
                    client.AddHubObject(hubObj);
                }
            }

            return client;
        }

        private void OnSessionReady(Session session)
        {
            string deviceId = session.Token;

            var client = GetClient(deviceId);
            client.UpdateSession(session);

            session.Start();
        }

        #endregion


        #region Hubs Managment
        private Dictionary<Type, Dictionary<string, RouteInfo>> hubRoutesDict = new Dictionary<Type, Dictionary<string, RouteInfo>>();

        public void AddHub<T>() where T : Hub
        {
            Type type = typeof(T);
            hubRoutesDict[type] = new Dictionary<string, RouteInfo>();

            // Extract all the routes from the hub and add them to a list
            foreach (var method in type.GetRuntimeMethods())
            {
                var route = method.GetCustomAttribute<Route>();

                if (route != null)
                {
                    var routeInfo = new RouteInfo(route, method);
                    hubRoutesDict[type][routeInfo.Name.ToLower()] = routeInfo;
                }
            }
        }

        #endregion
    }
}