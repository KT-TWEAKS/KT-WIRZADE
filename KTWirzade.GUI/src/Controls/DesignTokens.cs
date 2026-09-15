using System;

namespace KTWirzade.GUI.Controls
{
    public static class DesignTokens
    {
        public static class Spacing
        {
            public const double XS = 4;
            public const double SM = 8;
            public const double MD = 12;
            public const double LG = 16;
            public const double XL = 24;
            public const double XXL = 32;
        }

        public static class Radius
        {
            public const double SM = 4;
            public const double MD = 6;
            public const double LG = 8;
            public const double XL = 12;
            public const double Pill = 999;
        }

        public static class Size
        {
            public const double ButtonHeight = 32;
            public const double ButtonHeightSmall = 24;
            public const double ButtonHeightLarge = 40;
            public const double InputHeight = 36;
            public const double IconSM = 12;
            public const double IconMD = 16;
            public const double IconLG = 24;
            public const double IconXL = 32;
            public const double AvatarSM = 32;
            public const double AvatarMD = 56;
            public const double AvatarLG = 108;
            public const double TitleBar = 46;
            public const double BottomBar = 48;
        }

        public static class Duration
        {
            public const int Fast = 100;
            public const int Normal = 200;
            public const int Slow = 300;
            public const int Page = 400;
        }

        public static class Opacity
        {
            public const double Subtle = 0.4;
            public const double Muted = 0.6;
            public const double Strong = 0.8;
            public const double Full = 1.0;
        }

        public static class Elevation
        {
            public const double Card = 0.06;
            public const double Modal = 0.12;
            public const double Tooltip = 0.16;
        }

        public static class Color
        {
            public const string Accent = "#0096c7";
            public const string AccentDark = "#00b4d8";
            public const string AccentSubtle = "#200096c7";
            public const string Success = "#3da35a";
            public const string SuccessSubtle = "#203da35a";
            public const string Warning = "#e6a917";
            public const string WarningSubtle = "#20e6a917";
            public const string Error = "#c32b1d";
            public const string ErrorSubtle = "#20c32b1d";
            public const string Info = "#71d4db";
            public const string InfoSubtle = "#2071d4db";
            public const string TextPrimary = "#d5d6dd";
            public const string TextSecondary = "#909090";
            public const string BackgroundPrimary = "#1f1f20";
            public const string BackgroundCard = "#1d1e20";
            public const string BackgroundSubtle = "#31353a";
            public const string Border = "#1c1d1e";
        }
    }
}
