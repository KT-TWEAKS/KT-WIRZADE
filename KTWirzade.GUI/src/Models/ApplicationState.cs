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
            // ProgressPageViewModel.GetNextPage passes state.finishErrorPage to
            // FinishErrorPageViewModel; leaving it null made that state object null
            // on the error path (the success path never hits it because finishPage
            // was the only page pre-created).
            finishErrorPage = new FinishErrorPage();
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