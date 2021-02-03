using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.Json;
using System.Net;
using System;

namespace CoreRTA
{
    public class RouteInfo
    {
        public MethodInfo Method { get; private set; }
        public Type DataType { get; private set; }
        public string Name { get; private set; }

        public RouteInfo(Route route, MethodInfo method)
        {
            this.Name = route.RouteName.ToLower();
            this.DataType = route.RouteDataType;
            this.Method = method;
        }
    }
}