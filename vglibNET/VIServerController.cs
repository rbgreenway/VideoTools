using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VIClient;

namespace VISDK
{
    public class VIServerController
    {       
        private OnNewLiveFrame  m_onNewLiveFrame = null;
        private VIServerDescriptor m_serverCommand = new VIServerDescriptor();
        private VIServerDescriptor m_serverData = new VIServerDescriptor();
        private string m_serverVersion = null;
        private Videoinsight.LIB.Server m_serverClass = null;
        private IntPtr m_cuda = IntPtr.Zero;

        public event ConnectionReadyHandler Connected;
        public EventArgs e = null;
        public delegate void ConnectionReadyHandler(VIServerController serverController, EventArgs e);


        public VIServerController(IntPtr cuda)
        {
            m_cuda = cuda;
        }

        public void Shutdown()
        {
            // TODO:  need to shutdown all of the decoders and threads that are handling the image streams           
        }
        
        public void SetNewLiveFrameCallback(OnNewLiveFrame callbackFunction)
        {
            m_onNewLiveFrame = callbackFunction;
        }
     

        public void ConnectToServer(string serverIP, short serverDataPort, string username, string password)
        {
            m_serverCommand.user = username; // "Admin";
            m_serverCommand.password = password; // "";
            m_serverCommand.port = (short)(serverDataPort + 1); // 4021;

            m_serverData.user = m_serverCommand.user;
            m_serverData.password = m_serverCommand.password;
            m_serverData.port = serverDataPort; // 4020;

            IPServices.GetIPAddressTask(serverIP, OnGetDNSComplete);
            //IPServices.GetIPAddressTask("demovi.com", OnGetDNSComplete);
        }

        public void OnGetDNSComplete(Object result, AsyncRequestObject aro)
        {
            if (result is Exception)
            {
            }
            else
            {
                m_serverCommand.ipAddress = (IPAddress)result;
                m_serverData.ipAddress = (IPAddress)result;
                GetServerVersionAndMetadata();
            }
        }

        public void GetServerVersionAndMetadata()
        {
            IPServices.TCPTransactionTask(m_serverCommand, VIServices.PacketGetServerVersion(),
                                                             OnGetServerVersionAndMetadataComplete);
        }

        public void OnGetServerVersionAndMetadataComplete(Object result, AsyncRequestObject aro)
        {
            if (result is Exception)
            {
            }
            else
            {
                m_serverVersion = VIServices.Deserialize<string>(result);
                GetServerMetadata();
            }
        }

        public void GetServerMetadata()
        {
            IPServices.TCPTransactionTask(m_serverCommand, VIServices.PacketGetServerClass(m_serverCommand),
                                          OnGetServerMetadataComplete);
        }

        public void OnGetServerMetadataComplete(Object result, AsyncRequestObject aro)
        {
            if (result is Exception)
            {
            }
            else
            {
                m_serverClass = VIServices.Deserialize<Videoinsight.LIB.Server>(result);

                // ready to send command telling server which cameras to stream
                if (Connected != null)
                {
                    Connected(this, e);  // raise event signaling that server controller is connected, metadata is ready to ready
                }

            }
        }


        public void GetLive(List<int> cameraIDList)
        {          
            // TODO: need to somehow pass CudaUtil to VIServices.LiveStream
            IPServices.TCPRequestTask(m_serverData, VIServices.PacketStartLiveStream(m_serverData, cameraIDList.ToArray()),
                                                         OnGetLiveConnect);
        }

        public void OnGetLiveConnect(Object result, AsyncRequestObject aro)
        {
            if (result is Exception)
            {
            }
            else
            {
                // Store return value to cancel task
                AsyncRequestObject aroTask =  VIServices.LiveStream((Socket)result, m_onNewLiveFrame,m_cuda);

                // save aroTask as member variable so that I can call aroTask.cancel to stop
            }
        }

        public static AsyncRequestObject LiveStream(Socket socket, OnNewLiveFrame NewLiveFrameCallback, CancellationTokenSource cts = null, TaskScheduler ts = null)
        {
            AsyncRequestObject aro = new AsyncRequestObject(cts);
            ts = ts == null ? TaskScheduler.FromCurrentSynchronizationContext() : ts;

            BlockingCollection<LiveCompressedFrame> queueCompressed = new BlockingCollection<LiveCompressedFrame>(1);

            #region Streaming task


            aro.task = Task.Factory.StartNew(() =>
            {
                byte[] bytes = null;

                HiResTimer t = new HiResTimer();

                Int64 t1 = t.Value;
                Int64 frames = 0;

                while (!aro.CancellationToken.WaitHandle.WaitOne(0))
                {
                    bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 30000, Marshal.SizeOf(typeof(DATA_FRAME_HEADER)));
                    if (bytes != null && bytes.Length != 0)
                    {
                        // Full header received
                        GCHandle pinnedPacket = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        DATA_FRAME_HEADER dfh = (DATA_FRAME_HEADER)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(DATA_FRAME_HEADER));
                        pinnedPacket.Free();

                        // Get frame
                        bytes = IPServices.ReceiveOnSocket(socket, aro.CancellationToken, 5000, dfh.TotalLength - Marshal.SizeOf(typeof(DATA_FRAME_HEADER)));
                        if (bytes != null && bytes.Length != 0)
                        {
                            //queueCompressed.Add(new LiveCompressedFrame(dfh, bytes), aro.CancellationToken);
                            Debug.Print(dfh.CameraID.ToString() + " " + bytes.Length.ToString());

                            // add code here to put frame into decoder input queue
                        }
                        else
                            break;
                    }
                    else
                    {
                        // Server closed connection?
                        break;
                    }

                    ++frames;
                    if (frames % 100 == 0)
                    {
                        Int64 timeTicks = t.Value - t1;
                        Int64 timeElapseInSeconds =
                         timeTicks / t.Frequency;
                        Debug.Print("Frame rate = {0}", (float)frames / (float)timeElapseInSeconds);

                    }
                }

                //fs.Close();

            }, aro.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            #endregion

            return aro;
        }


        public void GetCameraList(out ObservableCollection<Videoinsight.LIB.Camera> cameraList)
        {
            cameraList = new ObservableCollection<Videoinsight.LIB.Camera>();
          
            foreach(Videoinsight.LIB.Camera camera in m_serverClass.Cameras )
                cameraList.Add(camera);
        }


    }
}
