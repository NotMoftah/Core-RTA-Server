using System;
using CoreRTA;
using CoreRTA.Database;

namespace ShedApp.Hubs
{
    public class AuthHub : Hub
    {
        public bool IsAuthnticated { get; private set; }


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
            System.Console.WriteLine(data);
        }

        #endregion


        [Route("auth/login", typeof(Views.LoginRequest))]
        void Login(Views.LoginRequest request)
        {
            var player = RtaDb.ReadDocument<Views.PlayerInfo>("clients", request.token);

            if (player != null && player.password == request.password)
            {
                IsAuthnticated = true;

                SendAsync("login", player);
            }
        }
    }
}