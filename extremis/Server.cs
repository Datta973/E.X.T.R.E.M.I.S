using System;
using System.Windows;
using System.Net;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics;
//using System.Speech.Synthesis;
using System.Net.Sockets;
using System.IO;

namespace Osion
{
    public delegate void Function(string msg);
    public class Server
    {
        static TcpClient client;
        static TcpListener server;
        static BackgroundWorker serviceThread;
        static BackgroundWorker wakeWordThread;
        static bool serverIsRunning = false;
        public static MainWindow CentralDisplay;
        //public static SpeechSynthesizer JARVIS;
        public static Process process;
        public static StreamWriter clientOut;
        IPAddress ipAddress;
        static int[] ports = new int[]{ 4000, 5000, 6000, 7000 };
        static int port;
        private Function doWork;
        public void Start()
        {

            MessageBox.Show(GetLocalIPAddress());
            //ipAddress = IPAddress.Parse("192.168.0.28");
            ipAddress = IPAddress.Parse(GetLocalIPAddress());
            //ipAddress = Dns.GetHostEntry("localhost").AddressList[0];

            try
            {
                port = 5000;
                server = new TcpListener(ipAddress, port);
                //server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

                server.Start();
            }catch(Exception ex)
            {
                port = 6000;
                server = new TcpListener(ipAddress, port);
                //server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

                server.Start();
            }



            serviceThread = new BackgroundWorker();
            serviceThread.WorkerSupportsCancellation = true;
            serviceThread.DoWork += new DoWorkEventHandler(ServiceHandler);
            serviceThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ServiceCompleted);
            serviceThread.RunWorkerAsync();


            //JARVIS = new SpeechSynthesizer();

            wakeWordThread = new BackgroundWorker();


            wakeWordThread.WorkerSupportsCancellation = true;
            wakeWordThread.DoWork += new DoWorkEventHandler(WakeWordHandler);
            wakeWordThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(WakeWordHandlingCompleted);
            wakeWordThread.RunWorkerAsync();



        }

        public void onMessage(Function method) {
            doWork = method;
        }
        public void Close() {
            //server.Server.Close();
            //Thread.Sleep(3000);
            Console.WriteLine("Server Stopped...");
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        public static void SendClientMessage(string msg)
        {
            if (serviceThread.IsBusy && client != null)
                clientOut.WriteLine(msg);
            else
            {
                Console.WriteLine("Client is not connected");
            }
        }

        public static void ReConnect()
        {
            if (serviceThread.IsBusy)
            {
                Console.WriteLine("Its busy");
            }
            else
            {
                serviceThread.RunWorkerAsync();
            }
        }

        private void ServiceHandler(object sender, DoWorkEventArgs e)
        {


            StreamReader clientIn;

            string message;
            Console.WriteLine("Listening at : {0}:{1}", ipAddress,port);


            client = server.AcceptTcpClient();
            clientIn = new StreamReader(client.GetStream());
            clientOut = new StreamWriter(client.GetStream());

            if (client.Connected)
            {
                ClientConnected();
            }

            clientOut.AutoFlush = true;

            while (!serviceThread.CancellationPending && client.Connected)
            {

                try
                {
                    while ((message = clientIn.ReadLine()) != null)
                    {
                        Console.WriteLine("Client : " + message);
                        doWork(message);
                        //JARVIS.Speak("Input received !!!");
                    }
                }
                catch (IOException ex)
                {
                    //Console.WriteLine("Client got disconnected");
                }
            }
            clientIn.Close();
            clientOut.Close();
            client.Close();
            client = null;
        }

        private void ClientConnected()
        {
            Console.WriteLine("Client is connected");
        }

        private void ServiceCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Client Service Finished");
        }

        private void WakeWordHandler(object sender, DoWorkEventArgs e)
        {
            using (process = new Process())
            {
                process.StartInfo.FileName = @"python.exe";
                process.StartInfo.Arguments = "D:\\Projects\\Educational\\College\\Mini_Project\\Porcupine\\demo\\python\\porcupine_demo.py --keyword_file_paths D:\\Projects\\Educational\\College\\Mini_Project\\Porcupine\\extremis_windows.ppn";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                while (!process.StandardOutput.EndOfStream)
                {
                    string line = process.StandardOutput.ReadLine();
                    Console.WriteLine(line);
                    if (line == "detected")
                    {
                        ReactAndRespond();
                    }
                }
                //process.WaitForExit();
            }
        }

        private void ReactAndRespond()
        {
            SendClientMessage("GETSPEECH");
            //JARVIS.Speak("Hello Sir !!!");
        }

        private void WakeWordHandlingCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            process.Close();
        }

        private static void TerminateServer(object sender, CancelEventArgs e)
        {
            wakeWordThread.CancelAsync();
            serviceThread.CancelAsync();
            Console.WriteLine("Closing...");
            /*if (serverIsRunning) {
                e.Cancel = true;
                serverIsRunning = false;
                CentralDisplay.setStatus("Closing the server...");
            }*/
        }
    }
}
