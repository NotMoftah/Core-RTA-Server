using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.Json;
using System.Net;
using System;


namespace CoreRTA
{
    public class HubObject
    {
        public Type Type { get; private set; }
        public object Instance { get; private set; }
        public Dictionary<string, RouteInfo> Routes { get; private set; }


        public HubObject(Type type, Dictionary<string, RouteInfo> routes, Client client)
        {
            this.Type = type;
            this.Routes = routes;
            this.Instance = Activator.CreateInstance(type);

            // Add client ref to the object
            (this.Instance as Hub).SetClient(client);
        }

        public void InvokeRoute(string routeName, string data)
        {
            if (Routes.TryGetValue(routeName.ToLower(), out RouteInfo route))
            {
                if (string.IsNullOrWhiteSpace(data) || route.DataType == typeof(string))
                {
                    route.Method.Invoke(Instance, new string[] { data });
                }
                else
                {
                    var request = JsonSerializer.Deserialize(data, route.DataType);
                    route.Method.Invoke(Instance, new object[] { request });
                }
            }
        }
    }
}