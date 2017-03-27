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

namespace vglibTestBench
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
                vm.CameraList.Add(new CheckedListItem<Videoinsight.LIB.Camera>(cam));
            }

            this.DataContext = vm;

            this.Title = vm.CameraList.Count.ToString() + " Cameras";
        }

        private void DonePB_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SelectAllPB_Click(object sender, RoutedEventArgs e)
        {
            foreach(CheckedListItem<Videoinsight.LIB.Camera> cam in vm.CameraList)
            {
                cam.IsChecked = true;
            }
        }

        private void UnselectAllPB_Click(object sender, RoutedEventArgs e)
        {
            foreach (CheckedListItem<Videoinsight.LIB.Camera> cam in vm.CameraList)
            {
                cam.IsChecked = false;
            }
        }
    }


    public class CheckedListItem<T> : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool isChecked;
        private T item;

        public CheckedListItem()
        { }

        public CheckedListItem(T item, bool isChecked = false)
        {
            this.item = item;
            this.isChecked = isChecked;
        }

        public T Item
        {
            get { return item; }
            set
            {
                item = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Item"));
            }
        }


        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                isChecked = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("IsChecked"));
            }
        }
    }


    public class Camera {
        public string Name { get; set; }
        public int ID {get; set;}
    }

  


    public class CameraSelectionViewModel : INotifyPropertyChanged
    {      
        private ObservableCollection<CheckedListItem<Videoinsight.LIB.Camera>> _cameraList;

        public CameraSelectionViewModel()
        {
           
            _cameraList = new ObservableCollection<CheckedListItem<Videoinsight.LIB.Camera>>();
        }




        public ObservableCollection<CheckedListItem<Videoinsight.LIB.Camera>> CameraList
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
