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
using System.IO;

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

        private Dictionary<string, List<Tuple<string, int[]>>> foundCategories = new Dictionary<string, List<Tuple<string, int[]>>>();

        OnnxYoloV4Applier modelApplier = null;

        public MainWindow()
        {
            InitializeComponent();
            curDirTextBox.Text = curDir;
            modelPathTextBox.Text = $"Путь до модели:\n{modelPath}";

            modelApplier = new OnnxYoloV4Applier(modelPath);

            Categories_ListBox.ItemsSource = foundCategories.Keys;
            reloadDB();
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
            foundCategories = new Dictionary<string, List<Tuple<string, int[]>>>();
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
                            float[] floatCoords = value.Value.BBox;
                            int[] coords = {
                                (int) floatCoords[0],
                                (int) floatCoords[1],
                                (int) (floatCoords[2] - floatCoords[0]),
                                (int) (floatCoords[3] - floatCoords[1])
                            };
                            if (!foundCategories.ContainsKey(category))
                            {
                                foundCategories[category] = new List<Tuple<string, int[]>>();
                            }
                            foundCategories[category].Add(new Tuple<string, int[]>(imagePath, coords));
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                Categories_ListBox.ItemsSource = null;
                                Categories_ListBox.ItemsSource = foundCategories.Keys;
                            }));

                            if (category == curCategory)
                            {
                                AddImageToPanel(imagePath, coords);
                            }
                            UploadToDB(new DetectedObject() { 
                                X      = coords[0],
                                Y      = coords[1],
                                Width  = coords[2],
                                Height = coords[3],
                                ClassName = category,
                                Details = new DetectedObjectDetails() { Image = ImageToBytes(CropFromPath(imagePath, coords)) }
                            });
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

        private void Refresh_DB_Click(object sender, RoutedEventArgs e)
        {
            reloadDB();
        }

        private void Clear_DB_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new LibraryContext())
            {
                db.DetectedObjects.RemoveRange(db.DetectedObjects);
                db.SaveChanges();
            }
            reloadDB();
        }

        private void reloadDB()
        {
            DB_Panel.Children.Clear();
            using (var db = new LibraryContext())
            {
                foreach (var d in db.DetectedObjects)
                {
                    var tb = new TextBlock();
                    tb.Text = $"ClassName: {d.ClassName}, Coords: ({d.X}, {d.Y}, {d.Width}, {d.Height})";
                    tb.TextWrapping = TextWrapping.Wrap;
                    tb.HorizontalAlignment = HorizontalAlignment.Center;
                    tb.Margin = new Thickness(20, 0, 20, 10);
                    DB_Panel.Children.Add(tb);
                }
            }
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
                var th = new Thread(() =>
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ImagesPanel.Children.Clear();
                    }));
                    var curFoundImages = new List<Tuple<string, int[]>>(foundCategories[category]);
                    foreach (var image in curFoundImages)
                    {
                        AddImageToPanel(image.Item1, image.Item2);
                    }
                });
                th.Start();
            }
        }

        private void AddImageToPanel(string imagePath, int[] coords)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                Image image = new Image();
                image.Stretch = Stretch.Fill;
                var cb = CropFromPath(imagePath, coords);
                image.Source = cb;
                ImagesPanel.Children.Add(image);
            }));
        }

        private static CroppedBitmap CropFromPath(string imagePath, int[] coords)
        {
            return new CroppedBitmap(
                new BitmapImage(new Uri(imagePath)),
                new Int32Rect(coords[0], coords[1], coords[2], coords[3]));
        }

        private static byte[] ImageToBytes(CroppedBitmap img)
        {
            byte[] data;
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(img));
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                data = ms.ToArray();
            }
            return data;
        }

        private void UploadToDB(DetectedObject query)
        {
            using (var db = new LibraryContext())
            {
                // Checking if the same image is already in DB
                var same_class = db.DetectedObjects.Where(d => d.ClassName == query.ClassName);
                var same_coords = same_class.Where(c => c.X == query.X
                                                     && c.Y == query.Y
                                                     && c.Width == query.Width
                                                     && c.Height == query.Height);
                var same_thumbs = same_coords.Where(d => d.Details.Image == query.Details.Image);
                foreach (var t in same_thumbs)
                {
                    return;
                }
                db.Add(query);
                db.SaveChanges();
            }
        }
    }
}
