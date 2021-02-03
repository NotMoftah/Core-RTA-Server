using System.Reflection;
using System;

namespace CoreRTA
{
    class handlerInfo
    {
        public object instance;
        public Type requestType;
        public string routeName;
        public MethodInfo methodInfo;
    }

}