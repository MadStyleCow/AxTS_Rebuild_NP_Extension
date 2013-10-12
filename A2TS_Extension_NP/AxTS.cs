using ArmA2;
using System;
using System.Collections;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AxTS_Extension_NP
{
    public class AxTS : IExtension
    {
        AxTS_NamedPipeClient pipeClient = new AxTS_NamedPipeClient();

        public string Call(string args)
        {
            // Main function called by the game.
            try
            {
                String[] arguments = args.Trim().Split('@');
                switch (arguments[0].ToUpper())
                {
                    case "INIT":
                        return pipeClient.Init(int.Parse(arguments[1]));
                        break;
                    case "SEND":
                        return pipeClient.Send(arguments[1]);
                        break;
                    case "RECV":
                        return pipeClient.Recv();
                        break;
                    default:
                        return String.Format("hint 'AxTS: Unknown command - {0}'", arguments[0]);
                }
            }
            catch(Exception ex)
            {
                return String.Format("hint '{0}'", ex.ToString());
            }
        }
    }

    class AxTS_NamedPipeClient
    {
        NamedPipeServerStream serverPipe;
        Queue outgoingMessages;
        Queue incomingMessages;
        Thread outgoingThread;
        Thread incomingThread;
        Boolean exception;
        Int32 channel;
        Boolean InitComplete;

        Regex getCommandRegexp = new Regex(@"\W{1}AxTS_CMD\W{1}(\w{3})\W{2}AxTS_CMD\W{1}");

        public string Init(int channelIdentifier)
        {
            channel = channelIdentifier;

            if (!InitComplete)
            {
                serverPipe = new NamedPipeServerStream("AXTS", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                outgoingMessages = Queue.Synchronized(new Queue());
                incomingMessages = Queue.Synchronized(new Queue());

                outgoingThread = new Thread(new ThreadStart(Sender));
                outgoingThread.IsBackground = true;

                incomingThread = new Thread(new ThreadStart(Receiver));
                incomingThread.IsBackground = true;
      
                outgoingThread.Start();
                incomingThread.Start();

                InitComplete = true;
                exception = false;
            }
            else
            {
                outgoingMessages.Clear();
                incomingMessages.Clear();
            }
            return String.Format("hint 'AxTS: Init success'");
        }

        public string Send(string commandText)
        {
            if (!exception)
            {
                if(!Parser(commandText) && serverPipe.IsConnected)
                {
                    outgoingMessages.Enqueue(commandText);
                }
                return String.Empty;
            }
            else
            {
                return String.Format("hint 'AxTS: Critical Exception '");
            }
        }

        public string Recv()
        {
            if (!exception)
            {
                if (serverPipe.IsConnected && incomingMessages.Count != 0)
                    return incomingMessages.Dequeue().ToString();
                else
                    return string.Empty;
            }
            else
            {
                return String.Format("hint 'AxTS: Critical Exception'");
            }
        }

        /*  Threads */
        void Sender()
        {
            while (true)
            {
                try
                {
                    if(serverPipe.IsConnected)
                    {
                        if (outgoingMessages.Count != 0)
                        {
                            byte[] bytesToWrite = Encoding.ASCII.GetBytes(outgoingMessages.Dequeue().ToString());
                            serverPipe.Write(bytesToWrite, 0, bytesToWrite.Length);
                            serverPipe.WaitForPipeDrain();
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                    else
                    {
                        serverPipe.WaitForConnection();
                    }
                }
                catch(IOException)
                {
                    serverPipe.Disconnect();
                }
                catch(Exception)
                {
                    exception = true;
                }
            }
        }

        void Receiver()
        {
            while(true)
            {
                try
                {
                    if(serverPipe.IsConnected)
                    {
                        byte[] bytesReceived = new byte[512];
                        int byteCount = serverPipe.Read(bytesReceived, 0, 512);

                        if(byteCount != 0)
                        {
                            string receivedMessage = Encoding.ASCII.GetString(bytesReceived, 0, byteCount);

                            if (!Parser(receivedMessage))
                            {
                                incomingMessages.Enqueue(receivedMessage);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
                catch(Exception)
                {
                    exception = true;
                }
            }
        }

        /*  Logic   */
        bool Parser(string command)
        {
            // Should return false if the message is not meant for this extension.
            Match commandCode = getCommandRegexp.Match(command);

            if (commandCode.Success)
            {
                switch (commandCode.Groups[1].Value)
                {
                    case "VER":
                        outgoingMessages.Enqueue(GenerateVER());
                        return true;
                        break;
                    default:
                        return false;
                }
            }
            else
            {
                return false;
            }
        }

        string GenerateVER()
        {
            return String.Format("[AxTS_CMD]VER[/AxTS_CMD][AxTS_ARG]{0};[/AxTS_ARG]", channel);
        }
    }
}
