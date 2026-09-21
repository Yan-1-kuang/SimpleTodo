using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace SimpleTodo.Tools
{
    /// <summary>
    /// 生成 assets/app.ico：多尺寸、带透明通道的应用图标。
    /// 用法：make-icon.exe [输出路径]（默认 assets/app.ico）
    ///
    /// 说明：16~128 像素使用 32 位 BMP(DIB) 条目以保证兼容性，256 像素使用 PNG 压缩条目。
    /// 不依赖任何第三方库，直接手工组装 ICO 文件结构。
    /// </summary>
    internal static class MakeIcon
    {
        private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

        private class IconImage
        {
            public int Size;
            public byte[] Data;
        }

        private static int Main(string[] args)
        {
            string output = args.Length > 0 ? args[0] : Path.Combine("assets", "app.ico");

            try
            {
                List<IconImage> images = new List<IconImage>();
                for (int k = 0; k < Sizes.Length; k++)
                {
                    int size = Sizes[k];
                    using (Bitmap bitmap = Render(size))
                    {
                        IconImage image = new IconImage();
                        image.Size = size;
                        image.Data = size >= 256 ? EncodePng(bitmap) : EncodeDib(bitmap);
                        images.Add(image);
                    }
                }

                string directory = Path.GetDirectoryName(Path.GetFullPath(output));
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                WriteIco(output, images);

                FileInfo info = new FileInfo(output);
                Console.WriteLine("已生成 " + info.FullName + "（" +
                    info.Length.ToString(CultureInfo.InvariantCulture) + " 字节，" +
                    images.Count.ToString(CultureInfo.InvariantCulture) + " 个尺寸）");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("生成图标失败：" + ex.Message);
                return 1;
            }
        }

        #region 绘制

        private static Bitmap Render(int size)
        {
            Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float pad = size * 0.05f;
                RectangleF bounds = new RectangleF(pad, pad, size - pad * 2f, size - pad * 2f);
                float radius = size * 0.23f;

                // 圆角矩形 + 蓝紫渐变底
                using (GraphicsPath path = RoundedRectangle(bounds, radius))
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        bounds, Color.FromArgb(255, 79, 107, 255), Color.FromArgb(255, 124, 62, 242), 60f))
                    {
                        g.FillPath(brush, path);
                    }
                }

                // 白色对勾
                float thickness = Math.Max(1.4f, size * 0.115f);
                using (Pen pen = new Pen(Color.White, thickness))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    PointF[] points = new PointF[]
                    {
                        new PointF(size * 0.265f, size * 0.525f),
                        new PointF(size * 0.435f, size * 0.700f),
                        new PointF(size * 0.745f, size * 0.320f)
                    };
                    g.DrawLines(pen, points);
                }
            }

            return bitmap;
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;

            if (diameter <= 0f)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180f, 90f);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270f, 90f);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        #endregion

        #region 编码

        private static byte[] EncodePng(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        /// <summary>编码为 ICO 使用的 32 位 BMP 条目：BITMAPINFOHEADER + 自下而上的 BGRA 像素 + AND 掩码。</summary>
        private static byte[] EncodeDib(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int maskStride = ((width + 31) / 32) * 4;
            int maskSize = maskStride * height;
            int pixelSize = width * height * 4;

            byte[] buffer = new byte[40 + pixelSize + maskSize];
            using (MemoryStream stream = new MemoryStream(buffer))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // BITMAPINFOHEADER
                writer.Write(40);              // biSize
                writer.Write(width);           // biWidth
                writer.Write(height * 2);      // biHeight：颜色位图 + 掩码
                writer.Write((short)1);        // biPlanes
                writer.Write((short)32);       // biBitCount
                writer.Write(0);               // biCompression = BI_RGB
                writer.Write(pixelSize);       // biSizeImage
                writer.Write(0);               // biXPelsPerMeter
                writer.Write(0);               // biYPelsPerMeter
                writer.Write(0);               // biClrUsed
                writer.Write(0);               // biClrImportant

                // 像素数据：自下而上
                for (int y = height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color color = bitmap.GetPixel(x, y);
                        writer.Write(color.B);
                        writer.Write(color.G);
                        writer.Write(color.R);
                        writer.Write(color.A);
                    }
                }

                // AND 掩码：全 0，实际透明度由 alpha 通道决定
                writer.Write(new byte[maskSize]);
            }

            return buffer;
        }

        private static void WriteIco(string path, List<IconImage> images)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // ICONDIR
                writer.Write((short)0);                    // reserved
                writer.Write((short)1);                    // type = icon
                writer.Write((short)images.Count);         // 图像数量

                int offset = 6 + images.Count * 16;

                // ICONDIRENTRY × N
                for (int k = 0; k < images.Count; k++)
                {
                    IconImage image = images[k];
                    writer.Write((byte)(image.Size >= 256 ? 0 : image.Size)); // 宽（256 记为 0）
                    writer.Write((byte)(image.Size >= 256 ? 0 : image.Size)); // 高
                    writer.Write((byte)0);                 // 调色板颜色数
                    writer.Write((byte)0);                 // reserved
                    writer.Write((short)1);                // 色彩平面
                    writer.Write((short)32);               // 位深
                    writer.Write(image.Data.Length);       // 数据长度
                    writer.Write(offset);                  // 数据偏移
                    offset += image.Data.Length;
                }

                for (int k = 0; k < images.Count; k++)
                {
                    writer.Write(images[k].Data);
                }
            }
        }

        #endregion
    }
}
