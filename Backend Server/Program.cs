using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using CoreRTA;
using CoreRTA.Database;

namespace ShedApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Init new RTS APP
            var app = new RealtimeApp(IPAddress.Any, 4455, TimeSpan.FromMinutes(2));

            // Init the database
            RtaDb.Connect("rts-app");

            // Add the DB Collections
            RtaDb.CreateCollection("clients");

            // Add the Hubs
            app.AddHub<Hubs.AuthHub>();
            app.AddHub<Hubs.IndexHub>();
            app.AddHub<Hubs.PlayerHub>();

            // Run the App
            app.Run();
        }
    }
}
