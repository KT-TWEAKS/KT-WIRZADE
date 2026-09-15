using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace KTWirzade.GUI
{
    public interface IDragItem
    {
        [XmlIgnore]
        string FileNameWithoutExtension { get; }

        [XmlIgnore]
        string DisplayUsername { get; set; }

        string Username { get; set; }

        string Name { get; set; }

        string ShortDescription { get; set; }

        Guid? UniqueId { get; set; }

        [XmlIgnore]
        string Description { get; set; }

        [XmlIgnore]
        string Title { get; set; }

        string Version { get; set; }

        string UsbIconUri { get; set; }

        string FilePath { get; set; }

        [XmlIgnore]
        BitmapImage Icon { get; set; }

        [XmlIgnore]
        bool Selected { get; set; }

        [XmlIgnore]
        double FadeOpacity { get; }

        [XmlIgnore]
        int SidebarInitialHeight { get; set; }

        [XmlIgnore]
        bool Checked { get; set; }

        [XmlIgnore]
        Visibility ProgressVisibility { get; set; }

        [XmlIgnore]
        double ProgressValue { get; set; }

        [XmlIgnore]
        bool ItemClickable { get; set; }

        event PropertyChangedEventHandler PropertyChanged;
    }
}
