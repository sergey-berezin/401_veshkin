using System;
using ParallelObjectDetection;
using ParallelObjectDetection.DataStructures;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Collections.Generic;

namespace YoloV4ObjectDetector
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Please, place \"yolo4.onnx\" to \"ParallelObjectDetection/\" " +
                "and press \"Enter\" or enter path to ONNX model: ");
            string modelPath = "../../../../ParallelObjectDetection/yolov4.onnx";
            var userInput = Console.ReadLine();
            if (userInput != "") modelPath = userInput;

            Console.Write("Enter search folder: ");
            var searchFolder = Console.ReadLine();
            var filters = new string[] { "jpg", "jpeg", "png", "gif", "tiff", "bmp", "svg" };
            var imagePaths = PredictionUtils.GetFilesFrom(searchFolder, filters);

            bool foundAllObjects = false;

            var modelApplier = new OnnxYoloV4Applier(modelPath);

            var recieveResults = Task.Factory.StartNew(() =>
            {
                while (!foundAllObjects)
                {
                    while (modelApplier.foundObjectsBuffer.TryReceive(out var value))
                    {
                        Console.WriteLine($"Found {value.Value.Label} on {value.Key}");
                    }
                }
            }, TaskCreationOptions.LongRunning);

            await modelApplier.ApplyOnImagesAsync(imagePaths);
            foundAllObjects = true;
        }
    }
}
