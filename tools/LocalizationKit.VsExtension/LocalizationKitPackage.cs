using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace LocalizationKit.VsExtension
{
    /// <summary>The extension: one command, which opens the editor on the solution's resource files.</summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuid)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // MDI puts it in the document area, where a window that edits files belongs. Not Tabbed with a
    // Window of its own: that field takes the GUID of the window to dock beside, and a name there is
    // written into the registration as-is for Visual Studio to fail to resolve.
    [ProvideToolWindow(typeof(EditorToolWindow), Style = VsDockStyle.MDI, MultiInstances = false)]
    public sealed class LocalizationKitPackage : AsyncPackage
    {
        public const string PackageGuid = "3a6d1f40-7d1e-4a9c-bb6f-5a1c0f6d2b31";
        public const string CommandSetGuid = "b1f3b0c8-2d0a-4d2e-9c1b-6e5a7d3f8c24";
        public const int OpenEditorCommandId = 0x0100;

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commands)
            {
                commands.AddCommand(new MenuCommand(
                    (sender, args) => _ = OpenAsync(),
                    new CommandID(new Guid(CommandSetGuid), OpenEditorCommandId)));
            }
        }

        private async Task OpenAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            ToolWindowPane window = await ShowToolWindowAsync(
                typeof(EditorToolWindow),
                id: 0,
                create: true,
                cancellationToken: DisposalToken);

            if (window is EditorToolWindow editor)
            {
                editor.Control.SetSolution(SolutionDirectory());

                IVsWindowFrame frame = (IVsWindowFrame)editor.Frame;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
            }
        }

        /// <summary>The folder the open solution lives in, which is where the resource files are looked for.</summary>
        /// <returns>The folder, or null when no solution is open.</returns>
        private string SolutionDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(GetService(typeof(SVsSolution)) is IVsSolution solution))
            {
                return null;
            }

            solution.GetSolutionInfo(out string directory, out string file, out _);

            return !string.IsNullOrEmpty(directory)
                ? directory
                : string.IsNullOrEmpty(file) ? null : Path.GetDirectoryName(file);
        }
    }
}
