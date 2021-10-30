using System;
using Microsoft.ML;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using ParallelObjectDetection.DataStructures;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;

namespace ParallelObjectDetection
{
    public class OnnxYoloV4Applier
    {
        string modelPath;
        string[] classesNames;
        public BufferBlock<KeyValuePair<string, YoloV4Result>> foundObjectsBuffer;

        // Model is available here: https://github.com/onnx/models/tree/master/vision/object_detection_segmentation/yolov4
        public OnnxYoloV4Applier(string modelPath = "../../../../ParallelObjectDetection/yolov4.onnx")
        {
            classesNames = PredictionUtils.classesNames;
            this.modelPath = modelPath;
            foundObjectsBuffer = new BufferBlock<KeyValuePair<string, YoloV4Result>>();
        }

        public List<YoloV4Result> ApplyOnImage(string imagePath)
        {
            MLContext mlContext = new MLContext();
            var predictionEngine = PredictionUtils.GeneratePredictionEngine(mlContext, modelPath);
            var predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = BitmapFromPath(imagePath) });
            return (List<YoloV4Result>) predict.GetResults(foundObjectsBuffer, imagePath, classesNames, 0.3f, 0.7f);
        }

        public async Task<Dictionary<string, List<YoloV4Result>>> ApplyOnImagesAsync(List<string> imagePaths)
        {
            foundObjectsBuffer = new BufferBlock<KeyValuePair<string, YoloV4Result>>();
            var result = new Dictionary<string, List<YoloV4Result>>();

            var modelApplier = new ActionBlock<string>(imagePath =>
            {
                var detectedObjects = ApplyOnImage(imagePath);
                result.Add(imagePath, detectedObjects);
            }, new ExecutionDataflowBlockOptions{ MaxDegreeOfParallelism = Environment.ProcessorCount });

            var buffer = new BufferBlock<string>();
            buffer.LinkTo(modelApplier);
            _ = buffer.Completion.ContinueWith(task => modelApplier.Complete());

            Parallel.ForEach(imagePaths, imagePath => buffer.Post(imagePath));
            buffer.Complete();

            await modelApplier.Completion;
            return result;
        }

        private Bitmap BitmapFromPath(string imagePath) => new Bitmap(Image.FromFile(imagePath));
        
    }
}
