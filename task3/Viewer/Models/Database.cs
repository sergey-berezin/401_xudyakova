using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NuGetYOLO;



namespace Task3.Models
{
    public class DbImage
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public byte[] Content { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }

        virtual public ICollection<DbItem> Items { get; set; }
    }

    public class DbItem
    {
        public int Id { get; set; }
        public int LabelNum { get; set; }
        public float Conf { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }

    }


    class LibraryContext : DbContext
    {
        public DbSet<DbImage> Images { get; set; }
        public DbSet<DbItem> Items { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseLazyLoadingProxies().UseSqlite("Data Source=library.db");
        public LibraryContext()
        {
            Database.EnsureCreated();
        }
    }
}
