using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ATCDataserver
{
    public class DataReceiver
    {
        private string _host;
        private int _port;

        public Queue<string> ReceivedMessageQueue { get; set; } = new Queue<string>();

        public DataReceiver(string host, int port)
        {
            _host = host;
            _port = port;
        }


        public async void StreamReceiveAsync()
        {
            using (TcpClient tcpClient = new TcpClient())
            {
                try
                {
                    await tcpClient.ConnectAsync(_host, _port);

                    var stream = tcpClient.GetStream();

                    // Receive data from the server
                    byte[] buffer = new byte[4096];

                    string leftOverBuffer = "";

                    while (true)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        
                        leftOverBuffer += receivedMessage;

                        var splitMessage = leftOverBuffer.Split(new[] { '\n' });

                        
                        // last element of split is always incomplete
                        leftOverBuffer = splitMessage.Last();


                        for (int i = 0; i < splitMessage.Length - 1; i++)
                        {
                            Console.WriteLine($"Received from server: {splitMessage[i]}");
                            ReceivedMessageQueue.Enqueue(splitMessage[i]);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

    }
}
