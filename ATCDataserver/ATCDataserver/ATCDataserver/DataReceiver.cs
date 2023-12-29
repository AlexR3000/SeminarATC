using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ATCDataserver
{
    public class DataReceiver
    {
        private string _host;
        private int _port;

        public ConcurrentQueue<string> ReceivedMessageQueue { get; set; } = new ConcurrentQueue<string>();

        public DataReceiver(string host, int port)
        {
            _host = host;
            _port = port;
        }


        public void StreamReceive()
        {
            var tcpClient = new TcpClient();
            try
            {
                tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                tcpClient.ConnectAsync(_host, _port).Wait();
                var stream = tcpClient.GetStream();

                // Receive data from the server
                var buffer = new byte[4096];

                var leftOverBuffer = "";
                var count = 0;

                var restartTimer = DateTime.UtcNow;
                var resetTimer = true;
                while (true)
                {
                    
                    if (tcpClient.Available > 0)
                    {
                        EnqueueReceivedMessages(stream, buffer, ref leftOverBuffer, ref count);
                        resetTimer = true;
                    }
                    else // in case that seemingly the base station stops sending messages, restart connection
                    {
                        // reset timer after every time readable bytes have been available in the tcpClient 
                        if (resetTimer)
                        {
                            restartTimer = DateTime.UtcNow;
                            resetTimer = false;
                        }
                        
                        if ((DateTime.UtcNow - restartTimer).TotalSeconds > 5)
                        {
                            tcpClient.Close();
                            tcpClient = new TcpClient();
                            tcpClient.ConnectAsync(_host, _port).Wait();

                            stream = tcpClient.GetStream();
                            restartTimer = DateTime.UtcNow;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
            finally
            {
                tcpClient?.Close();
            }
      
        }

        public void EnqueueReceivedMessages(NetworkStream stream, byte[] buffer, ref string leftOverBuffer, ref int count)
        {


            int bytesRead = stream.ReadAsync(buffer, 0, buffer.Length).Result;

            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            leftOverBuffer += receivedMessage;

            var splitMessage = leftOverBuffer.Split(new[] { '\n' });


            // last element of split is always incomplete
            leftOverBuffer = splitMessage.Last();


            for (int i = 0; i < splitMessage.Length - 1; i++)
            {
                Console.WriteLine($"{count++} Received from server: {splitMessage[i]}");
                ReceivedMessageQueue.Enqueue(splitMessage[i]);
            }
            
        }

    }
}
