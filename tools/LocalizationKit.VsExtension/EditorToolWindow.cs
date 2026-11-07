using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace LocalizationKit.VsExtension
{
    /// <summary>The window Visual Studio opens: a frame around the editor page.</summary>
    [Guid(WindowGuid)]
    internal sealed class EditorToolWindow : ToolWindowPane
    {
        public const string WindowGuid = "8f2c6a71-8c5d-4d6f-9a23-5b7f4e0c9a10";

        public EditorToolWindow()
        {
            Caption = "LocalizationKit Resw Editor";
            Content = Control;
        }

        /// <summary>Gets the control, so the package can tell it which solution is open.</summary>
        public EditorToolWindowControl Control { get; } = new EditorToolWindowControl();
    }
}
