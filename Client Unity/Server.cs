using System.Collections.Concurrent;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using CoreTcp;
using System;

namespace CoreRTA
{
    public class Server : MonoBehaviour
    {

        #region Public Fields

        [HideInInspector]
        public static Action OnServerConnected;
        public static Action OnServerDisconnected;
        public static string DeviceID { get; private set; }


        [Header("Server Remote Point")]
        public string endPoint;
        public int port;
        #endregion

        #region Private Fields
        private static bool isInit = false;
        private static ClientSession session;

        private static List<object> routesHandler;
        private static ConcurrentQueue<string> events;
        private static Dictionary<string, List<handlerInfo>> routesMap;
        private static ConcurrentQueue<DelayedResponse> readyResponses;


        #endregion

        static Server()
        {
            routesHandler = new List<object>();
            events = new ConcurrentQueue<string>();
            routesMap = new Dictionary<string, List<handlerInfo>>();
            readyResponses = new ConcurrentQueue<DelayedResponse>();
        }


        #region Unity Managment
        void Awake()
        {
            Init(endPoint, port);

            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void Update()
        {
            // Server Events
            if (events != null && events.Count > 0)
            {
                if (gameObject.activeInHierarchy)
                {
                    while (events.Count > 0)
                    {
                        string eventName;

                        if (events.TryDequeue(out eventName))
                        {
                            if (eventName == "server_connected" && OnServerConnected != null)
                                foreach (var method in OnServerConnected.GetInvocationList())
                                    try { method.DynamicInvoke(); } catch (System.Exception) { }

                            if (eventName == "server_disconnected" && OnServerDisconnected != null)
                                foreach (var method in OnServerDisconnected.GetInvocationList())
                                    try { method.DynamicInvoke(); } catch (System.Exception) { }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }


            // Server Responses
            if (readyResponses != null && readyResponses.Count > 0)
            {
                if (gameObject.activeInHierarchy)
                {
                    while (readyResponses.Count > 0)
                    {
                        DelayedResponse response;

                        if (readyResponses.TryDequeue(out response))
                        {
                            if (response.instance != null)
                            {
                                response.methodInfo.Invoke(response.instance, new object[] { response.payload });
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        void OnApplicationQuit()
        {
            DisconnectAsync();
        }

        void OnSceneUnloaded(Scene scene)
        {
            RemovedDestroyedHandlers();

            if (OnServerConnected != null)
            {
                var listeners = OnServerConnected.GetInvocationList();

                OnServerConnected = null;

                foreach (var item in listeners)
                {
                    if (((MonoBehaviour)item.Target) != null)
                    {
                        OnServerConnected += (Action)item;
                    }
                }
            }

            if (OnServerDisconnected != null)
            {
                var listeners = OnServerDisconnected.GetInvocationList();
                OnServerDisconnected = null;

                foreach (var item in listeners)
                {
                    if (((MonoBehaviour)item.Target) != null)
                    {
                        OnServerDisconnected += (Action)item;
                    }
                }
            }

        }

        #endregion


        #region Public Fields

        public static bool IsConnected
        {
            private set {; }
            get
            {
                if (session == null)
                    return false;

                return session.IsConnected;
            }
        }
        public static bool IsConnecting
        {
            private set {; }
            get
            {
                if (session == null)
                    return false;

                return session.IsConnecting;
            }
        }

        public static void Init(string ip, int port)
        {
            if (session == null)
            {
                string token = SystemInfo.deviceUniqueIdentifier;
                session = new ClientSession(ip, port, token);
                DeviceID = session.DeviceID;

                session.OnSegmentEvent += BroadcasrResponse;
                session.OnConnectedEvent += OnSessionConnected;
                session.OnDisconnectedEvent += OnSessionDisconnected;
            }

            isInit = true;
        }

        #endregion


        #region Server Connection Management
        private static void OnSessionConnected()
        {
            events.Enqueue("server_connected");
        }

        private static void OnSessionDisconnected()
        {
            events.Enqueue("server_disconnected");
        }

        public static async void ConnectAsync()
        {
            if (!isInit) return;

            session.ConnectAsync();

            while (session.IsConnecting)
                await Task.Delay(100);
        }

        public static async void ReconnectAsync()
        {
            if (!isInit) return;

            session.DisconnectAsync();

            while (session.IsConnected)
                await Task.Delay(100);

            session.ConnectAsync();

            while (session.IsConnecting)
                await Task.Delay(100);
        }

        public static async void DisconnectAsync()
        {
            if (!isInit) return;

            session.DisconnectAsync();

            while (session.IsConnected)
                await Task.Delay(100);
        }

        #endregion


        #region Server Route Handlers Managment

        public static void AddRouteHandler(object handler)
        {
            if (routesHandler.Contains(handler))
                return;

            routesHandler.Add(handler);

            foreach (var method in handler.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                try
                {
                    var attrs = method.GetCustomAttributes(typeof(Route), true);

                    if (attrs.Length > 0 && attrs[0] != null)
                    {
                        var requestType = ((Route)attrs[0]).requestType;
                        var routeName = ((Route)attrs[0]).routeName;

                        if (!routesMap.ContainsKey(routeName.ToLower()))
                            routesMap[routeName.ToLower()] = new List<handlerInfo>();

                        routesMap[routeName.ToLower()].Add(new handlerInfo
                        {
                            instance = handler,
                            methodInfo = method,
                            routeName = routeName,
                            requestType = requestType
                        });
                    }
                }
                catch (System.Exception e) { Debug.LogError(e.Message); }
            }
        }

        public static void RemovedDestroyedHandlers()
        {
            foreach (var route in routesMap)
                route.Value.RemoveAll(x => (x.instance as MonoBehaviour) == null);

            routesHandler.RemoveAll(x => (x as MonoBehaviour) == null);
        }

        public static void ClearHandlers()
        {
            routesMap = new Dictionary<string, List<handlerInfo>>();
        }

        private static void BroadcasrResponse(string route, string payload)
        {
            Debug.LogWarning(string.Format("{0}: {1}", route, payload));

            if (routesMap.TryGetValue(route.ToLower(), out List<handlerInfo> routers))
            {
                foreach (var responseHnadler in routesMap[route.ToLower()])
                {
                    try
                    {
                        if (responseHnadler.instance != null)
                        {
                            if (responseHnadler.requestType == typeof(string))
                            {
                                readyResponses.Enqueue(new DelayedResponse
                                {
                                    route = route,
                                    payload = payload,
                                    instance = responseHnadler.instance,
                                    methodInfo = responseHnadler.methodInfo
                                });
                            }
                            else if (payload.Length > 0)
                            {
                                readyResponses.Enqueue(new DelayedResponse
                                {
                                    route = route,
                                    payload = JsonUtility.FromJson(payload, responseHnadler.requestType),
                                    instance = responseHnadler.instance,
                                    methodInfo = responseHnadler.methodInfo
                                });
                            }
                            else
                            {
                                readyResponses.Enqueue(new DelayedResponse
                                {
                                    route = route,
                                    payload = null,
                                    instance = responseHnadler.instance,
                                    methodInfo = responseHnadler.methodInfo
                                });
                            }
                        }
                    }
                    catch (System.Exception e) { Debug.LogError("ERROR DESERILIZE ROUTE " + route + "\n" + e.Message); }
                }
            }
        }

        #endregion


        #region Server Request Managment

        public static bool SendRequest(string type)
        {
            if (!isInit) return false;

            return session.SendFormatedRequest(type, string.Empty);
        }

        public static bool SendRequest(string type, string json)
        {
            if (!isInit) return false;

            return session.SendFormatedRequest(type, json);
        }

        public static bool SendRequest(string type, object payload)
        {
            if (!isInit) return false;

            string json = JsonUtility.ToJson(payload);

            return session.SendFormatedRequest(type, json);
        }

        #endregion

    }

}
