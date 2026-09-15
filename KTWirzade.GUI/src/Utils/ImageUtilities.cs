using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace KTWirzade.GUI.Utils
{
    public static class ImageUtilities
    {
        private static Dictionary<string, ImageCodecInfo> encoders = null;

        private static object encodersLock = new object();

        public static Dictionary<string, ImageCodecInfo> Encoders
        {
            get
            {
                if (encoders == null)
                {
                    lock (encodersLock)
                    {
                        if (encoders == null)
                        {
                            encoders = new Dictionary<string, ImageCodecInfo>();
                            ImageCodecInfo[] imageEncoders = ImageCodecInfo.GetImageEncoders();
                            foreach (ImageCodecInfo codec in imageEncoders)
                            {
                                encoders.Add(codec.MimeType.ToLower(), codec);
                            }
                        }
                    }
                }
                return encoders;
            }
        }

        public static Bitmap ResizeImage(Image image, int width, int height, bool useAttributes = true)
        {
            Bitmap result = new Bitmap(width, height);
            result.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                if (useAttributes)
                {
                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    Rectangle rect = new Rectangle(0, 0, result.Width, result.Height);
                    graphics.DrawImage(image, rect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                }
                else
                {
                    graphics.DrawImage(image, 0, 0, result.Width, result.Height);
                }
            }
            return result;
        }
    }
}
