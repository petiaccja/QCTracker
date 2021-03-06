﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace VehicleTelemetry {
    /// <summary>
    /// Establish connection to stream messages over TCP.
    /// </summary>
    public class MessageProviderTcp : IMessageProvider {
        private Socket socket;
        private TcpListener listener;
        private object lockObject = new object();
        private volatile bool isMessaging;
        private byte[] sizeBuffer = new byte[2];
        private byte[] checksumBuffer = new byte[4];
        private byte[] dataBuffer = null;
        private bool isListening;


        /// <summary>
        /// Listens on default port.
        /// </summary>
        public MessageProviderTcp() : this(5640) { }

        /// <summary>
        /// Specify the listening port.
        /// </summary>
        /// <param name="port">Which port to use for incoming connections.</param>
        public MessageProviderTcp(ushort port) {
            listener = TcpListener.Create(port);
            socket = null;
            isListening = false;
        }


        /// <summary>
        /// Free internal resources. Cannot be reused afterwards.
        /// </summary>
        public void Dispose() {
            Disconnect();
        }


        protected delegate void ReadMessagePhase();

        /// <summary>
        /// Which port to use for incoming connections.
        /// </summary>
        public ushort ListenPort {
            get {
                return (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
            }
            set {
                ((IPEndPoint)listener.LocalEndpoint).Port = value;
            }
        }

        /// <summary>
        /// Check if connection is alive.
        /// </summary>
        public bool IsConnected {
            get {
                return socket != null && socket.Connected;
            }
        }

        /// <summary>
        /// Check if listening is in progress.
        /// </summary>
        public bool IsListening {
            get {
                return isListening;
            }
        }

        /// <summary>
        /// Listen for incoming connections. DEPRECATED!
        /// </summary>
        public void Listen() {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Fired when a new message is received.
        /// </summary>
        public event MessageHandler OnMessage;

        /// <summary>
        /// Listen for incoming connections asynchronously.
        /// </summary>
        /// <param name="callback">Called if a connection is established or on failure.</param>
        /// <exception cref="InvalidOperationException">Already connected or listening.</exception>
        /// <exception cref="SocketException">There was an error with the underlying TCP socket.</exception>
        public void ListenAsync(ListenCallback callback) {
            if (IsConnected || isListening) {
                throw new InvalidOperationException("Already connected.");
            }

            try {
                isListening = true;
                listener.Start();
                listener.BeginAcceptSocket(ListenAsyncCallback, callback);
            }
            catch (Exception ex) {
                callback(false);
                listener.Stop();
                throw;
            }
        }

        /// <summary>
        /// Stop listening to incoming connections.
        /// </summary>
        public void Cancel() {
            try {
                listener.Stop();
            }
            catch (SocketException ex) {
                // swallow exception
            }
        }


        /// <summary>
        /// Disconnect from remote peer.
        /// </summary>
        public void Disconnect() {
            isMessaging = false;
            if (socket != null) {
                socket.Dispose();
                socket = null;
            }
        }

        /// <summary>
        /// Create a control that allows to configure this provider.
        /// </summary>
        public System.Windows.Forms.UserControl GetConfigurator() {
            return new MessageProviderTcp_Config(this);
        }

        /// <summary>
        /// Handler for C#'s asnync socket.
        /// </summary>
        private void ListenAsyncCallback(IAsyncResult result) {
            bool isOk = false;
            try {
                lock (lockObject) {
                    socket = listener.EndAcceptSocket(result);
                }
                isOk = true;

                // start messaging
                isMessaging = true;
                ReadMessageSize();
            }
            catch (Exception ex) {
                isOk = false;
            }
            finally {
                ListenCallback callback = (ListenCallback)result.AsyncState;

                listener.Stop();
                isListening = false;
                callback(isOk);
            }
        }


        private void ReadMessageSize() {
            try {
                lock (lockObject) {
                    socket.BeginReceive(sizeBuffer, 0, 2, SocketFlags.None, ReadCallback, new AsyncStateObject(ReadMessage, 2));
                }
            }
            catch (Exception ex) {
                isMessaging = false;
            }
        }

        private void ReadMessage() {
            // decode size
            int size = (sizeBuffer[0] << 8) + sizeBuffer[1];
            dataBuffer = new byte[size];

            try {
                lock (lockObject) {
                    socket.BeginReceive(dataBuffer, 0, size, SocketFlags.None, ReadCallback, new AsyncStateObject(ReadCheckSum, size));
                }
            }
            catch (Exception ex) {
                isMessaging = false;
            }
        }

        private void ReadCheckSum() {
            try {
                lock (lockObject) {
                    socket.BeginReceive(checksumBuffer, 0, 4, SocketFlags.None, ReadCallback, new AsyncStateObject(CommitMessage, 4));
                }
            }
            catch (Exception ex) {
                isMessaging = false;
            }
        }

        private void CommitMessage() {
            // decode values
            uint checksum = ((uint)checksumBuffer[0] << 24) + ((uint)checksumBuffer[1] << 16) + ((uint)checksumBuffer[2] << 8) + (uint)checksumBuffer[3];

            // verify checksum
            bool isChecksumOk;
            if (checksum == 0) {
                isChecksumOk = true;
            }
            else {
                // TODO: compute actual cheksum, including sizeBuffer
                isChecksumOk = true;
            }

            if (!isChecksumOk) {
                // kill connection
                isMessaging = false;
            }

            // decode message
            Message msg = Message.Deserialize(dataBuffer);
            if (msg == null) {
                // invalid message, kill connection
                isMessaging = false;
            }

            // fire event
            if (OnMessage != null) {
                OnMessage(msg);
            }

            // get next message
            if (isMessaging) {
                ReadMessageSize();
            }
        }

        private void ReadCallback(IAsyncResult result) {
            try {
                int read = 0;
                lock (lockObject) {
                    read = socket.EndReceive(result);
                }
                AsyncStateObject state = (AsyncStateObject)result.AsyncState;
                if (read != state.numBytesToRead) {
                    isMessaging = false;
                }
                else if (isMessaging) {
                    state.next();
                }
            }
            catch (Exception e) {
                isMessaging = false;
            }
        }

        protected class AsyncStateObject {
            public AsyncStateObject(ReadMessagePhase next, int numBytesToRead) {
                this.next = next;
                this.numBytesToRead = numBytesToRead;
            }
            public ReadMessagePhase next;
            public int numBytesToRead;
        }
    }
}
