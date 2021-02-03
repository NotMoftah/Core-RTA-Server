using System.Diagnostics;
using System.Reflection;
using System.Text;
using System;


namespace CoreRTA
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Route : Attribute
    {
        public string routeName;
        public Type requestType;

        public Route(string routeName)
        {
            this.routeName = routeName;
            this.requestType = typeof(string);
        }

        public Route(string routeName, Type requestType)
        {
            this.routeName = routeName;
            this.requestType = requestType;
        }
    }
}