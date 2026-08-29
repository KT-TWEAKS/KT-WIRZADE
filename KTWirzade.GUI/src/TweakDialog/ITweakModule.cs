using System;

namespace KTWirzade.GUI.TweakDialog
{
    public interface ITweakModule
    {
        event EventHandler Completed;

        void StartOperations();

        void SetLast();

        bool IsUninstallable();
    }
}