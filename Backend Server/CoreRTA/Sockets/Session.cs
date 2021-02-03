using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Text.Json;
using System.Text;
using System;


namespace CoreRTA
{
    public class Session : IDisposable
    {

        #region Connection Public Pramaters

        // Pipe Info
        public string Token { get; private set; }
        public Socket Socket { get; private set; }

        // Events
        public Action<string> OnSessionMessage { get; set; }
        public Action OnSessionDisconnected { get; set; }


        // Option: Buffer size
        public int OptionReceiveBufferSize { get; set; } = 8192;
        public int OptionSendBufferSize { get; set; } = 8192;

        // Statics
        public long BytesReceived { get; private set; }
        public long BytesSent { get; private set; }

        #endregion

        public Session(Socket socket, string token)
        {
            this.Socket = socket;
            this.Token = token;
        }


        #region Connect/Disconnect Session
        public bool IsConnected { get; private set; }

        public void Start()
        {
            if (IsConnected)
                return;

            // Update the session socket disposed flag
            IsSocketDisposed = false;

            // Setup buffers
            dataBuffer = string.Empty;
            receiveBuffer = new Buffer();
            sendBufferMain = new Buffer();
            sendBufferFlush = new Buffer();

            // Setup recieve event args
            receiveEventArg = new SocketAsyncEventArgs();
            receiveEventArg.Completed += OnAsyncCompleted;

            // Setup send event args
            sendEventArg = new SocketAsyncEventArgs();
            sendEventArg.Completed += OnAsyncCompleted;

            // Apply the option: no delay
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            // Apply the option: keep alive
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            // Prepare receive & send buffers
            sendBufferMain.Reserve(OptionSendBufferSize);
            sendBufferFlush.Reserve(OptionSendBufferSize);
            receiveBuffer.Reserve(OptionReceiveBufferSize);

            // Reset statistic
            BytesSent = 0;
            BytesReceived = 0;

            // Update the connected flag
            IsConnected = true;

            // Call the session Connected handler
            OnConnected();

            // Run Session
            StartReceive();
        }
        public void Stop()
        {
            if (!IsConnected)
                return;

            // Reset event args
            receiveEventArg.Completed -= OnAsyncCompleted;
            sendEventArg.Completed -= OnAsyncCompleted;

            try
            {
                try
                {
                    // Shutdown the socket associated with the client
                    Socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException) { }

                // Close the session socket
                Socket.Close();

                // Dispose the session socket
                Socket.Dispose();

                // Dispose event arguments
                receiveEventArg.Dispose();
                sendEventArg.Dispose();

                // Update the session socket disposed flag
                IsSocketDisposed = true;
            }
            catch (ObjectDisposedException) { }

            // Update the connected flag
            IsConnected = false;

            // Update sending/receiving flags
            receiving = false;
            sending = false;

            // Clear send/receive buffers
            ClearBuffers();

            // Call the session disconnected handler
            OnDisconnected();
        }

        #endregion

        #region Send/Recieve Raw Data

        // Message Markers
        private readonly string MessageEndMark = "<$MSG/>";

        // Receive buffer
        private bool receiving;
        private string dataBuffer;
        private Buffer receiveBuffer;
        private SocketAsyncEventArgs receiveEventArg;

        // Send buffer
        private bool sending;
        private Buffer sendBufferMain;
        private Buffer sendBufferFlush;
        private long sendBufferFlushOffset;
        private SocketAsyncEventArgs sendEventArg;
        private readonly object sendLock = new object();

        private void StartSend()
        {
            if (sending)
                return;

            if (!IsConnected)
                return;

            if (!string.IsNullOrWhiteSpace(Token) && OnSessionMessage == null)
                return;

            bool process = true;

            while (process)
            {
                process = false;

                lock (sendLock)
                {
                    if (sending)
                        return;

                    // Swap send buffers
                    if (sendBufferFlush.IsEmpty)
                    {
                        // Swap flush and main buffers
                        sendBufferFlush = Interlocked.Exchange(ref sendBufferMain, sendBufferFlush);
                        sendBufferFlushOffset = 0;

                        sending = !sendBufferFlush.IsEmpty;
                    }
                    else
                        return;
                }

                // Check if the flush buffer is empty
                if (sendBufferFlush.IsEmpty)
                {
                    // Call the empty send buffer handler
                    return;
                }

                try
                {
                    // Async write with the write handler
                    sendEventArg.SetBuffer(sendBufferFlush.Data, (int)sendBufferFlushOffset, (int)(sendBufferFlush.Size - sendBufferFlushOffset));
                    if (!Socket.SendAsync(sendEventArg))
                        process = ProcessSend(sendEventArg);
                }
                catch (ObjectDisposedException) { }
            }
        }
        private void StartReceive()
        {
            if (receiving)
                return;

            if (!IsConnected)
                return;

            if (!string.IsNullOrWhiteSpace(Token) && OnSessionMessage == null)
                return;

            bool process = true;

            while (process)
            {
                process = false;

                try
                {
                    // Async receive with the receive handler
                    receiving = true;
                    receiveEventArg.SetBuffer(receiveBuffer.Data, 0, (int)receiveBuffer.Capacity);
                    if (!Socket.ReceiveAsync(receiveEventArg))
                        process = ProcessReceive(receiveEventArg);
                }
                catch (ObjectDisposedException) { }
            }
        }
        private void ClearBuffers()
        {
            lock (sendLock)
            {
                // Clear send buffers
                sendBufferMain.Clear();
                sendBufferFlush.Clear();
                sendBufferFlushOffset = 0;
            }
        }

        public bool SendAsync(string payload)
        {
            try
            {
                SendAsync(Encoding.UTF8.GetBytes(payload + MessageEndMark));
            }
            catch (System.Exception)
            {
                return false;
            }

            return true;
        }
        private bool SendAsync(byte[] buffer, long offset, long size)
        {
            if (!IsConnected)
                return false;

            if (size == 0)
                return true;

            lock (sendLock)
            {
                // Detect multiple send handlers
                bool sendRequired = sendBufferMain.IsEmpty || sendBufferFlush.IsEmpty;

                // Fill the main send buffer
                sendBufferMain.Append(buffer, offset, size);

                // Avoid multiple send handlers
                if (!sendRequired)
                    return true;
            }

            // Try to send the main buffer
            Task.Factory.StartNew(StartSend);

            return true;
        }
        private bool SendAsync(byte[] buffer) { return SendAsync(buffer, 0, buffer.Length); }

        #endregion

        #region IO Async Processing

        // This method is called whenever a receive or send operation is completed on a socket
        private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            // Determine which type of operation just completed and call the associated handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    if (ProcessReceive(e))
                        StartReceive();
                    break;
                case SocketAsyncOperation.Send:
                    if (ProcessSend(e))
                        StartSend();
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        // This method is invoked when an asynchronous receive operation completes
        private bool ProcessReceive(SocketAsyncEventArgs e)
        {
            if (!IsConnected)
                return false;

            int size = e.BytesTransferred;

            // Received some data from the client
            if (size > 0)
            {
                // Update statistic
                BytesReceived += size;

                // Call the buffer received handler
                OnReceivedData(receiveBuffer.Data, 0, size);

                // If the receive buffer is full increase its size
                if (receiveBuffer.Capacity == size)
                    receiveBuffer.Reserve(2 * size);
            }

            receiving = false;

            // Try to receive again if the session is valid
            if (e.SocketError == SocketError.Success)
            {
                // If zero is returned from a read operation, the remote end has closed the connection
                if (size > 0)
                    return true;
                else
                    Stop();
            }
            else
            {
                OnError(e.SocketError);
                Stop();
            }

            return false;
        }

        // This method is invoked when an asynchronous send operation completes
        private bool ProcessSend(SocketAsyncEventArgs e)
        {
            if (!IsConnected)
                return false;

            long size = e.BytesTransferred;

            // Send some data to the client
            if (size > 0)
            {
                // Update statistic
                BytesSent += size;

                // Increase the flush buffer offset
                sendBufferFlushOffset += size;

                // Successfully send the whole flush buffer
                if (sendBufferFlushOffset == sendBufferFlush.Size)
                {
                    // Clear the flush buffer
                    sendBufferFlush.Clear();
                    sendBufferFlushOffset = 0;
                }
            }

            sending = false;

            // Try to send again if the session is valid
            if (e.SocketError == SocketError.Success)
                return true;
            else
            {
                OnError(e.SocketError);
                Stop();
                return false;
            }
        }

        #endregion

        #region Session Events

        // Handle connected event
        private void OnConnected()
        {

        }

        // Handle disconnected event
        private void OnDisconnected()
        {
            if (OnSessionDisconnected != null)
                OnSessionDisconnected.Invoke();
        }

        /// Handle error event
        private void OnError(SocketError error)
        {
            // Skip disconnect errors
            if ((error == SocketError.ConnectionAborted) ||
                (error == SocketError.ConnectionRefused) ||
                (error == SocketError.ConnectionReset) ||
                (error == SocketError.OperationAborted) ||
                (error == SocketError.Shutdown))
                return;

        }

        // Handle data received event
        private void OnReceivedData(byte[] buffer, int offset, int size)
        {
            // Decode the raw bytes and appened them to buffer
            dataBuffer += Encoding.UTF8.GetString(buffer, offset, size);

            // Split the string
            var segments = dataBuffer.Split(MessageEndMark);
            var validSegmentsCount = segments.Length - (dataBuffer.EndsWith(MessageEndMark) ? 0 : 1);

            // Extract request from segments and emit them
            for (int i = 0; i < validSegmentsCount; i++)
            {
                if (segments[i].Length > 0)
                {
                    // Notify the server
                    if (OnSessionMessage != null)
                        OnSessionMessage.Invoke(segments[i]);
                }
            }

            // Add last segment to the buffer for further processing if not valid
            dataBuffer = dataBuffer.EndsWith(MessageEndMark) ? string.Empty : segments[segments.Length - 1];

        }


        #endregion

        #region IDisposable

        /// <summary>
        /// Disposed flag
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Session socket disposed flag
        /// </summary>
        public bool IsSocketDisposed { get; private set; } = true;

        // Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposingManagedResources)
        {
            // The idea here is that Dispose(Boolean) knows whether it is
            // being called to do explicit cleanup (the Boolean is true)
            // versus being called due to a garbage collection (the Boolean
            // is false). This distinction is useful because, when being
            // disposed explicitly, the Dispose(Boolean) method can safely
            // execute code using reference type fields that refer to other
            // objects knowing for sure that these other objects have not been
            // finalized or disposed of yet. When the Boolean is false,
            // the Dispose(Boolean) method should not execute code that
            // refer to reference type fields because those objects may
            // have already been finalized."

            if (!IsDisposed)
            {
                if (disposingManagedResources)
                {
                    // Dispose managed resources here...
                    Stop();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...
                OnSessionDisconnected = null;
                OnSessionMessage = null;

                // Mark as disposed.
                IsDisposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~Session()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        #endregion
    }
}
