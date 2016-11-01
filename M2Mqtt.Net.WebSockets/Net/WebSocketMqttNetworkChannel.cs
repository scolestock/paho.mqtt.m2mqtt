/*
All rights reserved. This program and the accompanying materials
are made available under the terms of the Eclipse Public License v1.0
and Eclipse Distribution License v1.0 which accompany this distribution. 

The Eclipse Public License is available at 
   http://www.eclipse.org/legal/epl-v10.html
and the Eclipse Distribution License is available at 
   http://www.eclipse.org/org/documents/edl-v10.php.
*/

using System;
using System.IO;
using System.Threading;
using uPLibrary.Networking.M2Mqtt.Utility;
using WebSocket4Net;

namespace uPLibrary.Networking.M2Mqtt
{
    /// <summary>
    /// Channel to communicate over the network via WebSocket using WebSocket4Net library.
    /// </summary>
    /// <example>
    /// 
    /// var channel = new WebSocketMqttNetworkChannel("127.0.0.1", 80, false);
    ///
    /// this.MqttClient = new MqttClient("127.0.0.1", 80, false, 
    ///                MqttSslProtocols.None, ValidateServerCertificate,
    ///                SelectLocalCertificate, channel);
    ///
    /// this.MqttClient.Connect(clientId, userName, password, true, 60);
    /// 
    /// </example>
    public class WebSocketMqttNetworkChannel : IMqttNetworkChannel
    {
        private WebSocket _webSocket;
        private MemoryStream _stream;
        private string _serverUri;
        private string _subProtocol = "mqttv3.1";
        private int _connectTimeoutMilliseconds = 10000;
        private int _receiveTimeoutMilliseconds = 30000;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketMqttNetworkChannel" /> class.
        /// </summary>
        /// <param name="remoteHostName">Name of the remote host.</param>
        /// <param name="remotePort">The remote port.</param>
        /// <param name="secure">if set to <c>true</c> secure.</param>
        /// <param name="connectTimeoutMilliseconds">The connect timeout in milliseconds.</param>
        /// <param name="receiveTimeoutMilliseconds">The receive timeout in milliseconds.</param>
        public WebSocketMqttNetworkChannel(string remoteHostName, int remotePort, bool secure, int connectTimeoutMilliseconds = 10000, int receiveTimeoutMilliseconds = 30000)
        {
            _serverUri = string.Format("{0}://{1}:{2}/mqtt", (secure ? "wss" : "ws"), remoteHostName, remotePort);
            _connectTimeoutMilliseconds = connectTimeoutMilliseconds;
            _receiveTimeoutMilliseconds = receiveTimeoutMilliseconds;
        }

        /// <summary>
        /// Data available on channel
        /// </summary>
        public bool DataAvailable
        {
            get
            {
                return _stream != null && (_stream.Position < _stream.Length);
            }
        }

        /// <summary>
        /// Receive data from the network channel
        /// </summary>
        /// <param name="buffer">Data buffer for receiving data</param>
        /// <returns>
        /// Number of bytes received
        /// </returns>
        public int Receive(byte[] buffer)
        {
            return Receive(buffer, _receiveTimeoutMilliseconds);
        }

        /// <summary>
        /// Receive data from the network channel with a specified timeout
        /// </summary>
        /// <param name="buffer">Data buffer for receiving data</param>
        /// <param name="timeout">Timeout on receiving (in milliseconds)</param>
        /// <returns>
        /// Number of bytes received
        /// </returns>
        public int Receive(byte[] buffer, int timeout)
        {
            if (_webSocket == null)
            {
                throw new Exception("WebSocket not open.");
            }

            int elapsed = 0;
            while (elapsed < timeout && _webSocket.State == WebSocketState.Open)
            {
                if (DataAvailable)
                {
                    var bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    Trace.WriteLine(TraceLevel.Verbose, "Reading {0} bytes", bytesRead);
                    return bytesRead;
                }

                Thread.Sleep(10);
                elapsed += 10;
            }

            return 0;
        }

        /// <summary>
        /// Send data on the network channel to the broker
        /// </summary>
        /// <param name="buffer">Data buffer to send</param>
        /// <returns>
        /// Number of byte sent
        /// </returns>
        public int Send(byte[] buffer)
        {
            if (_webSocket != null)
            {
                _webSocket.Send(buffer, 0, buffer.Length);
                return buffer.Length;
            }

            return 0;
        }

        /// <summary>
        /// Close the network channel
        /// </summary>
        public void Close()
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                _webSocket.Close();
            }
        }

        /// <summary>
        /// Connect to remote server
        /// </summary>
        public void Connect()
        {
            _webSocket = new WebSocket(_serverUri, _subProtocol);

            _webSocket.Opened += new EventHandler(OnOpened);
            _webSocket.DataReceived += new EventHandler<DataReceivedEventArgs>(OnDataReceived);
            _webSocket.MessageReceived += new EventHandler<MessageReceivedEventArgs>(OnMessageReceived);
            _webSocket.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(OnError);
            _webSocket.Closed += new EventHandler(OnClosed);

            _webSocket.Open();

            // Wait for connect before proceeding.
            var timeout = _connectTimeoutMilliseconds;
            int elapsed = 0;
            while (elapsed < timeout && _webSocket.State != WebSocketState.Open)
            {
                Thread.Sleep(10);
                elapsed += 10;
            }
        }

        /// <summary>
        /// Accept client connection
        /// </summary>
        public void Accept()
        {
        }

        /// <summary>
        /// Called when Opened event occurs.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        private void OnOpened(object sender, EventArgs e)
        {
            Trace.WriteLine(TraceLevel.Verbose, "WebSocket Open");
        }

        /// <summary>
        /// Called when MessageReceived event occurs.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MessageReceivedEventArgs" /> instance containing the event data.</param>
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Trace.WriteLine(TraceLevel.Verbose, "WebSocket MessageReceived {0}", e.Message);
        }

        /// <summary>
        /// Called when DataReceived event occurs.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DataReceivedEventArgs" /> instance containing the event data.</param>
        private void OnDataReceived(object sender, DataReceivedEventArgs e)
        {
            Trace.WriteLine(TraceLevel.Verbose, "WebSocket DataReceived {0} bytes", e.Data.Length);
            _stream = new MemoryStream(e.Data);
        }

        /// <summary>
        /// Called when Error event occurs.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="WebSocketSharp.ErrorEventArgs" /> instance containing the event data.</param>
        private void OnError(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            Trace.WriteLine(TraceLevel.Verbose, "WebSocket Error {0}", e.Exception.Message);
        }

        /// <summary>
        /// Called when Closed event occurs.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        private void OnClosed(object sender, EventArgs e)
        {
            Trace.WriteLine(TraceLevel.Verbose, "WebSocket Close");
        }
    }
}
