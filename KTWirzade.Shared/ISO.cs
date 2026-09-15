using System;
using JetBrains.Annotations;

namespace KTWirzade.Shared
{
    [Serializable]
    public class ISO
    {
        public string Name { get; set; }
        public string Creator { get; set; }
        public Guid? UniqueId { get; set; } = null;
        
        public string Version { get; set; }
        [CanBeNull] public string WindowsVersion { get; set; }
        [CanBeNull] public string WindowsUpdateVersion { get; set; }
        public string[] Options { get; set; }
        
        public bool HardwareRequirementsDisabled { get; set; } = false;
        public bool BitLockerDisabled { get; set; } = false;
        public bool InternetRequired { get; set; } = false;
    }
}
