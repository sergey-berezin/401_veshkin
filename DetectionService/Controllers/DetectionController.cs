using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YoloV4ObjectDetectorUI;
using ParallelObjectDetection.DataStructures;
using ParallelObjectDetection;
using Microsoft.ML;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;

namespace DetectionService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DetectionController : ControllerBase
    {
        DetectedImagesContext db;

        public DetectionController(DetectedImagesContext db)
        {
            this.db = db;
        }

        [Route("apply-on-image")]
        public List<YoloV4Result> Get(string imagePath, string modelPath)
        {
            MLContext mlContext = new MLContext();
            var predictionEngine = PredictionUtils.GeneratePredictionEngine(mlContext, modelPath);
            var predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = new Bitmap(Image.FromFile(imagePath)) });
            var predictionResult = (List<YoloV4Result>)predict.GetResults(imagePath, PredictionUtils.classesNames, 0.3f, 0.7f);
            foreach (var foundResult in predictionResult)
            {
                var category = foundResult.Label;
                float[] floatCoords = foundResult.BBox;
                int[] coords = {
                    (int) floatCoords[0],
                    (int) floatCoords[1],
                    (int) (floatCoords[2] - floatCoords[0]),
                    (int) (floatCoords[3] - floatCoords[1])
                };

                UploadToDB(new DetectedObject()
                {
                    X = coords[0],
                    Y = coords[1],
                    Width = coords[2],
                    Height = coords[3],
                    ClassName = category,
                    Details = new DetectedObjectDetails() { Image = ImageToBytes(CropFromPath(imagePath, coords)) }
                });
            }
            return predictionResult;
        }

        [Route("get-images")]
        public List<DetectedObject> Get()
        {
            return db.DetectedObjects.ToList();
        }


        [Route("clear-db")]
        public void Delete()
        {
            db.DetectedObjects.RemoveRange(db.DetectedObjects);
            db.DetectedObjectsDetails.RemoveRange(db.DetectedObjectsDetails);
            db.SaveChanges();
        }

        private void UploadToDB(DetectedObject query)
        {
            // Checking if the same image is already in DB
            var same_class = db.DetectedObjects.Where(d => d.ClassName == query.ClassName);
            var same_coords = same_class.Where(c => c.X == query.X
                                                    && c.Y == query.Y
                                                    && c.Width == query.Width
                                                    && c.Height == query.Height);
            var same_thumbs = same_coords.ToArray().Where(d => d.Details.Image.SequenceEqual(query.Details.Image));
            foreach (var t in same_thumbs)
            {
                return;
            }
            db.Add(query);
            db.SaveChanges();
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
    }
}
