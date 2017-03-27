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


namespace VideoSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow1 : Window
    {

        ViewModel vm;

        SurfArray m_surfArray;
        IntPtr m_cudaUtil;
        IntPtr m_cudaContext;
        VIServerController m_serverController;
        uint m_defaultWidth;
        uint m_defaultHeight;
        int m_count;
        bool m_captureRefImage;
        byte[] m_refImageData;
        int m_refImageWidth;
        int m_refImageHeight;

        Detect_net.Detector m_detector;

        // ROI Selection Stuff
            Canvas m_refCanvas;
            Canvas m_testCanvas;

            Point m_refStartPoint, m_refCurrentPoint;
            Rectangle m_refRect;
            bool m_allowRefRoiSelect;
            int m_refRoiX, m_refRoiY, m_refRoiW, m_refRoiH;
            bool m_refDragging = false;

            Point m_testStartPoint, m_testCurrentPoint;
            Rectangle m_testRect;
            bool m_allowTestRoiSelect;
            int m_testRoiX, m_testRoiY, m_testRoiW, m_testRoiH;
            bool m_testDragging = false;


        public MainWindow1()
        {
            m_defaultHeight = 16;  // TODO: these can't be zero for now, since if they're zero, no D3D9 surface will be created.  Can fix this later.
            m_defaultWidth = 16;
            m_count = 0;
            m_captureRefImage = false;

            InitializeComponent();

            vm = new ViewModel();
            this.DataContext = vm;

            // Set default Server
            //vm.ServerIP = "www.demovi.com";
            //vm.ServerDataPort = "4020";
            vm.ServerIP = "10.0.0.219";
            vm.ServerDataPort = "4010";
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

            m_detector = new Detect_net.Detector();
            m_detector.InitDetector();
            
        }

        ~MainWindow1()
        {
            m_detector.ShutdownDetector();
        }


        private void InitRoiSelectionControls()
        {
            m_allowRefRoiSelect = false;

            m_refStartPoint = new Point();
            m_refCurrentPoint = new Point();
            m_refRect = new Rectangle();
            m_testStartPoint = new Point();
            m_testCurrentPoint = new Point();
            m_testRect = new Rectangle();

            m_refRect.Stroke = System.Windows.Media.Brushes.Red;
            m_refRect.StrokeThickness = 3;
            m_testRect.Stroke = System.Windows.Media.Brushes.Blue;
            m_testRect.StrokeThickness = 3;

          
            m_refCanvas.MouseDown += m_refCanvas_MouseDown;
            m_refCanvas.MouseMove += m_refCanvas_MouseMove;
            m_refCanvas.MouseUp += m_refCanvas_MouseUp;
        }



        private void NewLiveFrameHandler(byte[] frame, IntPtr frameGPU, int width, int height, int cameraID, System.Drawing.Imaging.PixelFormat pf, Exception exceptionState)
        {
            // OK it's in here
            // SOmehting in here causing it

            Stopwatch sw = new Stopwatch();
            sw.Start();


            if (cameraID == vm.TestCamera.ID)
            {
                if (vm.TestCamera.pSurface != IntPtr.Zero)
                {
                    try
                    {
                        //if the size of the image has changed, need to resize the DirectX surface
                        if (width != vm.TestCamera.Width || height != vm.TestCamera.Height)
                        {
                            ConfigImageD3DSurface(vm.TestCamera.ID, (uint)width, (uint)height, false);
                            vm.TestCamera.Width = width;
                            vm.TestCamera.Height = height;
                        }

                        vm.TestCamera.d3dImage.Lock();
                        vm.TestCamera.d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, vm.TestCamera.pSurface);

                        // copy GPU array (output of decoder) onto the IDirect3DSurface9
                        //CudaTools.DX_GpuCopyImageToSurface(m_cudaUtil, cameraID, frameGPU);
                        byte[] imageData;
                        if(CudaTools.Cuda_CopyDataFromGpu(m_cudaUtil, out imageData, frameGPU, width * height * 4))
                        {
                            // Calculate Detector Value
                            if(m_detector != null && vm.ROIset)
                            {
                                double value = 0.0;
                                int selectedDetector = vm.SelectedDetector;

                                switch (selectedDetector)
                                {
                                    case 0:
                                        value = m_detector.SurfCalcCorrelation(imageData);
                                        break;                                   
                                    case 1:
                                        value = m_detector.AkazeCalcCorrelation(imageData);
                                        break;
                                    case 2:
                                        value = m_detector.CalcTemplateMatch(imageData);
                                        break;
                                }
                              
                                //double value = m_detector.CalcTemplateMatch(imageData);
                                vm.DetectorValue = value.ToString();
                                vm.AddPoint(value);
                            }
                        }

                        vm.TestCamera.d3dImage.AddDirtyRect(new Int32Rect(0, 0, width, height));
                        vm.TestCamera.d3dImage.Unlock();


                        // if CapRefImage_PB has been clicked, display it in the Ref Image Frame and copy it to m_refImageData 
                        if(m_captureRefImage)
                        {                            
                            m_captureRefImage = false;

                            // copy data to ref image
                            if (CudaTools.Cuda_CopyDataFromGpu(m_cudaUtil, out m_refImageData, frameGPU, width * height * 4))
                            {
                                //if the size of the image has changed, need to resize the DirectX surface
                                if (width != vm.RefCamera.Width || height != vm.RefCamera.Height)
                                {
                                    ConfigImageD3DSurface(vm.RefCamera.ID, (uint)width, (uint)height, false);
                                    vm.RefCamera.Width = width;
                                    vm.RefCamera.Height = height;
                                }

                                vm.RefCamera.d3dImage.Lock();
                                vm.RefCamera.d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, vm.RefCamera.pSurface);

                                CudaTools.DX_GpuCopyImageToSurface(m_cudaUtil, vm.RefCamera.ID, frameGPU);

                                vm.RefCamera.d3dImage.AddDirtyRect(new Int32Rect(0, 0, width, height));
                                vm.RefCamera.d3dImage.Unlock();
                            }
                            else
                            {
                                m_refImageData = null;
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        Debug.Print("Error: " + e.Message);
                    }
                }
            }

            sw.Stop();
            long t = sw.ElapsedMilliseconds;
            UInt64 totalMem = 0;
            UInt64 freeMem = 0;
            int w = vm.TestCamera.Width;
            int h = vm.TestCamera.Height;
            CudaTools.Cuda_GetDeviceMemory64(m_cudaUtil, out totalMem, out freeMem);
            if (freeMem < 1000000)
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
                vm.ControlPanelVisibility = Visibility.Visible;

                // get the list of cameras on this server
                ObservableCollection<Videoinsight.LIB.Camera> cameraList;
                m_serverController.GetCameraList(out cameraList);

                // Set the camera list to the View Model for this app
                vm.CameraList = cameraList;

                // open dialog that allows you to select the cameras that you want to stream
                CameraSelection dlg = new CameraSelection(cameraList);
                dlg.ShowDialog();
                int selectedCameraID = dlg.vm.SelectedCamera.ID;

                // After the dialog is closed, build ViewModel's ActiveDictionary, which is the dictionary that contains the list of the cameras that were marked to be streamed
                // in the dialog above (the cameras that were checked)

                foreach (Videoinsight.LIB.Camera cam in cameraList)
                {
                    if (cam.CameraID == selectedCameraID)
                    {
                        vm.TestCamera = new CameraParams(cam, 0, 0, 0, 0, -1, IntPtr.Zero, null, -1);
                        vm.RefCamera = new CameraParams(cam, 0, 0, 0, 0, -1, IntPtr.Zero, null, -1);
                        break;
                    }
                }


                if (m_surfArray != null)
                {
                    m_surfArray.Shutdown();
                    m_surfArray = null;
                }

                // build the SurfArray = this is the 2D "array" of D3D9 panels that are used to display the decoded images 
                m_surfArray = new SurfArray(1, 2, DisplayContainer);

                // configure the behavior and look of the Surface Array
                Color panelSelectedColor = Colors.Red;
                Color panelUnselectedColor = Colors.Gray;
                Color panelTitleTextColor = Colors.White;
                Color surfArrayBackgroundColor = Colors.Gray;
                double titleFontSize = 12.0;
                double panelMargin = 10.0;
                m_surfArray.SetDisplayParams(panelSelectedColor, panelUnselectedColor, panelTitleTextColor, titleFontSize, surfArrayBackgroundColor, panelMargin);
                m_surfArray.SetPanelsSelectable(false);  // if true, the callback function is called when a panel is clicked

                // set the callback function that is called whenever a particular panel is clicked
                m_surfArray.SetCallback(CallbackFunction);


                // assign each camera to a position in the SurfArray
                uint w = m_defaultWidth;
                uint h = m_defaultHeight;
                bool useAlpha = false;
                IntPtr pSurf = IntPtr.Zero;
                bool success = false;

                // Reference Image
                m_surfArray.AssignCameraToPosition(0, 0, (uint)selectedCameraID+1, m_defaultWidth, m_defaultHeight, "Reference Image", false);
                vm.RefCamera.SurfaceIndex = m_surfArray.GetSurfaceIndex(0, 0);
                pSurf = IntPtr.Zero;
                m_surfArray.GetSurface_Params(vm.RefCamera.SurfaceIndex, out pSurf, out w, out h, out useAlpha);
                vm.RefCamera.DisplayRow = 0;
                vm.RefCamera.DisplayCol = 0; 
                vm.RefCamera.Width = (int)w;
                vm.RefCamera.Height = (int)h;
                vm.RefCamera.pSurface = pSurf;
                vm.RefCamera.d3dImage = m_surfArray.GetD3DImage(vm.RefCamera.DisplayRow, vm.RefCamera.DisplayCol);
                vm.RefCamera.ID = selectedCameraID + 1;
                success = CudaTools.DX_GpuAddD3DSurface(m_cudaUtil, vm.RefCamera.SurfaceIndex, vm.RefCamera.pSurface, vm.RefCamera.Width, vm.RefCamera.Height);

                // Test Image
                m_surfArray.AssignCameraToPosition(0, 1, (uint)selectedCameraID, m_defaultWidth, m_defaultHeight, "Test Image", false);
                vm.TestCamera.SurfaceIndex = m_surfArray.GetSurfaceIndex(0, 1);
                pSurf = IntPtr.Zero;
                m_surfArray.GetSurface_Params(vm.TestCamera.SurfaceIndex, out pSurf, out w, out h, out useAlpha);
                vm.TestCamera.DisplayRow = 0;
                vm.TestCamera.DisplayCol = 1;
                vm.TestCamera.Width = (int)w;
                vm.TestCamera.Height = (int)h;
                vm.TestCamera.pSurface = pSurf;
                vm.TestCamera.d3dImage = m_surfArray.GetD3DImage(vm.TestCamera.DisplayRow, vm.TestCamera.DisplayCol);
                vm.TestCamera.ID = selectedCameraID;
                success = CudaTools.DX_GpuAddD3DSurface(m_cudaUtil, vm.TestCamera.SurfaceIndex, vm.TestCamera.pSurface, vm.TestCamera.Width, vm.TestCamera.Height);

                
                m_refCanvas = m_surfArray.GetCanvas((uint)vm.RefCamera.ID);
                m_testCanvas = m_surfArray.GetCanvas((uint)vm.TestCamera.ID);
                InitRoiSelectionControls();


                // See how much memory is left on GPU
                ulong totMem = 0;
                ulong freeMem = 0;
                CudaTools.Cuda_GetDeviceMemory64(m_cudaUtil, out totMem, out freeMem);
                //MessageBox.Show("Total = " + totMem.ToString() + "   Free = " + freeMem.ToString());



                // command server to start streaming
                List<int> cameraIDList = new List<int>();
                cameraIDList.Add(vm.TestCamera.ID);
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


            try
            {
                CameraParams cp;
                if (ID == vm.TestCamera.ID)
                    cp = vm.TestCamera;
                else if (ID == vm.RefCamera.ID)
                    cp = vm.RefCamera;
                else
                    return;

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
                cp.SurfaceIndex = surfaceIndex;
         
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }


        }


        private void ConnectPB_Click(object sender, RoutedEventArgs e)
        {
            short dataport = Convert.ToInt16(vm.ServerDataPort);

            m_serverController.ConnectToServer(vm.ServerIP, dataport, vm.Username, vm.Password);
        }

        private void QuitPB_Click(object sender, RoutedEventArgs e)
        {
            m_serverController.Shutdown();  // this doesn't do anything yet, but needs to!!

            if (m_surfArray != null)
                m_surfArray.Shutdown();

            CudaTools.Cuda_Free64(m_cudaUtil);

            Close();
        }

        private void SetRefPB_Click(object sender, RoutedEventArgs e)
        {
            m_captureRefImage = true;
            m_allowRefRoiSelect = true;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Mouse Handling routines for ROI selection



        /// MouseUp Handler for the Test Image Display Canvas that is overlaid over the image display  
        void m_testCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            m_testDragging = false;
        }

        /// MouseUp Handler for the Reference Image Display Canvas that is overlaid over the image display     
        void m_refCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            m_refDragging = false;


            m_testCanvas.Children.Remove(m_testRect);

            m_testRect.Width = m_refRect.Width;
            m_testRect.Height = m_refRect.Height;
            Canvas.SetLeft(m_testRect, Canvas.GetLeft(m_refRect));
            Canvas.SetTop(m_testRect, Canvas.GetTop(m_refRect));

            m_testCanvas.Children.Add(m_testRect);


            vm.ROI = new System.Drawing.Rectangle(m_refRoiX, m_refRoiY, m_refRoiW, m_refRoiH);
            vm.ROIset = true;

            m_detector.SetRefImage(m_refImageData, (uint)vm.RefCamera.Width, (uint)vm.RefCamera.Height, (uint)vm.ROI.X, (uint)vm.ROI.Y, (uint)vm.ROI.Width, (uint)vm.ROI.Height);

        }

        /// MouseMove Handler for the Test Image Display Canvas       
        void m_testCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (m_testDragging && m_allowTestRoiSelect && e.LeftButton == MouseButtonState.Pressed)
            {
                m_testCurrentPoint = e.GetPosition(m_testCanvas);

                double top, left, width, height;

                if (m_testStartPoint.Y < m_testCurrentPoint.Y)
                {
                    top = m_testStartPoint.Y;
                    height = m_testCurrentPoint.Y - m_testStartPoint.Y;
                }
                else
                {
                    top = m_testCurrentPoint.Y;
                    height = m_testStartPoint.Y - m_testCurrentPoint.Y;
                }

                if (m_testStartPoint.X < m_testCurrentPoint.X)
                {
                    left = m_testStartPoint.X;
                    width = m_testCurrentPoint.X - m_testStartPoint.X;
                }
                else
                {
                    left = m_testCurrentPoint.X;
                    width = m_testStartPoint.X - m_testCurrentPoint.X;
                }

                m_testCanvas.Children.Remove(m_testRect);

                m_testRect.Width = width;
                m_testRect.Height = height;
                Canvas.SetLeft(m_testRect, left);
                Canvas.SetTop(m_testRect, top);

                m_testRoiX = (int)left;
                m_testRoiY = (int)top;
                m_testRoiW = (int)width;
                m_testRoiH = (int)height;

                m_testCanvas.Children.Add(m_testRect);
            }
        }

        /// MouseDown Handler for the Test Image Display Canvas
        void m_testCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (m_allowTestRoiSelect && e.ButtonState == MouseButtonState.Pressed)
            {
                m_testStartPoint = e.GetPosition(m_testCanvas);
                m_testCurrentPoint = m_testStartPoint;
                m_testDragging = true;
            }
        }


        /// MouseMove Handler for the Reference Image Display Canvas
        void m_refCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (m_refDragging && m_allowRefRoiSelect && e.LeftButton == MouseButtonState.Pressed)
            {
                m_refCurrentPoint = e.GetPosition(m_refCanvas);

                double top, left, width, height;

                if (m_refStartPoint.Y < m_refCurrentPoint.Y)
                {
                    top = m_refStartPoint.Y;
                    height = m_refCurrentPoint.Y - m_refStartPoint.Y;
                }
                else
                {
                    top = m_refCurrentPoint.Y;
                    height = m_refStartPoint.Y - m_refCurrentPoint.Y;
                }

                if (m_refStartPoint.X < m_refCurrentPoint.X)
                {
                    left = m_refStartPoint.X;
                    width = m_refCurrentPoint.X - m_refStartPoint.X;
                }
                else
                {
                    left = m_refCurrentPoint.X;
                    width = m_refStartPoint.X - m_refCurrentPoint.X;
                }

                m_refCanvas.Children.Remove(m_refRect);

                m_refRect.Width = width;
                m_refRect.Height = height;
                Canvas.SetLeft(m_refRect, left);
                Canvas.SetTop(m_refRect, top);

                m_refRoiX = (int)left;
                m_refRoiY = (int)top;
                m_refRoiW = (int)width;
                m_refRoiH = (int)height;

                m_refCanvas.Children.Add(m_refRect);
            }
        }


        /// MouseDown Handler for the Reference Image Display Canvas
        void m_refCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (m_allowRefRoiSelect && e.ButtonState == MouseButtonState.Pressed)
            {
                m_refStartPoint = e.GetPosition(m_refCanvas);
                m_refCurrentPoint = m_refStartPoint;
                m_refDragging = true;
                vm.ROIset = false;
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            vm.ResetPlot();
        }




        //////////////////////////////////////////////////////////////////////////////////////////////////////////


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
        public int ID { get; set; }

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
            ID = -1;
        }

        public CameraParams(Videoinsight.LIB.Camera _camera, int _w, int _h, int _displayRow, int _displayCol, int _surfaceIndex, IntPtr _pSurface, D3DImage _d3dImage = null, int id = -1)
        {
            camera = _camera;
            Width = _w;
            Height = _h;
            DisplayRow = _displayRow;
            DisplayCol = _displayCol;
            SurfaceIndex = _surfaceIndex;
            pSurface = _pSurface;
            d3dImage = _d3dImage;
            ID = id;
        }

    }



    public class ViewModel : INotifyPropertyChanged
    {

        private bool _connected;
        private ObservableCollection<Videoinsight.LIB.Camera> _cameraList;
        private CameraParams _testCamera;
        private CameraParams _refCamera;
        private string _serverIP;
        private string _serverDataPort;
        private string _username;
        private string _password;
        private Visibility _controlPanelVisibility;

        private bool _ROIset;
        private System.Drawing.Rectangle _ROI;

        private string _detectorValue;

        private bool[] _detectorArray = new bool[] { true, false, false };

        public OxyPlot.PlotModel MyPlotModel { get; private set; }
        public  OxyPlot.Series.LineSeries MyDataSeries {get; set;}
        public OxyPlot.Axes.LinearAxis MyXAxis { get; set; }
        public OxyPlot.Axes.LinearAxis MyYAxis { get; set; }

        public int pointCount { get; set; }
        public int pointLimit { get; set; }
        public double maxValue { get; set; }

        public ViewModel()
        {
            _connected = false;
            _cameraList = new ObservableCollection<Videoinsight.LIB.Camera>();
            _testCamera = null;
            _controlPanelVisibility = Visibility.Collapsed;
            _detectorValue = "0";

            pointCount = 0;
            pointLimit = 100;
            maxValue = 1.5;

            MyPlotModel = new OxyPlot.PlotModel { Title = "", TitleFontSize=10 };

            MyXAxis = new OxyPlot.Axes.LinearAxis 
            { 
                Position = OxyPlot.Axes.AxisPosition.Bottom, 
                Minimum = 0, 
                Maximum = pointLimit,
                Title="",
                MajorGridlineStyle = OxyPlot.LineStyle.None,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                TickStyle = OxyPlot.Axes.TickStyle.None
            };
            MyYAxis = new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Minimum = 0,
                Maximum = maxValue,
                Title = "",
                MajorGridlineStyle = OxyPlot.LineStyle.None,
                MinorGridlineStyle = OxyPlot.LineStyle.None, 
                TickStyle = OxyPlot.Axes.TickStyle.None
            };       
            MyPlotModel.Axes.Add(MyXAxis);
            MyPlotModel.Axes.Add(MyYAxis);
            MyXAxis.IsZoomEnabled = false;
            MyXAxis.IsPanEnabled = false;
            MyYAxis.IsZoomEnabled = false;
            MyYAxis.IsPanEnabled = false;


            MyDataSeries = new OxyPlot.Series.LineSeries { Title = "", MarkerType = OxyPlot.MarkerType.None, };
            MyDataSeries.Points.Add(new OxyPlot.DataPoint(0, 0));
            MyDataSeries.Points.Add(new OxyPlot.DataPoint(1, 1));
            MyDataSeries.Points.Add(new OxyPlot.DataPoint(2, 0.5));
            MyDataSeries.Points.Add(new OxyPlot.DataPoint(3, 1));
            MyPlotModel.Series.Add(MyDataSeries);
                       
        }

        ~ViewModel()
        {

        }

        public void ResetPlot()
        {
            MyDataSeries.Points.Clear();
            pointCount = 0;
            maxValue = 1.5;
            MyYAxis.Maximum = maxValue;
            MyPlotModel.InvalidatePlot(true);
        }

        public void AddPoint(double y)
        {
            if (pointCount == pointLimit)
            {
                // shift all points to the left
                for (int i = 0; i < pointCount - 1; i++)
                {
                    MyDataSeries.Points[i] = new OxyPlot.DataPoint(i, MyDataSeries.Points[i + 1].Y);
                }

                MyDataSeries.Points[pointCount - 1] = new OxyPlot.DataPoint(pointCount - 1, y);
            }
            else
            {
                MyDataSeries.Points.Add(new OxyPlot.DataPoint(pointCount, y));
            }

            if (pointCount < pointLimit) pointCount++;

            // check range limits
            if(y>maxValue)
            {
                maxValue = y;
                MyYAxis.Maximum = maxValue * 1.1;
            }

            MyPlotModel.InvalidatePlot(true);
        }

     


        public Videoinsight.LIB.Camera GetCamera(int id)
        {
            Videoinsight.LIB.Camera cam = null;
            foreach (Videoinsight.LIB.Camera camera in CameraList)
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

        public CameraParams TestCamera
        {
            get
            {
                return _testCamera;
            }
            set
            {
                _testCamera = value;
                OnPropertyChanged(new PropertyChangedEventArgs("TestCamera"));
            }
        }


        public CameraParams RefCamera
        {
            get
            {
                return _refCamera;
            }
            set
            {
                _refCamera = value;
                OnPropertyChanged(new PropertyChangedEventArgs("RefCamera"));
            }
        }

        public Visibility ControlPanelVisibility
        {
            get
            {
                return _controlPanelVisibility;
            }
            set
            {
                _controlPanelVisibility = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ControlPanelVisibility"));
            }
        }


        public bool ROIset
        {
            get { return _ROIset; }
            set
            {
                if (value != _ROIset)
                {
                    _ROIset = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ROIset"));
                }
            }
        }


        public string DetectorValue
        {
            get { return _detectorValue; }
            set
            {
                if (value != _detectorValue)
                {
                    _detectorValue = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("DetectorValue"));
                }
            }
        }

        public System.Drawing.Rectangle ROI
        {
            get { return _ROI; }
            set
            {
                if (value != _ROI)
                {
                    _ROI = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ROI"));
                }
            }
        }


        
        public bool[] DetectorArray
        {
            get { return _detectorArray; }
        }

        public int SelectedDetector
        {
            get
            {
                for (int i = 0; i < _detectorArray.Length; i++)
                    if (_detectorArray[i]) return i;
                return -1;
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
