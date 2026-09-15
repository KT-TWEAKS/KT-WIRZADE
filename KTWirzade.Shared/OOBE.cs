using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using JetBrains.Annotations;

namespace KTWirzade.Shared
{
    public class OOBESoftware
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool Local { get; set; } = true;
        [CanBeNull] public string IconPath { get; set; }
        public bool? IsDefaultWebBrowser { get; set; }
    }
    [Serializable]
    public class OOBE
    {
        public enum InternetRequirementLevel
        {
            Request,
            Force
        }
        [CanBeNull] public string Username { get; set; }
        [CanBeNull] public string Password { get; set; }
        [CanBeNull] public string AdminPassword { get; set; }
        [CanBeNull] public InternetRequirementLevel? InternetRequirement { get; set; } = null;
        public bool AdminUserEnabled { get; set; } = false;
        public bool AutoLogon { get; set; } = false;
        public string[] Options { get; set; }
        public bool Verified { get; set; }
        public List<BulletPoint> BulletPoints { get; set; } = new List<BulletPoint>();
        public List<OOBESoftware> Software { get; set; } = new List<OOBESoftware>();
    }
}
