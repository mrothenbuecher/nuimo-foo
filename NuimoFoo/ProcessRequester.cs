using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuimoFoo
{
    class ProcessRequester
    {
        private Stream streamIn, streamOut;

        public ProcessRequester()
        {
            init();
        }

        public async void init()
        {
            try
            {
                Windows.Networking.Sockets.StreamSocket socket = new Windows.Networking.Sockets.StreamSocket();
                Windows.Networking.HostName serverHost = new Windows.Networking.HostName("localhost");
                string serverPort = "1337";
                await socket.ConnectAsync(serverHost, serverPort);
                streamIn = socket.InputStream.AsStreamForRead();
                streamOut = socket.OutputStream.AsStreamForWrite();
                Debug.WriteLine("Connected successfully");
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to connect: "+e.Message);
            }
        }

        public string GetProcesses()
        {
            string response = "";
            try
            {

                StreamWriter writer = new StreamWriter(streamOut);
                string request = "test";
                writer.WriteLine(request);
                writer.Flush();


                StreamReader reader = new StreamReader(streamIn);
                response = reader.ReadLine();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to response: " + e.Message);
            }
            return response;
        }

    }
}
