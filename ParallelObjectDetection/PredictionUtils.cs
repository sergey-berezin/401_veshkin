using Microsoft.ML;
using System.IO;
using System.Collections.Generic;
using ParallelObjectDetection.DataStructures;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;

namespace ParallelObjectDetection
{
    public class PredictionUtils
    {
        public static List<string> GetFilesFrom(string searchFolder, string[] filters, bool isRecursive = false)
        {
            List<string> filesFound = new List<string>();
            var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var filter in filters)
            {
                filesFound.AddRange(Directory.GetFiles(searchFolder, $"*.{filter}", searchOption));
            }
            return filesFound;
        }

        public static readonly string[] classesNames = { "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant",
            "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie",
            "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass",
            "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant",
            "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock",
            "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };


        
        public static PredictionEngine<YoloV4BitmapData, YoloV4Prediction> GeneratePredictionEngine(MLContext mlContext, string modelPath)
        {
            var pipeline =  mlContext.Transforms.ResizeImages(inputColumnName: "bitmap", outputColumnName: "input_1:0", imageWidth: 416, imageHeight: 416, resizing: ResizingKind.IsoPad)
                            .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input_1:0", scaleImage: 1f / 255f, interleavePixelColors: true))
                            .Append(mlContext.Transforms.ApplyOnnxModel(
                                shapeDictionary: new Dictionary<string, int[]>()
                                {
                                    { "input_1:0", new[] { 1, 416, 416, 3 } },
                                    { "Identity:0", new[] { 1, 52, 52, 3, 85 } },
                                    { "Identity_1:0", new[] { 1, 26, 26, 3, 85 } },
                                    { "Identity_2:0", new[] { 1, 13, 13, 3, 85 } },
                                },
                                inputColumnNames: new[]
                                {
                                    "input_1:0"
                                },
                                outputColumnNames: new[]
                                {
                                    "Identity:0",
                                    "Identity_1:0",
                                    "Identity_2:0"
                                },
                                modelFile: modelPath, recursionLimit: 100));

            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV4BitmapData>()));
            return mlContext.Model.CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model);
        }
        
    }
}
