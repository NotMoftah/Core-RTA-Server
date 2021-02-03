using System;
using CoreRTA;
using CoreRTA.Database;

namespace ShedApp.Hubs
{
    public class AuthHub : Hub
    {
        public AuthHub()
        {

        }

        #region Connection Events

        public override void OnClientConnected()
        {

        }
        public override void OnClientDisconnected()
        {

        }
        public override void OnClientTerminated()
        {

        }
        public override void OnClientData(string data)
        {

        }

        #endregion


        [Route("PlayerStatus")]
        void Ping(string payload)
        {
            System.Console.WriteLine(payload);
        }
    }
}