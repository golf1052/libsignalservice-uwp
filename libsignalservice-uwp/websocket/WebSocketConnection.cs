/** 
* Copyright (C) 2015-2017 smndtrl, golf1052
* 
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.ProtocolBuffers;
using libsignal.util;
using libsignalservice.push;
using libsignalservice.util;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using static libsignalservice.websocket.WebSocketProtos;

namespace libsignalservice.websocket
{
    public class WebSocketConnection //: WebSocketEventListener
    {
        private static readonly int KEEPALIVE_TIMEOUT_SECONDS = 55;

        private readonly LinkedList<WebSocketRequestMessage> incomingRequests = new LinkedList<WebSocketRequestMessage>();
        private readonly Dictionary<long, Tuple<int, string>> outgoingRequests = new Dictionary<long, Tuple<int, string>>();

        private readonly string wsUri;
        private readonly TrustStore trustStore;
        private readonly CredentialsProvider credentialsProvider;
        private readonly string userAgent;

        private Timer keepAliveTimer;

        MessageWebSocket socket;
        DataWriter messageWriter;
        private int attempts;
        private bool connected;

        public event EventHandler Connected;
        public event EventHandler Closed;
        public event TypedEventHandler<WebSocketConnection, WebSocketRequestMessage> MessageReceived;

        public WebSocketConnection(string httpUri, TrustStore trustStore, CredentialsProvider credentialsProvider, string userAgent)
        {
            this.trustStore = trustStore;
            this.credentialsProvider = credentialsProvider;
            this.userAgent = userAgent;
            this.attempts = 0;
            this.connected = false;
            this.wsUri = httpUri.Replace("https://", "wss://")
                .Replace("http://", "ws://") + $"/v1/websocket/?login={credentialsProvider.GetUser()}&password={credentialsProvider.GetPassword()}";
            this.userAgent = userAgent;
        }


        public async Task connect()
        {
            Debug.WriteLine("WSC connect()...");

            if (socket == null)
            {
                socket = new MessageWebSocket();
                if (userAgent != null)
                {
                    socket.SetRequestHeader("X-Signal-Agent", userAgent);
                }
                socket.MessageReceived += OnMessageReceived;
                socket.Closed += OnClosed;

                try
                {
                    Uri server = new Uri(wsUri);
                    await socket.ConnectAsync(server);
                    if (socket != null)
                    {
                        attempts = 0;
                        connected = true;
                    }
                    //Connected(this, EventArgs.Empty);
                    keepAliveTimer = new Timer(sendKeepAlive, null, TimeSpan.FromSeconds(KEEPALIVE_TIMEOUT_SECONDS), TimeSpan.FromSeconds(KEEPALIVE_TIMEOUT_SECONDS));
                    messageWriter = new DataWriter(socket.OutputStream);
                }
                catch (Exception e)
                {
                    /*WebErrorStatus status = WebSocketError.GetStatus(e.GetBaseException().HResult);

                    switch (status)
                    {
                        case WebErrorStatus.CannotConnect:
                        case WebErrorStatus.NotFound:
                        case WebErrorStatus.RequestTimeout:
                            Debug.WriteLine("Cannot connect to the server. Please make sure " +
                                "to run the server setup script before running the sample.");
                            break;

                        case WebErrorStatus.Unknown:
                            throw;

                        default:
                            Debug.WriteLine("Error: " + status);
                            break;
                    }*/
                }

                this.connected = false;
                Debug.WriteLine("WSC connected...");
            }
        }

        public void disconnect()
        {
            Debug.WriteLine("WSC disconnect()...");

            if (socket != null)
            {

                socket.Close(1000, "OK");
                socket = null;
                connected = false;
            }

            /*if (keepAliveSender != null)
            {
                keepAliveSender.shutdown();
                keepAliveSender = null;
            }*/
        }

        /*public  WebSocketRequestMessage readRequest(ulong timeoutMillis)
        {
            if (client == null)
            {
                throw new Exception("Connection closed!");
            }

            ulong startTime = KeyHelper.getTime();

            while (client != null && incomingRequests.Count == 0 && elapsedTime(startTime) < timeoutMillis)
            {
                //Util.wait(this, Math.Max(1, timeoutMillis - elapsedTime(startTime)));
            }

            if (incomingRequests.Count == 0 && client == null) throw new Exception("Connection closed!");
            else if (incomingRequests.Count == 0) throw new TimeoutException("Timeout exceeded");
            else
            {
                WebSocketRequestMessage message = incomingRequests.First();
                incomingRequests.RemoveFirst();
                return message;
            }
        }*/

        public async Task<Tuple<int, string>> sendRequest(WebSocketRequestMessage request)
        {
            if (socket == null || !connected)
            {
                throw new IOException("No connection!");
            }

            WebSocketMessage message = WebSocketMessage.CreateBuilder()
                .SetType(WebSocketMessage.Types.Type.REQUEST)
                .SetRequest(request)
                .Build();

            Tuple<int, string> empty = new Tuple<int, string>(0, string.Empty);
            outgoingRequests.Add((long)request.Id, empty);

            messageWriter.WriteBytes(message.ToByteArray());
            await messageWriter.StoreAsync();
            return empty;
        }

        public async Task sendMessage(WebSocketMessage message)
        {
            if (socket == null)
            {
                throw new Exception("Connection closed!");
            }

            messageWriter.WriteBytes(message.ToByteArray());
            await messageWriter.StoreAsync();
        }

        public async Task sendResponse(WebSocketResponseMessage response)
        {
            if (socket == null)
            {
                throw new Exception("Connection closed!");
            }

            WebSocketMessage message = WebSocketMessage.CreateBuilder()
                                               .SetType(WebSocketMessage.Types.Type.RESPONSE)
                                               .SetResponse(response)
                                               .Build();

            messageWriter.WriteBytes(message.ToByteArray());
            await messageWriter.StoreAsync();
        }

        private void sendKeepAlive(object state)
        {
            if (socket != null)
            {
                Debug.WriteLine("keepAlive");
                byte[] message = WebSocketMessage.CreateBuilder()
                    .SetType(WebSocketMessage.Types.Type.REQUEST)
                    .SetRequest(WebSocketRequestMessage.CreateBuilder()
                        .SetId(KeyHelper.getTime())
                    .SetPath("/v1/keepalive")
                    .SetVerb("GET")
                    .Build()).Build()
                    .ToByteArray();
                messageWriter.WriteBytes(message);
                messageWriter.StoreAsync();
            }
        }

        private ulong elapsedTime(ulong startTime)
        {
            return KeyHelper.getTime() - startTime;
        } 

        /*public void shutdown()
        {
            stop.set(true);
        }
    }*/

        private async void OnClosed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            Debug.WriteLine("WSC disconnected...");
            connected = false;

            for (int i = 0; i < outgoingRequests.Count; i++)
            {
                outgoingRequests.Remove(i);
                i--;
            }

            keepAliveTimer.Dispose();
            keepAliveTimer = null;

            await Task.Delay(Math.Min(++attempts * 200, (int)TimeSpan.FromSeconds(15).TotalMilliseconds));

            if (socket != null)
            {
                socket.Close(1000, "OK");
                socket = null;
                connected = false;
                await connect();
            }
        }

        private void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (DataReader reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

                    byte[] read = new byte[reader.UnconsumedBufferLength];
                    reader.ReadBytes(read);
                    try
                    {
                        WebSocketMessage message = WebSocketMessage.ParseFrom(read);

                        Debug.WriteLine("Message Type: " + message.Type);

                        if (message.Type == WebSocketMessage.Types.Type.REQUEST)
                        {
                            incomingRequests.AddFirst(message.Request);
                            MessageReceived(this, message.Request);
                        }
                        else if (message.Type == WebSocketMessage.Types.Type.RESPONSE)
                        {
                            if (outgoingRequests.ContainsKey((long)message.Response.Id))
                            {
                                outgoingRequests[(long)message.Response.Id] = Tuple.Create((int)message.Response.Status,
                                    Encoding.UTF8.GetString(message.Response.Body.ToByteArray()));
                            }
                        }
                        
                    }
                    catch (InvalidProtocolBufferException e)
                    {
                        Debug.WriteLine(e.Message);
                    }

                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
    }
}
