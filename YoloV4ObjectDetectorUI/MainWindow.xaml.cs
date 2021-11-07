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
using Microsoft.Win32;
using WinForms = System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using ParallelObjectDetection;
using System.Diagnostics;

namespace YoloV4ObjectDetectorUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string curDir = "Директория с изображениями не установлена";
        string modelPath = "../../../../ParallelObjectDetection/yolov4.onnx";

        string curCategory = null;

        private Dictionary<string, List<string>> foundCategories = new Dictionary<string, List<string>>();

        OnnxYoloV4Applier modelApplier = null;

        public MainWindow()
        {
            InitializeComponent();
            curDirTextBox.Text = curDir;
            modelPathTextBox.Text = $"Путь до модели:\n{modelPath}";

            modelApplier = new OnnxYoloV4Applier(modelPath);

            Categories_ListBox.ItemsSource = foundCategories.Keys;
        }

        private void SetDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WinForms.FolderBrowserDialog();
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                curDir = dlg.SelectedPath;
                curDirTextBox.Text = curDir;
            }
        }

        private void SetModelPath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            try
            {
                if (dlg.ShowDialog() == true)
                {
                    var testModelApplier = new OnnxYoloV4Applier(dlg.FileName);
                    testModelApplier.TryInstanciateModel();
                    modelPath = dlg.FileName;
                    modelPathTextBox.Text = modelPath;
                    modelApplier = testModelApplier;
                }
                else
                {
                    MessageBox.Show("Файл не выбран");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Не удалось инстанцировать модель");
            }
        }

        private void DetectObjects_Click(object sender, RoutedEventArgs e)
        {
            foundCategories = new Dictionary<string, List<string>>();
            Categories_ListBox.ItemsSource = foundCategories.Keys;
            var t = Task.Factory.StartNew(async () =>
            {
                var filters = new string[] { "jpg", "jpeg", "png", "gif", "tiff", "bmp", "svg" };
                var imagePaths = PredictionUtils.GetFilesFrom(curDir, filters);

                bool foundAllObjects = false;

                var recieveResults = Task.Factory.StartNew(() =>
                {
                    while (!foundAllObjects)
                    {
                        Thread.Sleep(250);
                        while (modelApplier.foundObjectsBuffer.TryReceive(out var value))
                        {
                            var category = value.Value.Label;
                            var imagePath = value.Key;
                            if (!foundCategories.ContainsKey(category))
                            {
                                foundCategories[category] = new List<string>();
                            }
                            foundCategories[category].Add(imagePath);
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                Categories_ListBox.ItemsSource = null;
                                Categories_ListBox.ItemsSource = foundCategories.Keys;
                            }));

                            if (curCategory != null)
                            {
                                AddImageToPanel(imagePath);
                            }
                        }
                    }
                }, TaskCreationOptions.LongRunning);

                var results = await modelApplier.ApplyOnImagesAsync(imagePaths);
                foundAllObjects = true;
            }, TaskCreationOptions.LongRunning);
        }

        private void StopObjectsDetection_Click(object sender, RoutedEventArgs e)
        {
            modelApplier.StopDetection = true;
        }

        private void Categories_ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(Categories_ListBox, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null && item.Content.ToString() != curCategory)
            {
                curCategory = item.Content.ToString();
                foundImagesClassName.Text = $"Найденные изображения для класса {curCategory}";
                UpdateImagesPanelForCategory(curCategory);
            }
        }

        private void UpdateImagesPanelForCategory(string category)
        {
            if (curCategory != null && foundCategories.ContainsKey(category))
            {
                var th = new Thread(() => {
                    this.Dispatcher.BeginInvoke(new Action(() => {
                        ImagesPanel.Children.Clear();
                    }));
                    var curFoundImages = new List<string>(foundCategories[category]);
                    foreach (var imagePath in curFoundImages)
                    {
                        AddImageToPanel(imagePath);
                    }
                });
                th.Start();
            }
        }

        private void AddImageToPanel(string imagePath)
        {
            this.Dispatcher.BeginInvoke(new Action(() => {
                Image image = new Image();
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(imagePath);
                bi.EndInit();
                image.Stretch = Stretch.Fill;
                image.Source = bi;
                ImagesPanel.Children.Add(image);
            }));
        }
    }
}
