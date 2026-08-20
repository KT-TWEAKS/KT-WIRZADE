using System;
using System.Runtime.InteropServices;

namespace KTWirzade.GUI
{
    public static class Extensions
    {
        public static Architecture? ToArchitecture(this ImageParsers.ImageArchitecture? arch)
        {
            if (!arch.HasValue)
            {
                return null;
            }
            Architecture value = default(Architecture);
            switch (arch.Value)
            {
                case ImageParsers.ImageArchitecture.x64:
                    value = Architecture.X64;
                    break;
                case ImageParsers.ImageArchitecture.x86:
                    value = Architecture.X86;
                    break;
                case ImageParsers.ImageArchitecture.Arm32:
                    value = Architecture.Arm;
                    break;
                case ImageParsers.ImageArchitecture.Arm64:
                    value = Architecture.Arm64;
                    break;
                default:
                    throw new InvalidOperationException();
            }
            return value;
        }
    }
}