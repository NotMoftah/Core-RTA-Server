using System.Reflection;
using System;


namespace CoreRTA
{
    class DelayedResponse
    {
        public string route;
        public object payload;
        public object instance;
        public MethodInfo methodInfo;
    }

}