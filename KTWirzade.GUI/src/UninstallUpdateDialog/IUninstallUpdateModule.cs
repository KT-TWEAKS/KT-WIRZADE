using System;

namespace KTWirzade.GUI.UninstallUpdateDialog
{
    public interface IUninstallUpdateModule
    {
        event EventHandler Completed;

        void StartOperations();

        void SetLast();

        int Height();
    }
}