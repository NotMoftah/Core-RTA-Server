using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;


namespace CoreRTA
{
    public class Client : IDisposable
    {
        private static ulong counter = 0;

        public ulong ServerID { get; private set; }
        public string DeviceID { get; private set; }
        public bool IsConnected { get; private set; }
        public DateTime LastUpdateTime { get; private set; }

        private List<HubObject> Hubs { get; set; }

        public Client(string deviceId)
        {
            this.DeviceID = deviceId;
            this.ServerID = Client.counter++;

            this.Hubs = new List<HubObject>();
        }

        public void Terminate()
        {
            // Terminate the session
            UpdateSession(null);

            // Notifiy listners
            for (int i = 0; i < Hubs.Count; i++)
                (Hubs[i].Instance as Hub).OnClientTerminated();

            // Dispose the client
            Dispose();
        }


        #region Hubs Managment

        public void AddHubObject(HubObject hub)
        {
            Hubs.Add(hub);
        }
        public T GetHub<T>() where T : Hub
        {
            for (int i = 0; i < Hubs.Count; i++)
                if (Hubs[i].Type == typeof(T))
                    return (T)Hubs[i].Instance;

            return null;
        }

        #endregion


        #region Session Managament

        private Session session;

        public void UpdateSession(Session session)
        {
            // If current session still alive. kill it
            if (this.session != null)
            {
                // Unsubscribe the session
                this.session.OnSessionMessage -= OnSessionMessage;
                this.session.OnSessionDisconnected -= OnSessionDisconnected;

                // Notifiy listners
                for (int i = 0; i < Hubs.Count; i++)
                    (Hubs[i].Instance as Hub).OnClientDisconnected();

                // Kill it.
                this.session.Stop();
                IsConnected = false;
                this.session.Dispose();
            }

            // If the session disconnected and there's no sessions anymore
            if (session == null)
            {
                IsConnected = false;
                this.session = null;
            }
            else
            {
                // Hook Events
                session.OnSessionMessage += OnSessionMessage;
                session.OnSessionDisconnected += OnSessionDisconnected;

                // Update Active Session
                this.IsConnected = true;
                this.session = session;

                // Notifiy listners
                for (int i = 0; i < Hubs.Count; i++)
                    (Hubs[i].Instance as Hub).OnClientConnected();
            }

            // Update last active state
            LastUpdateTime = DateTime.Now;
        }

        private void OnSessionDisconnected()
        {
            UpdateSession(null);
        }

        #endregion


        #region Hubs IO Managment
        private readonly string RouteEndMark = "<$ROUTE/>";

        public void SendAsync(string route, object payload)
        {
            if (session != null && session.IsConnected)
            {
                session.SendAsync(ParseRequest(route, JsonSerializer.Serialize(payload)));
            }
        }

        public void SendAsync(string route, string payload)
        {
            if (session != null && session.IsConnected)
            {
                session.SendAsync(ParseRequest(route, payload));
            }
        }

        public void SendAsync(string route)
        {
            if (session != null && session.IsConnected)
            {
                session.SendAsync(ParseRequest(route, string.Empty));
            }
        }

        private void OnSessionMessage(string message)
        {
            // Notifiy listners
            for (int i = 0; i < Hubs.Count; i++)
                (Hubs[i].Instance as Hub).OnClientData(message);

            try
            {
                // Extract request and broadcast
                var chunks = message.Split(RouteEndMark);
                if (chunks.Length == 2)
                {
                    for (int i = 0; i < Hubs.Count; i++)
                    {
                        Hubs[i].InvokeRoute(chunks[0], chunks[1]);
                    }
                }
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine(string.Format("ERROR: {0} -> {1}", "OnSessionMessage", e.Message));
            }
        }

        string ParseRequest(string route, string payload)
        {
            return route + RouteEndMark + payload;
        }

        #endregion


        #region IDisposable

        public bool IsDisposed { get; private set; }

        // Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposingManagedResources)
        {
            if (!IsDisposed)
            {
                if (disposingManagedResources)
                {
                    // Dispose managed resources here...
                    for (int i = 0; i < Hubs.Count; i++)
                        (Hubs[i].Instance as Hub).Dispose();
                }

                // Set large fields to null here...
                Hubs.Clear();

                // Mark as disposed.
                IsDisposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~Client()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        #endregion

    }
}