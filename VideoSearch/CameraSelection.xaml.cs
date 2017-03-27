using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace VideoSearch
{
    /// <summary>
    /// Interaction logic for CameraSelection.xaml
    /// </summary>
    public partial class CameraSelection : Window
    {

        public CameraSelectionViewModel vm;

        public CameraSelection(ObservableCollection<Videoinsight.LIB.Camera> cameraList)
        {
            InitializeComponent();
            vm = new CameraSelectionViewModel();

            foreach (Videoinsight.LIB.Camera cam in cameraList)
            {
                CameraItem ci = new CameraItem(cam.CameraName, cam.CameraID);
                vm.CameraList.Add(ci);
            }

            if(vm.CameraList.Count>0)
                vm.SelectedCamera = vm.CameraList[0];

            this.DataContext = vm;

            this.Title = vm.CameraList.Count.ToString() + " Cameras";
        }

        private void DonePB_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

    
    }




    //public class Camera {
    //    public string Name { get; set; }
    //    public int ID {get; set;}
    //}

  
    public class CameraItem
    {
        public string Name { get; set; }
        public int ID { get; set; }

        public CameraItem(string name, int id)
        {
            Name = name;
            ID = id;
        }
    }


    public class CameraSelectionViewModel : INotifyPropertyChanged
    {      
        private ObservableCollection<CameraItem> _cameraList;
        private CameraItem _selectedCamera;

        public CameraSelectionViewModel()
        {
           
            _cameraList = new ObservableCollection<CameraItem>();
        }

        public ObservableCollection<CameraItem> CameraList
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


        public CameraItem SelectedCamera
        {
            get
            {
                return _selectedCamera;
            }
            set
            {
                _selectedCamera = value;
                OnPropertyChanged(new PropertyChangedEventArgs("SelectedCamera"));
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
