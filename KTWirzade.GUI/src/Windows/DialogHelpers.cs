using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KTWirzade.GUI.Windows;

/// <summary>
/// Shared helpers previously copy-pasted verbatim into AntivirusDialog,
/// SecurityDialog, TweaksDialog and UninstallUpdatesDialog.
/// </summary>
internal static class DialogHelpers
{
    public static BitmapSource Convert(Bitmap bitmap)
    {
        using MemoryStream memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0L;
        BitmapImage bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    public static Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
    {
        using MemoryStream outStream = new MemoryStream();
        PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
        pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapImage));
        pngBitmapEncoder.Save(outStream);
        return new Bitmap(new Bitmap(outStream));
    }

    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null)
        {
            yield break;
        }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
            if (child != null && child is T)
            {
                yield return (T)child;
            }
            foreach (T item in FindVisualChildren<T>(child))
            {
                yield return item;
            }
        }
    }
}
