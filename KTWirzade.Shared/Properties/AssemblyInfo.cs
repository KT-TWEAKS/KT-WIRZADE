using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KTWirzade.Shared;

[assembly: AssemblyTitle("KTWirzade.Shared")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("KT WIRZADE")]
[assembly: AssemblyProduct("KTWirzade.Shared")]
[assembly: AssemblyCopyright("MIT License - Modified by kelvenapk")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

// Lets the APBX Developer tool classify internal action types produced by the
// shared playbook deserializer (LineInFileAction, ScheduledTaskAction, ...)
// without having to widen them all to public.
[assembly: InternalsVisibleTo("KTWirzade.DevTool")]
[assembly: InternalsVisibleTo("KTWirzade.GUI")]

[assembly: Guid("9bda9d32-e9a1-4db8-9d90-443792107e28")]

[assembly: AssemblyVersion(Globals.CurrentVersion)]
[assembly: AssemblyFileVersion(Globals.CurrentVersion)]
