using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfD3D;
using NVIDIA;
using System.ComponentModel;
using VISDK;
using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Windows.Interop;

namespace vglibTestBench
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ViewModel vm;

        SurfArray m_surfArray;
        IntPtr m_cudaUtil;
        IntPtr m_cudaContext;
        VIServerController m_serverController;
        uint m_defaultWidth;
        uint m_defaultHeight;
        int m_count;

        public MainWindow()
        {
            m_defaultHeight = 10;  // TODO: these can't be zero for now, since if they're zero, no D3D9 surface will be created.  Can fix this later.
            m_defaultWidth = 10;
            m_count = 0;

            InitializeComponent();

            vm = new ViewModel();
            this.DataContext = vm;

            // Set default Server
            vm.ServerIP = "www.demovi.com";
            vm.ServerDataPort = "4020";
            //vm.ServerIP = "10.0.0.219";
            //vm.ServerDataPort = "4010";
            vm.Username = "Admin";
            vm.Password = "";


            bool success;
            m_cudaUtil = CudaTools.Cuda_Create64();
            success = CudaTools.Cuda_GetContext64(m_cudaUtil, out m_cudaContext);

            byte[] deviceName = new byte[100];
            CudaTools.Cuda_GetDeviceName64(m_cudaUtil, deviceName);
            ulong totMem = 0;
            ulong freeMem = 0;
            CudaTools.Cuda_GetDeviceMemory64(m_cudaUtil, out totMem, out freeMem);

            m_serverController = new VIServerController(m_cudaUtil);
            m_serverController.Connected += ServerController_Connected;

            VIClient.OnNewLiveFrame frameHandler = NewLiveFrameHandler;
            m_serverController.SetNewLiveFrameCallback(frameHandler);

        }


        private void NewLiveFrameHandler(byte[] frame, IntPtr frameGPU, int width, int height, int cameraID, System.Drawing.Imaging.PixelFormat pf, Exception exceptionState)
        {
            // OK it's in here
            // SOmehting in here causing it
            
            Stopwatch sw = new Stopwatch();
            sw.Start();

            CameraParams cp;

            if(vm.ActiveDictionary.TryGetValue(cameraID, out cp))
            {             

                if (cp.pSurface != IntPtr.Zero)
                {
                    try
                    {
                        // if the size of the image has changed, need to resize the DirectX surface
                        if (width != cp.Width || height != cp.Height)
                        {
                            ConfigImageD3DSurface(cameraID, (uint)width, (uint)height, false);
                            cp.Width = width;
                            cp.Height = height;
                        }

                        cp.d3dImage.Lock();
                        cp.d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, cp.pSurface);

                        
                        // copy GPU array (output of decoder) onto the IDirect3DSurface9
                        //CudaTools.DX_GpuCopyImageToSurface(m_cudaUtil, cameraID, frameGPU);
                        

                        cp.d3dImage.AddDirtyRect(new Int32Rect(0, 0, width, height));
                        cp.d3dImage.Unlock();
                    }
                    catch (Exception e)
                    {
                        //Debug.Print("Error: " + e.Message);
                    }
                }
            }

            sw.Stop();
            long t = sw.ElapsedMilliseconds;
            UInt64 totalMem = 0;
            UInt64 freeMem = 0;
            int w = cp.Width;
            int h = cp.Height;
            CudaTools.Cuda_GetDeviceMemory64(m_cudaUtil, out totalMem, out freeMem);
            if(freeMem < 1000000)
            {
                Debug.Print("Out of GPU Memory");
            }

            //Debug.Print("Display Time: " + t.ToString());
        }


        

        void ServerController_Connected(VIServerController serverController, EventArgs e)
        {
            // run on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {

                // Disable the Connect Button since, at this time, we can't disconnect/reconnect
                ConnectPB.IsEnabled = false;

                // get the list of cameras on this server
                ObservableCollection<Videoinsight.LIB.Camera> cameraList;
                m_serverController.GetCameraList(out cameraList);

                // Set the camera list to the View Model for this app
                vm.CameraList = cameraList;

                // open dialog that allows you to select the cameras that you want to stream
                CameraSelection dlg = new CameraSelection(cameraList);
                dlg.ShowDialog();

                // After the dialog is closed, build ViewModel's ActiveDictionary, which is the dictionary that contains the list of the cameras that were marked to be streamed
                // in the dialog above (the cameras that were checked)
                vm.ResetActiveDictionary(); // clear the ActiveDictionary
                foreach (CheckedListItem<Videoinsight.LIB.Camera> cam in dlg.vm.CameraList)
                {
                    if (cam.IsChecked)
                    {
                        vm.ActiveDictionary.Add(cam.Item.CameraID, new CameraParams(cam.Item, 0, 0, 0, 0, -1, IntPtr.Zero));
                    }
                }

                // Figure out the size of the array of display panels needed to show all the cameras in the Active Dictionary
                int cols = 0;
                int rows = 0;
                GetMatrixDimensions(vm.ActiveDictionary.Count, ref rows, ref cols); // this is a really dumb function that sets the number of rows and cols depending on the number of cameras to be displayed

                if (m_surfArray != null)
                {
                    m_surfArray.Shutdown();
                    m_surfArray = null;
                }

                // build the SurfArray = this is the 2D "array" of D3D9 panels that are used to display the decoded images 
                m_surfArray = new SurfArray(rows, cols, DisplayContainer);

                // configure the behavior and look of the Surface Array
                Color panelSelectedColor = Colors.Red;
                Color panelUnselectedColor = Colors.Gray;
                Color panelTitleTextColor = Colors.White;
                Color surfArrayBackgroundColor = Colors.Gray;
                double titleFontSize = 12.0;
                double panelMargin = 10.0;                
                m_surfArray.SetDisplayParams(panelSelectedColor, panelUnselectedColor, panelTitleTextColor,titleFontSize,surfArrayBackgroundColor, panelMargin);
                m_surfArray.SetPanelsSelectable(true);  // if true, the callback function is called when a panel is clicked

                // set the callback function that is called whenever a particular panel is clicked
                m_surfArray.SetCallback(CallbackFunction);
             

                // assign each camera to a position in the SurfArray
                int index = 0;
                foreach (KeyValuePair<int, CameraParams> entry in vm.ActiveDictionary)
                {
                    int id = entry.Key;
                    //CameraParams cp = entry.Value;

                    if (index >= vm.ActiveDictionary.Count) break;
                    int r = index / cols;
                    int c = index - (r * cols);

                    Videoinsight.LIB.Camera cam = vm.GetCamera(id);
                    if (cam != null) m_surfArray.AssignCameraToPosition(r, c, (uint)cam.CameraID, m_defaultWidth, m_defaultHeight, cam.CameraName, false);


                    entry.Value.DisplayRow = r;
                    entry.Value.DisplayCol = c;
                    entry.Value.SurfaceIndex = m_surfArray.GetSurfaceIndex(r, c);

                    uint w = m_defaultWidth;
                    uint h = m_defaultHeight;
                    bool useAlpha = false;
                    IntPtr pSurf = IntPtr.Zero;
                    m_surfArray.GetSurface_Params(entry.Value.SurfaceIndex, out pSurf, out w, out h, out useAlpha);

                    entry.Value.Width = (int)w;
                    entry.Value.Height = (int)h;
                    entry.Value.pSurface = pSurf;

                    entry.Value.d3dImage = m_surfArray.GetD3DImage(entry.Value.DisplayRow, entry.Value.DisplayCol);

                    bool success = CudaTools.DX_GpuAddD3DSurface(m_cudaUtil, id, entry.Value.pSurface, entry.Value.Width, entry.Value.Height);

                    //if (vm.ActiveDictionary.ContainsKey(id))
                    //    vm.ActiveDictionary[id] = cp;
                    //else
                    //    Debug.Print("Shit");

                    index++;
                }


                // See how much memory is left on GPU
                ulong totMem = 0;
                ulong freeMem = 0;
                CudaTools.Cuda_GetDeviceMemory64(m_cudaUtil, out totMem, out freeMem);
                //MessageBox.Show("Total = " + totMem.ToString() + "   Free = " + freeMem.ToString());



                // command server to start streaming
                List<int> cameraIDList = new List<int>();
                foreach (int id in vm.ActiveDictionary.Keys) cameraIDList.Add(id);
                m_serverController.GetLive(cameraIDList);

            });
        }

   

        public void CallbackFunction(int row, int col, UInt32 cameraID)
        {
            MessageBox.Show("Row:" + row.ToString() + "  Col:" + col.ToString() + "   CameraID:" + cameraID.ToString());
        }


        public void ConfigImageD3DSurface(int ID, uint pixelWidth, uint pixelHeight, bool useAlphaChannel)
        {
            // ID = unique id for this display panel in the surface array.  It might be an ID for the camera, or an ID for an experiment indicator
            // pixelWidth, pixelHeight = the size of the display panel in pixels.  This should match the size of the image to be displaye on it.
            // panelHeader = string that is displayed above the panel
            // useAlphaChannel = sets whether to use the alpha channel or not (usually false)

            bool success;

            if (vm.ActiveDictionary.ContainsKey(ID))
            {
                try
                {
                    CameraParams cp = vm.ActiveDictionary[ID];

                    // if there's a surface already at this position, remove it, since we're about to create a new one
                    success = CudaTools.DX_GpuRemoveD3DSurface(m_cudaUtil, ID);


                    // create new surface, and use the Invoke command to make sure it runs on UI thread since there's some UI-dependent code in here

                    success = m_surfArray.AssignCameraToPosition(cp.DisplayRow, cp.DisplayCol, (uint)ID, pixelWidth, pixelHeight, cp.camera.CameraName, false);

                    int surfaceIndex = m_surfArray.GetSurfaceIndex(cp.DisplayRow, cp.DisplayCol);

                    // get pSurface and d3dImage
                    IntPtr pSurface = IntPtr.Zero;
                    uint uWidth;
                    uint uHeight;
                    bool useAlpha;

                    m_surfArray.GetSurface_Params(surfaceIndex, out pSurface, out uWidth, out uHeight, out useAlpha);

                    success = CudaTools.DX_GpuAddD3DSurface(m_cudaUtil, ID, pSurface, (int)uWidth, (int)uHeight);

                    cp.Width = (int)uWidth;
                    cp.Height = (int)uHeight;
                    cp.d3dImage = m_surfArray.GetD3DImage(cp.DisplayRow, cp.DisplayCol);
                    cp.pSurface = pSurface;

                    vm.ActiveDictionary[ID] = cp;
                }
                catch(Exception e)
                {
                    Debug.Print(e.Message);
                }

            }
        }


        private Videoinsight.LIB.Camera GetCamera(int id)
        {
            Videoinsight.LIB.Camera camera = null;

            foreach(Videoinsight.LIB.Camera cam in vm.CameraList)
            {
                if (id == cam.CameraID)
                {
                    camera = cam;
                    break;
                }
            }

            return camera;
        }



        private void GetMatrixDimensions(int numCameras, ref int rows, ref int cols)
        {
            switch (numCameras)
            {
                case 1: rows = 1; cols = 1; break;
                case 2: rows = 1; cols = 2; break;
                case 3: rows = 2; cols = 2; break;
                case 4: rows = 2; cols = 2; break;
                case 5: rows = 2; cols = 3; break;
                case 6: rows = 2; cols = 3; break;
                case 7: rows = 3; cols = 3; break;
                case 8: rows = 3; cols = 3; break;
                case 9: rows = 3; cols = 3; break;
                case 10: rows = 4; cols = 3; break;
                case 11: rows = 4; cols = 3; break;
                case 12: rows = 4; cols = 3; break;
                case 13: rows = 3; cols = 4; break;
                case 14: rows = 4; cols = 4; break;
                case 15: rows = 4; cols = 4; break;
                case 16: rows = 4; cols = 4; break;
                case 17: rows = 5; cols = 4; break;
                case 18: rows = 5; cols = 4; break;
                case 19: rows = 5; cols = 4; break;
                case 20: rows = 5; cols = 4; break;
                default: rows = 5; cols = 4; break;
            }
        }

        private void QuitPB_Click(object sender, RoutedEventArgs e)
        {
            m_serverController.Shutdown();  // this doesn't do anything yet, but needs to!!

            if(m_surfArray!=null)
                m_surfArray.Shutdown();

            vm.ResetActiveDictionary();

            CudaTools.Cuda_Free64(m_cudaUtil);

            Close();
        }

        private void ConnectPB_Click(object sender, RoutedEventArgs e)
        {
            short dataport = Convert.ToInt16(vm.ServerDataPort);

            m_serverController.ConnectToServer(vm.ServerIP,dataport,vm.Username,vm.Password);
        }



    }



    public class CameraParams
    {
        public Videoinsight.LIB.Camera camera { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int DisplayRow { get; set; }
        public int DisplayCol { get; set; }
        public int SurfaceIndex { get; set; }
        public IntPtr pSurface { get; set; }
        public D3DImage d3dImage { get; set; }

        public CameraParams()
        {
            camera = null;
            Width = 0;
            Height = 0;
            DisplayRow = 0;
            DisplayCol = 0;
            SurfaceIndex = -1;
            pSurface = IntPtr.Zero;
            d3dImage = null;
        }

        public CameraParams(Videoinsight.LIB.Camera _camera, int _w, int _h, int _displayRow, int _displayCol, int _surfaceIndex, IntPtr _pSurface, D3DImage _d3dImage = null)
        {
            camera = _camera;
            Width = _w;
            Height = _h;
            DisplayRow = _displayRow;
            DisplayCol = _displayCol;
            SurfaceIndex = _surfaceIndex;
            pSurface = _pSurface;
            d3dImage = _d3dImage;
        }
    }


    public class ViewModel : INotifyPropertyChanged
    {

        private bool _connected;
        private ObservableCollection<Videoinsight.LIB.Camera> _cameraList;
        private Dictionary<int, CameraParams> _activeDictionary;
        private string _serverIP;
        private string _serverDataPort;
        private string _username;
        private string _password;


        public ViewModel()
        {
            _connected = false;
            _cameraList = new ObservableCollection<Videoinsight.LIB.Camera>();
            _activeDictionary = new Dictionary<int, CameraParams>();
        }

        ~ViewModel()
        {
            ActiveDictionary.Clear();
        }


        public void ResetActiveDictionary()
        {          
            ActiveDictionary.Clear();
        }


        public Videoinsight.LIB.Camera GetCamera(int id)
        {
            Videoinsight.LIB.Camera cam = null;
            foreach(Videoinsight.LIB.Camera camera in CameraList)
            {
                if (camera.CameraID == id) cam = camera;
            }
            return cam;
        }


        public bool Connected
        {
            get
            {
                return _connected;
            }
            set
            {
                _connected = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Connected"));
            }
        }

        public string ServerIP
        {
            get
            {
                return _serverIP;
            }
            set
            {
                _serverIP = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ServerIP"));
            }
        }

        public string ServerDataPort
        {
            get
            {
                return _serverDataPort;
            }
            set
            {
                _serverDataPort = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ServerDataPort"));
            }
        }

        public string Username
        {
            get
            {
                return _username;
            }
            set
            {
                _username = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Username"));
            }
        }

        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                _password = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Password"));
            }
        }


        public ObservableCollection<Videoinsight.LIB.Camera> CameraList
        {
            get
            {
                return _cameraList;
            }
            set
            {
                _cameraList = value;
                OnPropertyChanged(new PropertyChangedEventArgs("CameraList"));
            }
        }

        public Dictionary<int, CameraParams> ActiveDictionary
        {
            get
            {
                return _activeDictionary;
            }
            set
            {
                _activeDictionary = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ActiveDictionary"));
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, e);
            }
        }
    }
}
