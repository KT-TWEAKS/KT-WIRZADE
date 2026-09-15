namespace KTWirzade.GUI.Models
{
    public class ApplicationState
    {
        public RequirementsPage activationPage { get; set; }
        public LicensePage licensePage { get; set; }
        public ModePage modePage { get; set; }
        public ProgressPage progressPage { get; set; }
        public FinishPage finishPage { get; set; }
        public FinishErrorPage finishErrorPage { get; set; }
        public ApplicationState()
        {
            activationPage = new RequirementsPage();
            licensePage = new LicensePage();
            modePage = new ModePage();
            progressPage = new ProgressPage();
            finishPage = new FinishPage();
        }
    }

    public class FinishErrorPage
    {
    }

    public class FinishPage
    {
    }
    public class IsoModePage
    {
        public bool IsCustom { get; set; }
    }
    public class LicensePage
    {
        public bool IsLicenseAccepted { get; set; }
    }
    public class ModePage
    {
        public bool IsCustom { get; set; }
    }
    public class ProgressPage
    {
    }
    public class RequirementsPage
    {
    }
    public class IsoLicensePage
    {
        public bool IsLicenseAccepted { get; set; }
    }
    public class IsoRequirementsPage
    {
    }
}