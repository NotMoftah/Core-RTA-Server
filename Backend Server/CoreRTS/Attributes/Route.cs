using System;

namespace CoreRTA
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Route : Attribute
    {
        public string RouteName { get; private set; }
        public Type RouteDataType { get; private set; }

        public Route(string routeName)
        {
            this.RouteName = routeName;
            this.RouteDataType = typeof(string);
        }

        public Route(string routeName, Type requestType)
        {
            this.RouteName = routeName;
            this.RouteDataType = requestType;
        }
    }
}