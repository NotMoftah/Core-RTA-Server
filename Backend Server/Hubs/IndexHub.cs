using System;
using CoreRTA;
using CoreRTA.Database;

namespace ShedApp.Hubs
{
    public class IndexHub : Hub
    {
        public IndexHub()
        {

        }

        #region Connection Events

        public override void OnClientConnected()
        {
            System.Console.WriteLine("OnClientConnected");
        }
        public override void OnClientDisconnected()
        {
            System.Console.WriteLine("OnClientDisconnected");
        }
        public override void OnClientTerminated()
        {
            System.Console.WriteLine("OnClientTerminated");
        }
        public override void OnClientData(string data)
        {

        }

        #endregion


        [Route("index/ping")]
        void Ping(string payload)
        {
            SendAsync("index/ping", payload);
        }
    }
}