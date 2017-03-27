using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VIClient
{
    
    public class AsyncRequestObject
    {
        private CancellationTokenSource m_cancellationTokenSource;
        private Task m_task;
        private Exception m_exception;

        public Task task 
        { 
            get 
            {
                return m_task;
            } 
            set
            {
                m_task = value;
            }
        }

        public Exception exception
        {
            get
            {
                return m_exception;
            }
            set
            {
                m_exception = value;
            }
        }

        public CancellationTokenSource CancellationTokenSource
        {
            get
            {
                return m_cancellationTokenSource;
            }
        }

        public CancellationToken CancellationToken
        {
            get 
            {
                return m_cancellationTokenSource.Token;
            }
        }

        public AsyncRequestObject(CancellationTokenSource cts = null)
        {
            m_cancellationTokenSource = cts == null ? new CancellationTokenSource() : cts;
        }

        public void Cancel()
        {
            m_cancellationTokenSource.Cancel();
        }

    }

    public class PeerAddress
    {
        private string m_address;
        private short m_port;
        private IPAddress m_ipAddress;

        public PeerAddress(string address, short port)
        {
            m_address = address;
            m_port = port;
        }

        public string address
        {
            get { return m_address; }
            set
            {
                m_address = value;
            }
        }

        public short port
        {
            get { return m_port; }
            set
            {
                m_port = value;
            }
        }

        public IPAddress ipAddress
        {
            // This could get changed by two threads.
            get { lock (this) { return m_ipAddress; } }
            set
            {
                lock (this) { m_ipAddress = value; }
            }
        }
    }

    class IPServices
    {

        public delegate void OnGetIPAddressComplete(Object result, AsyncRequestObject aro = null);
        public delegate void OnTCPTransactionComplete(Object result, AsyncRequestObject aro = null);
        public delegate void OnTCPRequestComplete(Object result, AsyncRequestObject aro = null);

        public static void TransmitOnSocket(Socket socket, byte[] req, CancellationToken ct)
        {
            try
            {
                // Already cancelled?
                ct.ThrowIfCancellationRequested();

                int bytesSent = 0;

                var ar = socket.BeginSend(req, 0, req.Length, SocketFlags.None, null, null);
                var wh = ar.AsyncWaitHandle;
                try
                {
                    switch (WaitHandle.WaitAny(new WaitHandle[] { ar.AsyncWaitHandle, ct.WaitHandle }))
                    {
                        // Socket event
                        case 0:
                            bytesSent = socket.EndSend(ar);
                            break;
                        // Cancelled externally
                        case 1:
                            socket.Close();
                            ct.ThrowIfCancellationRequested();
                            break;
                    }
                }
                finally
                {
                    wh.Close();
                }
                if (bytesSent != req.Length)
                {
                    throw new Exception(String.Format("Failure sending data to server, bytes requested={0}, bytes sent={1}", req.Length, bytesSent));
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled
                throw;
            }
            catch (Exception ex)
            {
                // Something else happened
                throw;
            }
        }

        public static byte[] ReceiveOnSocket(Socket socket, CancellationToken ct, int waitMS = Timeout.Infinite, int expectedSize = 0)
        {
            try
            {
                // Already cancelled?
                ct.ThrowIfCancellationRequested();

                int bytesReceived = 0;
                const int CHUNK_SIZE = 8192;
                int bufSize = expectedSize == 0 ? CHUNK_SIZE : expectedSize;
                byte[] buf = new byte[bufSize];
                // First time wait longer, then revert to waitMS
                int waitTime = 5000;

                var ar = socket.BeginReceive(buf, bytesReceived, buf.Length - bytesReceived, SocketFlags.None, null, null);
                var wh = ar.AsyncWaitHandle;
                try
                {
                    while (true)
                    {
                        int chunkLength = 0;
                        switch (WaitHandle.WaitAny(new WaitHandle[] { ar.AsyncWaitHandle, ct.WaitHandle }, waitTime))
                        {
                            // Socket event
                            case 0:
                                chunkLength = socket.EndReceive(ar);
                                wh.Close();
                                if (chunkLength > 0)
                                {
                                    bytesReceived += chunkLength;
                                    if (expectedSize != 0 && bytesReceived == expectedSize)
                                    {
                                        // Make it drop out.
                                        chunkLength = 0;
                                    }
                                    else
                                    {
                                        if (bytesReceived == bufSize)
                                        {
                                            Array.Resize(ref buf, bufSize += CHUNK_SIZE);
                                        }
                                        ar = socket.BeginReceive(buf, bytesReceived, buf.Length - bytesReceived, SocketFlags.None, null, null);
                                        wh = ar.AsyncWaitHandle;
                                    }
                                }
                                break;

                            // Cancelled externally
                            case 1:
                                ct.ThrowIfCancellationRequested();
                                break;

                            case WaitHandle.WaitTimeout:
                                // Drop out
                                chunkLength = 0;
                                break;
                        }
                        if (chunkLength <= 0)
                        {
                            Array.Resize(ref buf, bytesReceived);
                            break;
                        }
                        waitTime = waitMS;
                    }
                }
                finally
                {
                    wh.Close();
                }
                return buf;
            }
            catch (OperationCanceledException)
            {
                // Cancelled
                throw;
            }
            catch (Exception ex)
            {
                // Something else happened
                throw;
            }
        }

        public static Socket ConnectTCP(IPAddress ipAddr, short port, CancellationToken ct)
        {
            try
            {
                // Already cancelled?
                ct.ThrowIfCancellationRequested();

                Socket socket = null;
                IPEndPoint ep = new IPEndPoint(ipAddr, port);
                socket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                var ar = socket.BeginConnect(ep, null, null);

                var wh = ar.AsyncWaitHandle;
                try
                {
                    switch (WaitHandle.WaitAny(new WaitHandle[] { ar.AsyncWaitHandle, ct.WaitHandle }))
                    {
                        // Socket event
                        case 0:
                            socket.EndConnect(ar);
                            break;
                        // Cancelled externally
                        case 1:
                            socket.Close();
                            ct.ThrowIfCancellationRequested();
                            break;
                    }
                }
                finally
                {
                    wh.Close();
                }
                return socket;
            }
            catch (OperationCanceledException)
            {
                // Cancelled
                throw;
            }
            catch (Exception ex)
            {
                // Something else happened
                throw;
            }
        }

        public static Task<Socket> ConnectTCPTask(IPAddress ipAddr, short port, CancellationToken ct)
        {
            return Task.Factory.StartNew(() =>
            {
                return ConnectTCP(ipAddr, port, ct);
            });
        }

        public static IPAddress GetIPAddress(string URL, CancellationToken ct)
        {
            try
            {
                // Already cancelled?
                ct.ThrowIfCancellationRequested();

                IPAddress ip = null;

                var ar = Dns.BeginGetHostAddresses(URL, null, null);
                var wh = ar.AsyncWaitHandle;
                try
                {
                    switch (WaitHandle.WaitAny(new WaitHandle[] { ar.AsyncWaitHandle, ct.WaitHandle }))
                    {
                        case 0:
                            IPAddress[] addrs = Dns.EndGetHostAddresses(ar);
                            foreach (IPAddress ipAddr in addrs)
                            {
                                if (ipAddr.AddressFamily == AddressFamily.InterNetwork || ipAddr.AddressFamily == AddressFamily.InterNetworkV6)
                                {
                                    ip = ipAddr;
                                    break;
                                }
                            }
                            break;
                        case 1:
                            ct.ThrowIfCancellationRequested();
                            break;
                    }
                }
                finally
                {
                    wh.Close();
                }
                return ip;
            }
            catch (OperationCanceledException)
            {
                // Cancelled
                throw;
            }
            catch (Exception ex)
            {
                // Something else happened
                throw;
            }

        }

        public static AsyncRequestObject GetIPAddressTask(string hostAddress, OnGetIPAddressComplete GetIPAddressCompleteCallback, CancellationTokenSource cts = null, TaskScheduler ts = null)
        {
            AsyncRequestObject aro = new AsyncRequestObject(cts);
            aro.task = Task.Factory.StartNew(() =>
            {
                return GetIPAddress(hostAddress, aro.CancellationToken);
            }, aro.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default).ContinueWith((antecedent) =>
                {
                    if (!aro.CancellationToken.IsCancellationRequested)
                    {
                        switch (antecedent.Status)
                        {
                            case TaskStatus.Faulted:
                                if(GetIPAddressCompleteCallback!=null)
                                {
                                    GetIPAddressCompleteCallback(antecedent.Exception.InnerException, aro);
                                }
                                break;
                            case TaskStatus.RanToCompletion:
                                if(GetIPAddressCompleteCallback!=null)
                                {
                                    GetIPAddressCompleteCallback(antecedent.Result, aro); 
                                }
                                break;
                        }
                    }
                }, ts == null ? TaskScheduler.FromCurrentSynchronizationContext() : ts);
            return aro;
        }

        public static AsyncRequestObject TCPTransactionTask(PeerAddress host, byte[] pkt, OnTCPTransactionComplete TCPTransactionCompleteCallback, CancellationTokenSource cts = null, TaskScheduler ts = null)
        {
            AsyncRequestObject aro = new AsyncRequestObject(cts);
            aro.task = Task.Factory.StartNew(() =>
            {
                if(host.ipAddress==null)
                {
                    throw new Exception("No IP Address");
                    // Not a good idea
                    // host.ipAddress = GetIPAddress(host.address, aro.CancellationToken);
                }

                Socket socket = ConnectTCP(host.ipAddress, host.port, aro.CancellationToken);

                TransmitOnSocket(socket, pkt, aro.CancellationToken);

                return ReceiveOnSocket(socket, aro.CancellationToken);
            }, aro.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default).ContinueWith((antecedent) => 
                {
                    if (!aro.CancellationToken.IsCancellationRequested)
                    {
                        switch (antecedent.Status)
                        {
                            case TaskStatus.Faulted:
                                aro.exception = antecedent.Exception.InnerException;
                                if (TCPTransactionCompleteCallback != null)
                                {
                                    TCPTransactionCompleteCallback(antecedent.Exception.InnerException, aro);
                                }
                                break;
                            case TaskStatus.RanToCompletion:
                                if (TCPTransactionCompleteCallback != null)
                                {
                                    TCPTransactionCompleteCallback(antecedent.Result, aro);
                                }
                                break;
                        }
                    }
                }, ts == null ? TaskScheduler.FromCurrentSynchronizationContext() : ts);
            return aro;
        }

        public static AsyncRequestObject TCPRequestTask(PeerAddress host, byte[] pkt, OnTCPRequestComplete TCPRequestCompleteCallback, CancellationTokenSource cts = null, TaskScheduler ts = null)
        {
            AsyncRequestObject aro = new AsyncRequestObject(cts);
            aro.task = Task.Factory.StartNew(() =>
            {
                if (host.ipAddress == null)
                {
                    throw new Exception("No IP Address");
                    // Not a good idea
                    // host.ipAddress = GetIPAddress(host.address, aro.CancellationToken);
                }

                Socket socket = ConnectTCP(host.ipAddress, host.port, aro.CancellationToken);

                TransmitOnSocket(socket, pkt, aro.CancellationToken);

                return socket;
            }, aro.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default).ContinueWith((antecedent) =>
            {
                if (!aro.CancellationToken.IsCancellationRequested)
                {
                    switch (antecedent.Status)
                    {
                        case TaskStatus.Faulted:
                            aro.exception = antecedent.Exception.InnerException;
                            if (TCPRequestCompleteCallback != null)
                            {
                                TCPRequestCompleteCallback(antecedent.Exception.InnerException, aro);
                            }
                            break;
                        case TaskStatus.RanToCompletion:
                            if (TCPRequestCompleteCallback != null)
                            {
                                TCPRequestCompleteCallback(antecedent.Result, aro);
                            }
                            break;
                    }
                }
            }, ts == null ? TaskScheduler.FromCurrentSynchronizationContext() : ts);
            return aro;
        }

        public static AsyncRequestObject TCPRequestPulseRequestsTask(PeerAddress host, byte[] pkt1, byte[] pkt2, int msDelay, OnTCPRequestComplete TCPRequestCompleteCallback, CancellationTokenSource cts = null, TaskScheduler ts = null)
        {
            AsyncRequestObject aro = new AsyncRequestObject(cts);
            aro.task = Task.Factory.StartNew(() =>
            {
                if (host.ipAddress == null)
                {
                    throw new Exception("No IP Address");
                    // Not a good idea
                    // host.ipAddress = GetIPAddress(host.address, aro.CancellationToken);
                }

                Socket socket = ConnectTCP(host.ipAddress, host.port, aro.CancellationToken);

                TransmitOnSocket(socket, pkt1, aro.CancellationToken);

            }, aro.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default).ContinueWith((antecedent) =>
                {
                    aro.CancellationToken.WaitHandle.WaitOne(msDelay);
                    aro.CancellationToken.ThrowIfCancellationRequested();
                    Socket socket = ConnectTCP(host.ipAddress, host.port, aro.CancellationToken);

                    TransmitOnSocket(socket, pkt2, aro.CancellationToken);

                }, aro.CancellationToken ).ContinueWith((antecedent) =>
                    {
                        if (!aro.CancellationToken.IsCancellationRequested)
                        {
                            switch (antecedent.Status)
                            {
                                case TaskStatus.Faulted:
                                    aro.exception = antecedent.Exception.InnerException;
                                    if (TCPRequestCompleteCallback != null)
                                    {
                                        TCPRequestCompleteCallback(antecedent.Exception.InnerException, aro);
                                    }
                                    break;
                                case TaskStatus.RanToCompletion:
                                    if (TCPRequestCompleteCallback != null)
                                    {
                                        TCPRequestCompleteCallback(null, aro);
                                    }
                                    break;
                            }
                        }
                    }, ts == null ? TaskScheduler.FromCurrentSynchronizationContext() : ts);
            return aro;
        }
    }


}
