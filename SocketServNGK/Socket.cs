using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Mime;
using System.Linq;

namespace SocketServNGK
{
    public class connState_t {
        public Socket workSocket = null;
        public const int bufferSize = 1 << 20;
        public byte[] recBuffer = new byte[bufferSize];
        public StringBuilder sb = new StringBuilder();
    }

    public class AsyncSocket
    {
        public ManualResetEvent CS_OPEN = new ManualResetEvent(false);
        private ApiController controller;
        private List<connState_t> subscriptionList;
        private List<Socket> approvedList;

        public AsyncSocket()
        {
            controller = new ApiController("1234", "https://fruitflywebapi.azurewebsites.net/api/Heatmap");
            subscriptionList = new List<connState_t>();
            approvedList = new List<Socket>();
        }

        public void ListenBegin()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            Socket listen = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Now bind the socket
            try
            {
                listen.Bind(localEndPoint);
                listen.Listen(100);

                // This is our main loop where we accept new connections
                while(true)
                {
                    CS_OPEN.Reset();

                    listen.BeginAccept(new AsyncCallback(Accept), listen);

                    CS_OPEN.WaitOne();
                }

            } catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Accept(IAsyncResult ar)
        {
            CS_OPEN.Set();

            Socket listen = (Socket)ar.AsyncState;
            Socket handler = listen.EndAccept(ar);

            connState_t state = new connState_t();
            state.workSocket = handler;
            handler.BeginReceive(state.recBuffer, 0, connState_t.bufferSize, 0, new AsyncCallback(Receive), state);
        }

        public void Receive(IAsyncResult ar)
        {
            String response = String.Empty;

            connState_t state = (connState_t)ar.AsyncState;
            Socket handler = state.workSocket;

            if (!handler.Connected) return;

            int ec = handler.EndReceive(ar);
            if(ec > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(
                state.recBuffer, 0, ec));

                response = state.sb.ToString();
                if(response.IndexOf("<EOF>") > -1) // If we at the end of the message
                {
                    var subStrings = response.Split(" ");
                    switch(subStrings[0])
                    {
                        case "AUTH":
                            {
                                if (subStrings.Length < 4)
                                {
                                    SendResponse(state, String.Format($"ERROR: Expected credentials."));
                                }
                                else
                                {
                                    // @TODO:
                                    // Our ApiController needs to implement a call to the web api, that returns a JWT.
                                    // This JWT we then send back to the user, that uses it for getting AUTH.
                                    string JWT = controller.GetJWT(subStrings[1], subStrings[2]).Result;

                                    if(JWT != String.Empty && controller.GetPermission(JWT).Result)
                                    {
                                        // We got approved. Notify the client and add their socket to the list.
                                        approvedList.Add(handler);
                                        SendResponse(state, String.Format("SUCCESS: You are authorized to send data."));
                                    } else
                                    {
                                        SendResponse(state, String.Format("ERROR: (1) You are not authorized. (2) Your credentials are wrong."));
                                    }
                                }
                            } break;
                        case "CRED":
                            {
                                // @TODO
                                // We want the client to use this call, if they want to get added to the web api database.
                                SendResponse(state, String.Format("Not implemented yet."));
                            } break;
                        case "SUB":
                            {
                                subscriptionList.Add(state);
                                SendResponse(state, String.Format($"You are subscribed to live weather updates."));
                            } break;
                        case "SEND":
                            {
                                if (subStrings[1] != String.Empty && approvedList.IndexOf(handler) > -1)
                                {
                                    // We assume subStrings[1] is a json string
                                    controller.PostUpdate(subStrings[1]).Wait();
                                } else
                                {
                                    // ERROR: The client didn't pass the checks, alert them.
                                    SendResponse(state, String.Format($"ERROR: (1) You are not authorized. (2) You didn't submit data."));
                                    break;
                                }

                                foreach (var cli in subscriptionList)
                                {
                                    // Only try to send if we think the client is connected.
                                    // Second check is because we don't want to alert the sender of their own update.
                                    if (cli.workSocket != state.workSocket)
                                    {
                                        SendResponse(cli, subStrings[1]);
                                    }
                                      
                                }

                                SendResponse(state, String.Format($"The update was submitted to the server."));
                            } break;
                        case "QUIT":
                            {
                                SendResponse(state, String.Format($"Connection closed by server."));
                                handler.Shutdown(SocketShutdown.Both); // Right now we do not allow send / receive after a failed message from a client.
                                handler.Close();
                            } break;
                        case "TEST":
                            {
                                SendResponse(state, String.Format($"This is a test string from the server."));
                            } break;
                        default:
                            {
                                SendResponse(state, String.Format($"ERROR: {subStrings[0]} is not a valid command."));
                            } break;
                    }
                } else {
                    // Receive the rest of the message
                    handler.BeginReceive(state.recBuffer, 0, connState_t.bufferSize, 0, new AsyncCallback(Receive), state);
                }
            }
        }

        private void SendResponse(connState_t state, String data)
        {
            byte[] bytesToSend = Encoding.ASCII.GetBytes(data);
            state.workSocket.BeginSend(bytesToSend, 0, bytesToSend.Length, 0, new AsyncCallback(Send), state);
        }

        private void Send(IAsyncResult ar)
        {
            try
            {
                connState_t state = (connState_t)ar.AsyncState;
                Array.Clear(state.recBuffer, 0, connState_t.bufferSize);
                state.sb.Clear();

                Socket handler = state.workSocket;
                int bytesSent = handler.EndSend(ar);

                handler.BeginReceive(state.recBuffer, 0, connState_t.bufferSize, 0, new AsyncCallback(Receive), state);
                //handler.Shutdown(SocketShutdown.Both); // Right now we do not allow send / receive after a failed message from a client.
                //handler.Close();
            } catch (Exception e)
            {
                // Failed to send
                Console.WriteLine(e.ToString());
            }
        }

    }
}
