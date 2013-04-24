using System;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Collections;
using ArmA2;

namespace A2TS_Extension_NP
{
    public class A2TS : IExtension
    {
        NamedPipes nPipe = new NamedPipes();

        public string Call(string args)
        {
            try
            {
                // Entry point, as well as the only function called by the plug-in.
                String[] callCommand = args.Trim().Split('@');

                if (callCommand[0].ToUpper().Equals("INIT"))
                {
                    return nPipe.Init();
                }
                else if (callCommand[0].ToUpper().Equals("SEND"))
                {
                    return nPipe.Send(callCommand[1]);
                }
                else if (callCommand[0].ToUpper().Equals("RECV"))
                {
                    return nPipe.Recv();
                }
                else
                    return "hint 'A2TS: Unknown command: " + callCommand[0].ToUpper() + ".'";
            }
            catch (Exception ex)
            {
                return "hint 'A2TS: Call failure. Reason: " + ex.Message + "'";
            }
        }
    }

    public class NamedPipes
    {
        NamedPipeServerStream _pipe;
        Thread _sendThread;
        Thread _recvThread;
        Queue _outgoingMessages;
        Queue _outgoingMessagesSynchronized;
        Queue _incomingMessages;
        Queue _incomingMessagesSynchronized;
        Boolean _criticalException;
        String _exceptionMessage;

        public string Init()
        {
            _pipe = new NamedPipeServerStream("a2ts", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
            _sendThread = new Thread(new ThreadStart(Sender));
            _sendThread.IsBackground = true;
            _sendThread.Start();

            _recvThread = new Thread(new ThreadStart(Receiver));
            _recvThread.IsBackground = true;
            _recvThread.Start();

            _outgoingMessages = new Queue();
            _outgoingMessagesSynchronized = Queue.Synchronized(_outgoingMessages);

            _incomingMessages = new Queue();
            _incomingMessagesSynchronized = Queue.Synchronized(_incomingMessages);
            
            return "hint 'A2TS: Init Complete.'";
        }

        public string Send(string message)
        {
            if (_criticalException)
            {
                return "hint 'A2TS: THREAD FAILURE. PLEASE RESTART ARMA2 IMMEDIATELY. EXCEPTION MESSAGE: " + _exceptionMessage + "'"; 
            }

            if (_pipe.IsConnected)
            {
                _outgoingMessagesSynchronized.Enqueue(message);
                return string.Empty;
            }
            else
            {
                return "hintsilent 'A2TS: DISCONNECTED FROM PIPE'";
            }
        }

        public void Sender()
        {
            while (true)
            {
                try
                {
                    if (!_pipe.IsConnected)
                    {
                        _pipe.WaitForConnection();
                    }

                    if (_outgoingMessagesSynchronized.Count != 0)
                    {
                        object messageToSend = _outgoingMessagesSynchronized.Dequeue();
                        byte[] bytesToWrite = Encoding.ASCII.GetBytes(messageToSend.ToString());
                        _pipe.Write(bytesToWrite, 0, bytesToWrite.Length);
                        _pipe.WaitForPipeDrain();
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (IOException)
                {
                    _pipe.Disconnect();
                }
                catch (Exception ex)
                {
                    _criticalException = true;
                    _exceptionMessage = ex.Message;
                }
            }
        }

        public string Recv()
        {
            if (_criticalException)
            {
                return "hint 'A2TS: THREAD FAILURE. PLEASE RESTART ARMA2 IMMEDIATELY. EXCEPTION MESSAGE: " + _exceptionMessage + "'";
            }

            if (!_pipe.IsConnected || _incomingMessagesSynchronized.Count == 0)
            {
                return String.Empty;
            }
            else
            {
                return _incomingMessagesSynchronized.Dequeue().ToString();
            }
        }

        public void Receiver()
        {
            while (true)
            {
                try
                {
                    byte[] bytesReceived = new byte[512];
                    int byteCount;

                    if (!_pipe.IsConnected)
                    {
                        Thread.Sleep(500);
                    }
                    else
                    {
                        byteCount = _pipe.Read(bytesReceived, 0, 512);
                        if (byteCount != 0)
                        {
                            _incomingMessages.Enqueue(Encoding.ASCII.GetString(bytesReceived, 0, byteCount));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _criticalException = true;
                    _exceptionMessage = ex.ToString();
                }
            }
        }
    }
}
