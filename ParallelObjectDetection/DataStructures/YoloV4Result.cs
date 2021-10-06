namespace ParallelObjectDetection.DataStructures
{
    public class YoloV4Result
    {
        /// <summary>
        /// x1, y1, x2, y2 in page coordinates.
        /// <para>left, top, right, bottom.</para>
        /// </summary>
        public float[] BBox { get; }

        /// <summary>
        /// The Bbox category.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Confidence level.
        /// </summary>
        public float Confidence { get; }

        public YoloV4Result(float[] bbox, string label, float confidence)
        {
            BBox = bbox;
            Label = label;
            Confidence = confidence;
        }

        public override string ToString() =>
            $"Label: {Label}, Confidence: {Confidence}, " +
            $"BBox: [x1: {BBox[0]}, y1: {BBox[1]}, x2: {BBox[2]}, y2: {BBox[3]}]";
    }
}
