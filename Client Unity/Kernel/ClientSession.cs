using System;
using System.Text;


namespace CoreTcp
{
    class ClientSession : TcpClient
    {
        private string dataBuffer = string.Empty;
        private readonly string EndOfRoute = "<$ROUTE/>";
        private readonly string EndOfMessage = "<$MSG/>";

        public Action OnConnectedEvent;
        public Action OnDisconnectedEvent;
        public Action<string, string> OnSegmentEvent;
        public string DeviceID { get; private set; }


        public ClientSession(string address, int port, string token) : base(address, port, token)
        {
            this.DeviceID = base.Token;
            base.OptionKeepAlive = true;
        }


        #region Connection Events
        protected override void OnConnected()
        {
            // Send <Session-Ready-Signal>
            base.SendAsync(DeviceID);

            if (OnConnectedEvent != null)
                OnConnectedEvent();
        }

        protected override void OnDisconnected()
        {
            if (OnDisconnectedEvent != null)
                OnDisconnectedEvent();
        }

        #endregion


        #region Session IO Outgoing Managment

        public new bool SendAsync(string payload)
        {
            return base.SendAsync(payload + EndOfMessage);
        }

        public bool SendFormatedRequest(string route, string payload)
        {
            return SendAsync(route + EndOfRoute + payload + EndOfMessage);
        }


        #endregion


        #region Session IO Incoming Managment

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Decode the raw bytes and appened them to buffer
            dataBuffer += Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

            // Split the string
            var segments = dataBuffer.Split(new string[] { EndOfMessage }, StringSplitOptions.None);
            var validSegmentsCount = segments.Length - (dataBuffer.EndsWith(EndOfMessage) ? 0 : 1);

            // Extract request from segments and emit them
            for (int i = 0; i < validSegmentsCount; i++)
            {
                if (segments[i].Length > 0)
                {
                    // Notify the server
                    ExtractFormatedRequestAndEmit(segments[i]);
                }
            }

            // Add last segment to the buffer for further processing if not valid
            dataBuffer = dataBuffer.EndsWith(EndOfMessage) ? string.Empty : segments[segments.Length - 1];
        }

        private void ExtractFormatedRequestAndEmit(string data)
        {
            // Extract request and broadcast
            var chunks = data.Split(new string[] { EndOfMessage }, StringSplitOptions.None);
            if (chunks.Length == 2)
            {
                // Emit
                OnSegmentEvent(chunks[0], chunks[1]);
            }

        }

        #endregion

    }
}
