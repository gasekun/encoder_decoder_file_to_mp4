// See https://aka.ms/new-console-template for more information

using System.Drawing;
using System.Drawing.Imaging;
using FFMediaToolkit;
using FFMediaToolkit.Decoding;
using FFMediaToolkit.Encoding;
using FFMediaToolkit.Graphics;
using FFmpeg.AutoGen;

namespace test_ffmpeg;

internal static class Program
{
    public static unsafe Bitmap ToBitmap(this ImageData bitmap)
    {
        fixed (byte* p = bitmap.Data)
        {
            return new Bitmap(bitmap.ImageSize.Width, bitmap.ImageSize.Height, bitmap.Stride,
                PixelFormat.Format24bppRgb, new nint(p));
        }
    }

    public static void Main(string[] args)
    {
        FFmpegLoader.FFmpegPath = Environment.CurrentDirectory + "/ffmpeg/";
        Console.Write("Pixel size:");
        var pixelSize = Console.ReadLine();
        Console.WriteLine("1 - encode file, 2 - decode mp4");
        var input = Console.ReadLine();
        switch (input)
        {
            case "1":
                CryptFile(int.Parse(pixelSize));
                break;
            case "2":
                EncryptyFile(int.Parse(pixelSize));
                break;
        }
    }

    private static void EncryptyFile(int pixelSize)
    {
        var folder_images = Environment.CurrentDirectory + "/images_crypt/";

        if (Directory.Exists(folder_images))
            Directory.Delete(folder_images, true);
        Directory.CreateDirectory(folder_images);

        var fileName = "image";

        var pixel_size = pixelSize;
        var Height = 720;
        var Width = 1280;

        var i = 1;
        Console.Write("File name:");
        var input_mp4 = Console.ReadLine();
        var file = MediaFile.Open(Environment.CurrentDirectory + $"/{input_mp4}");
        var extract_frame = true;
        
        Task.Factory.StartNew(() =>
        {
            var dots = "";
            Console.Clear();
            while (extract_frame)
            {
                dots += '.';
                Console.WriteLine($"Extract frame {dots,4}");
                Console.WriteLine($"{i}/{file.Video.Info.NumberOfFrames}");
                if (dots.Length > 3)
                    dots = "";
                Thread.Sleep(100);
                Console.CursorLeft = 0;
                Console.CursorTop = 0;
            }
        
            Console.Clear();
        });

        // var count_cpu = Environment.ProcessorCount;
        //
        // var count_images_for_thread = Math.Round(((double)file.Video.Info.NumberOfFrames / (double)count_cpu),
        //     MidpointRounding.ToEven);
        //
        // var list_thread = new List<Thread>();
        // var taskfactory = new TaskFactory();
        // for (int j = 0; j < count_cpu; j++)
        // {
        //    
        //
        //     var thread = new Thread((j) =>
        //     {
        //         var num = (int)j;
        //         Console.WriteLine($"start task {j}");
        //         for (int k = 1; k <= count_images_for_thread; k++)
        //         {
        //             lock (file)
        //             {
        //                 var time = new TimeSpan((long)(count_images_for_thread * num + k * 1000000000 / file.Video.Info.AvgFrameRate));
        //                 if (file.Video.TryGetFrame(time, out var imageData))
        //                 {
        //                     imageData.ToBitmap().Save(folder_images + fileName + '_' +
        //                                               (count_images_for_thread * num + k) +
        //                                               "_.png");
        //                 }
        //             }
        //         }
        //     });
        //     thread.Start(j);
        //     list_thread.Add(thread);
        // }

        while (file.Video.TryGetNextFrame(out var imageData))
        {
            imageData.ToBitmap().Save(folder_images + fileName + '_' + i + "_.png");
            i++;
            GC.Collect();
        }

        extract_frame = false;
        ImagesToFile(folder_images, Width, pixel_size, Height, "out");
    }

    private static void CryptFile(int pixelSize)
    {
        var folder_images = Environment.CurrentDirectory + "/images_crypt/";


        var fileName = "image";

        var pixel_size = pixelSize;
        var Height = 720;
        var Width = 1280;

        if (Directory.Exists(folder_images))
            Directory.Delete(folder_images, true);
        Directory.CreateDirectory(folder_images);

        Console.Write("Path File:");
        var path_file = Console.ReadLine();

        var bytes = File.ReadAllBytes(path_file);
        Console.WriteLine($"File size in bytes: {bytes.Length}");


        FileToImages(Width, Height, bytes, pixel_size, folder_images, fileName);


        var images = Directory.GetFiles(folder_images);
        Array.Sort(images, (s1, s2) =>
        {
            var num1 = int.Parse(Path.GetFileName(s1).Split('_')[1]);
            var num2 = int.Parse(Path.GetFileName(s2).Split('_')[1]);
            return num1 < num2 ? -1 : num1 > num2 ? 1 : 0;
        });
        var const_length_images = images.Length;
        var settings = new VideoEncoderSettings(Width, Height, 24, VideoCodec.H264);
        settings.EncoderPreset = EncoderPreset.Fast;
        settings.CRF = 17;
        // Directory.CreateDirectory(Environment.CurrentDirectory + "/temp_video_part/");
        // for (int i = 0; i < 10; i++)
        // {
        //     var images_part = images.Take(const_length_images / 10).ToList();
        //     if (i < 9)
        //     {
        //         var temp_list = images.ToList();
        //         temp_list.RemoveRange(0, const_length_images / 10);
        //         images = temp_list.ToArray();
        //     }


        Directory.CreateDirectory(Environment.CurrentDirectory + "/out_video/");
        var file = MediaBuilder
            .CreateContainer(Environment.CurrentDirectory + "/out_video/out.mp4")
            .WithVideo(settings).Create();

        var count_img = 0;

        Task.Factory.StartNew(() =>
        {
            Console.Clear();
            var count_all_img = bytes.Length / ((Height / pixel_size) * Width / (pixel_size * 8));
            while (count_img <= count_all_img)
            {
                Console.WriteLine($"Image to mp4: {Math.Round(count_img / (float)count_all_img * 100, 2),8}%");
                Thread.Sleep(100);
                Console.CursorLeft = 0;
                Console.CursorTop = 0;
            }

            Console.Clear();
        });

        foreach (var image in images)
        {
            var bitmap = new Bitmap(Image.FromFile(image));
            var rect = new Rectangle(Point.Empty, bitmap.Size);
            var bitLock = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var bitmapData = ImageData.FromPointer(bitLock.Scan0, ImagePixelFormat.Rgba32, bitmap.Size);
            file.Video.AddFrame(bitmapData);
            count_img++;
            bitmap.Dispose();
            GC.Collect();
        }

        var bitmap2 = new Bitmap(Width, Height);
        var rect2 = new Rectangle(Point.Empty, bitmap2.Size);
        var bitLock2 = bitmap2.LockBits(rect2, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var bitmapData2 = ImageData.FromPointer(bitLock2.Scan0, ImagePixelFormat.Rgba32, bitmap2.Size);

        file.Video.AddFrame(bitmapData2);
        file.Dispose();
        GC.Collect();
        //Thread.Sleep(1000);
        // }

        Console.WriteLine("Finished!!");
    }

    private static void ImagesToFile(string folder_images, int Width, int pixel_size, int Height, string path_save)
    {
        var images = Directory.GetFiles(folder_images);
        Array.Sort(images, (s1, s2) =>
        {
            var num1 = int.Parse(Path.GetFileName(s1).Split('_')[1]);
            var num2 = int.Parse(Path.GetFileName(s2).Split('_')[1]);
            return num1 < num2 ? -1 : num1 > num2 ? 1 : 0;
        });
        var file_bytes = new List<byte>();
        var count_img = 0;
        var convert_do = true;

        Task.Factory.StartNew(() =>
        {
            Console.Clear();
            while (count_img < images.Length && convert_do)
            {
                Console.WriteLine($"Images to file: {Math.Round(count_img / (float)images.Length * 100, 2),8}%");
                Thread.Sleep(100);
                Console.CursorLeft = 0;
                Console.CursorTop = 0;
            }

            Console.Clear();
        });


        foreach (var path_image in images)
        {
            var bitmap = new Bitmap(Image.FromFile(path_image));
            count_img++;
            for (int i = 0; i < Width / (pixel_size * 8); i++)
            for (var j = 0; j < Height / pixel_size; j++)
            {
                var number = "";
                for (var k = 0; k < 8; k++)
                {
                    var colorOnImage = bitmap.GetPixel(i * pixel_size * 8 + pixel_size * k + pixel_size / 2,
                        j * pixel_size + pixel_size / 2);
                    switch (colorOnImage)
                    {
                        case { R: < 100, G: < 100, B: < 100 }:
                            number += 0;
                            break;
                        case { R: > 150, G: > 150, B: > 150 }:
                            number += 1;
                            break;
                        case { R: < 100, G: < 100, B: > 150 }:
                            goto save_file;
                        default:
                            Console.WriteLine($"Wrong Color: {colorOnImage.ToString()}," +
                                              $" Pixel: [{j * pixel_size * 8 + pixel_size * k}," +
                                              $"{i * pixel_size}], File name: {Path.GetFileName(path_image)}");
                            break;
                    }
                }

                file_bytes.Add((byte)Convert.ToInt32(number, 2));
            }

            GC.Collect();
        }

        save_file:
        convert_do = false;
        Thread.Sleep(200);
        Console.Write("Enter ext:");
        var ext = Console.ReadLine();
        File.WriteAllBytes($"{path_save}.{ext}", file_bytes.ToArray());
        Console.WriteLine($"Finished!! Out File {file_bytes.Count} bytes");
    }

    private static void FileToImages(int Width, int Height, byte[] bytes, int pixel_size, string folder_images,
        string fileName)
    {
        var row = 0;
        var col = 0;
        var count_img = 1;

        Task.Factory.StartNew(() =>
        {
            Console.Clear();
            var count_all_img = bytes.Length / ((Height / pixel_size) * Width / (pixel_size * 8));
            while (count_img <= count_all_img)
            {
                Console.WriteLine($"File to image: {Math.Round(count_img / (float)count_all_img * 100, 2),8}%");
                Thread.Sleep(100);
                Console.CursorLeft = 0;
                Console.CursorTop = 0;
            }

            Console.Clear();
        });

        var rectengles = new List<Rectangle>();

        var brush_w = new SolidBrush(Color.White);
        var image = new Bitmap(Width, Height);
        var g = Graphics.FromImage(image);
        g.Clear(Color.Black);

        foreach (var t in bytes)
        {
            if (row == image.Height / pixel_size)
            {
                row = 0;
                col++;
            }

            if (col == image.Width / (pixel_size * 8))
            {
                g.FillRectangles(brush_w, rectengles.ToArray());
                g.Dispose();
                image.Save(folder_images + fileName + '_' + count_img + "_.png", ImageFormat.Png);
                count_img++;

                image = new Bitmap(Width, Height);
                g = Graphics.FromImage(image);
                g.Clear(Color.Black);
                rectengles.Clear();
                GC.Collect();
                col = 0;
                row = 0;
            }

            var number = Helper.ToBinary(t);
            for (var j = 0; j < 8; j++)
                if (number[j] == '1')
                {
                    rectengles.Add(new Rectangle(col * pixel_size * 8 + j * pixel_size,
                        row * pixel_size,
                        pixel_size, pixel_size));
                }

            row++;
        }

        g.FillRectangles(brush_w, rectengles.ToArray());
        brush_w = new SolidBrush(Color.Red);
        for (var j = 0; j < 8; j++)
            g.FillRectangle(brush_w, col * pixel_size * 8 + j * pixel_size, row * pixel_size, pixel_size, pixel_size);


        g.Dispose();

        image.Save(folder_images + fileName + '_' + count_img + "_.png", ImageFormat.Png);
    }
}

internal class Helper
{
    public static string ToBinary(long Decimal)
    {
        long BinaryHolder;
        string[] BinaryResult = { "0", "0", "0", "0", "0", "0", "0", "0" };
        var i = 0;

        while (Decimal > 0)
        {
            BinaryHolder = Decimal % 2;
            BinaryResult[i++] = BinaryHolder.ToString();
            Decimal = Decimal / 2;
        }

        Array.Reverse(BinaryResult);
        return string.Concat(BinaryResult);
    }
}