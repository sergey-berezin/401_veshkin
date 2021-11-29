using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace YoloV4ObjectDetectorUI
{
    public class DetectedObject
    {
        [Key]
        public int ObjectId { get; set; }
        public string ClassName { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        virtual public DetectedObjectDetails Details { get; set; }
    }

    public class DetectedObjectDetails
    {
        [Key]
        public int ID { get; set; }
        public byte[] Image { get; set; }
    }

    class LibraryContext: DbContext
    {
        public DbSet<DetectedObject> DetectedObjects { get; set; }
        public DbSet<DetectedObjectDetails> DetectedObjectsDetails { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseLazyLoadingProxies().UseSqlite("Data Source=detected_objects.db");
    }
}
