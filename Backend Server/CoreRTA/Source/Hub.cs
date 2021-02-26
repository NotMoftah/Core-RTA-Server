using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CoreRTA
{
    public abstract class Hub : IDisposable
    {
        public Client Client { get; private set; }

        public void SetClient(Client client)
        {
            if (Client == null) Client = client;
        }

        #region Client Connection

        public virtual void OnClientConnected()
        {

        }

        public virtual void OnClientDisconnected()
        {

        }

        public virtual void OnClientTerminated()
        {

        }

        #endregion


        #region Client IO

        public virtual void OnClientData(string data)
        {

        }

        #endregion


        #region Functions
        public void SendAsync(string route, object data)
        {
            if (Client == null)
                return;

            Client.SendAsync(route, data);
        }

        public void SendAsync(string route, string data)
        {
            if (Client == null)
                return;

            Client.SendAsync(route, data);
        }

        public void SendAsync(string route)
        {
            if (Client == null)
                return;

            Client.SendAsync(route, string.Empty);
        }

        #endregion


        #region IDisposable
        public bool IsDisposed { get; private set; }

        public virtual void Dispose()
        {
            if (!IsDisposed)
            {
                Client = null;
                IsDisposed = true;

                GC.SuppressFinalize(this);
            }
        }

        #endregion
    }
}