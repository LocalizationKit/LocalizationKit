using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LocalizationKit.VsExtension
{
    /// <summary>The tool window's contents: the editor page, and the file access it cannot do itself.</summary>
    /// <remarks>
    /// <para>
    /// The page is the same file a browser opens. What changes inside Visual Studio is only how files
    /// get in and out: the page asks the host, over the WebView2 message channel it already speaks,
    /// instead of going through a file picker it is not allowed to use.
    /// </para>
    /// <para>
    /// Four messages, and that is the whole contract. The page sends <c>ready</c> when it has loaded,
    /// <c>openFiles</c> when someone asks for different files, and <c>save</c> with the complete text
    /// of a file. The host sends <c>files</c>.
    /// </para>
    /// </remarks>
    internal sealed class EditorToolWindowControl : UserControl
    {
        private readonly WebView2 _web = new WebView2();
        private readonly TextBlock _status = new TextBlock { Margin = new Thickness(8, 4, 8, 4) };
        private readonly Dictionary<string, string> _pathsByCulture =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private string _solutionDirectory;

        public EditorToolWindowControl()
        {
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Visual Studio does not theme a plain WPF control for you. A TextBlock's default
            // foreground is black, the shell paints a dark background behind it in a dark theme, and
            // the result is a status line that is there and cannot be read — which also hides
            // whatever it was reporting. So both colours come from the shell's own brushes.
            _status.TextWrapping = TextWrapping.Wrap;
            _status.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);

            Border strip = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8, 4, 8, 5),
                Child = _status,
            };

            strip.SetResourceReference(Border.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            strip.SetResourceReference(Border.BorderBrushProperty, VsBrushes.ToolWindowBorderKey);
            SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);

            Grid.SetRow(_web, 0);
            Grid.SetRow(strip, 1);
            layout.Children.Add(_web);
            layout.Children.Add(strip);

            Content = layout;
            Say("Starting the editor…");

            _ = InitializeAsync();
        }

        /// <summary>
        /// Says something, in both places it can be read: the status line, and the page itself.
        /// </summary>
        /// <param name="text">What to say.</param>
        /// <remarks>
        /// The page gets a copy because it draws its own colours and is therefore the one place a
        /// message cannot be swallowed by a theme. It is also the place someone is already looking.
        /// </remarks>
        private void Say(string text)
        {
            _status.Text = text;

            if (_web.CoreWebView2 != null)
            {
                _web.CoreWebView2.PostWebMessageAsJson("{\"type\":\"status\",\"text\":" + Quote(text) + "}");
            }
        }

        /// <summary>Tells the window which solution to look in for resource files.</summary>
        /// <param name="solutionDirectory">The solution's folder.</param>
        public void SetSolution(string solutionDirectory)
        {
            _solutionDirectory = solutionDirectory;
        }

        private async Task InitializeAsync()
        {
            try
            {
                // WebView2 keeps its own cache, and a tool window cannot write next to the
                // extension's assembly, so it goes where the user's own data goes.
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LocalizationKit",
                    "WebView2");

                Directory.CreateDirectory(userData);

                CoreWebView2Environment environment =
                    await CoreWebView2Environment.CreateAsync(null, userData).ConfigureAwait(true);

                await _web.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _web.CoreWebView2.WebMessageReceived += OnMessage;

                string folder = PreparePage(userData);

                // A mapped host name rather than a file: URL, so the page runs in a secure context and
                // behaves the way it does when served.
                _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "localizationkit.editor",
                    folder,
                    CoreWebView2HostResourceAccessKind.Allow);

                _web.CoreWebView2.NavigationCompleted += OnNavigated;
                _web.CoreWebView2.Navigate("https://localizationkit.editor/index.html");
                Say("Editor ready.");
            }
            catch (Exception exception) when (exception is IOException
                || exception is WebView2RuntimeNotFoundException
                || exception is UnauthorizedAccessException)
            {
                Say(exception is WebView2RuntimeNotFoundException
                    ? "This window needs the WebView2 runtime, which Visual Studio normally installs. Install it and reopen the window."
                    : "The editor could not start: " + exception.Message);
            }
        }


        /// <summary>Writes the page as a complete HTML document and returns the folder holding it.</summary>
        /// <param name="userData">The folder this window keeps its own files in.</param>
        /// <returns>The folder to map as the virtual host.</returns>
        /// <remarks>
        /// <para>
        /// The page is shared with the browser, where the host that publishes it supplies the document
        /// around it — the doctype, the charset, the viewport. Loaded as a bare file none of that is
        /// there, and both omissions show: without a charset the bytes are read as the system codepage,
        /// so every character above ASCII arrives as mojibake (a middle dot as Â·, an ellipsis as
        /// â€¦), and without a doctype the document renders in quirks mode.
        /// </para>
        /// <para>
        /// So the document is assembled here instead of being added to the shared file, which would
        /// then be wrong for the other host.
        /// </para>
        /// </remarks>
        private static string PreparePage(string userData)
        {
            string source = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "Editor",
                "index.html");

            string folder = Path.Combine(userData, "Page");
            Directory.CreateDirectory(folder);

            string document =
                "<!doctype html>\r\n" +
                "<html lang=\"en\">\r\n" +
                "<head>\r\n" +
                "<meta charset=\"utf-8\">\r\n" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\r\n" +
                "</head>\r\n" +
                "<body>\r\n" +
                File.ReadAllText(source, Encoding.UTF8) +
                "\r\n</body>\r\n</html>\r\n";

            File.WriteAllText(
                Path.Combine(folder, "index.html"),
                document,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return folder;
        }

        /// <summary>Tells the page which theme Visual Studio is in, once it has loaded.</summary>
        /// <param name="sender">The web view.</param>
        /// <param name="args">Whether the navigation succeeded.</param>
        /// <remarks>
        /// The page follows the operating system otherwise, which is the wrong answer inside an IDE
        /// whose theme is its own setting: a dark Visual Studio on a light Windows would hold a white
        /// page. The shell's own window background says which it is, so its brightness decides.
        /// </remarks>
        private void OnNavigated(object sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                Say("The page did not load (" + args.WebErrorStatus + ").");
                return;
            }

            string theme = IsDark() ? "dark" : "light";
            _ = _web.CoreWebView2.ExecuteScriptAsync(
                "document.documentElement.dataset.theme = '" + theme + "'");
        }

        private bool IsDark()
        {
            if (!(TryFindResource(VsBrushes.ToolWindowBackgroundKey) is System.Windows.Media.SolidColorBrush brush))
            {
                return false;
            }

            System.Windows.Media.Color colour = brush.Color;
            double brightness = ((0.299 * colour.R) + (0.587 * colour.G) + (0.114 * colour.B)) / 255.0;

            return brightness < 0.5;
        }

        private void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string json;
            try
            {
                json = args.WebMessageAsJson;
            }
            catch (ArgumentException)
            {
                return;
            }

            string type = Read(json, "type");

            switch (type)
            {
                case "ready":
                case "openFiles":
                    SendFiles();
                    break;

                case "save":
                    Save(Read(json, "culture"), Read(json, "text"));
                    break;
            }
        }

        private void SendFiles()
        {
            IReadOnlyList<ResourceFile> files = ResourceFiles.Find(_solutionDirectory ?? string.Empty);

            if (files.Count == 0)
            {
                Say(_solutionDirectory == null
                    ? "No solution is open, so there are no resource files to find."
                    : "No .resw files under " + _solutionDirectory + ". The window looks for Strings/<culture>/Resources.resw.");
                return;
            }

            _pathsByCulture.Clear();
            StringBuilder json = new StringBuilder("{\"type\":\"files\",\"files\":[");

            for (int i = 0; i < files.Count; i++)
            {
                ResourceFile file = files[i];
                _pathsByCulture[file.Culture] = file.Path;

                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append("{\"name\":").Append(Quote(file.Culture + "/" + file.Name))
                    .Append(",\"path\":").Append(Quote(file.Path.Replace('\\', '/')))
                    .Append(",\"text\":").Append(Quote(file.Text))
                    .Append('}');
            }

            json.Append("]}");

            _web.CoreWebView2.PostWebMessageAsJson(json.ToString());
            Say(string.Format(
                CultureInfo.CurrentCulture,
                "{0} file(s) from {1}: {2}",
                files.Count,
                _solutionDirectory,
                string.Join(", ", files.Select(file => file.Culture))));
        }

        private void Save(string culture, string text)
        {
            if (string.IsNullOrEmpty(culture) || text == null)
            {
                return;
            }

            if (!_pathsByCulture.TryGetValue(culture, out string path))
            {
                Say("Nothing to save " + culture + " into: the window did not open a file for it.");
                return;
            }

            try
            {
                ResourceFiles.Save(path, text);
                Say("Saved " + path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Say("Could not save " + path + ": " + exception.Message);
            }
        }

        /// <summary>
        /// Reads one string property out of a flat JSON object.
        /// </summary>
        /// <param name="json">The message.</param>
        /// <param name="name">The property to read.</param>
        /// <returns>The value, or an empty string.</returns>
        /// <remarks>
        /// Hand-rolled because this project targets .NET Framework, where System.Text.Json is not
        /// there, and the messages are three known shapes rather than arbitrary input.
        /// </remarks>
        private static string Read(string json, string name)
        {
            string marker = "\"" + name + "\":";
            int at = json.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
            {
                return string.Empty;
            }

            int index = at + marker.Length;
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            if (index >= json.Length || json[index] != '"')
            {
                return string.Empty;
            }

            index++;
            StringBuilder value = new StringBuilder();

            while (index < json.Length)
            {
                char current = json[index];

                if (current == '\\' && index + 1 < json.Length)
                {
                    char escaped = json[index + 1];
                    index += 2;

                    switch (escaped)
                    {
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'u':
                            if (index + 4 <= json.Length
                                && int.TryParse(
                                    json.Substring(index, 4),
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture,
                                    out int code))
                            {
                                value.Append((char)code);
                                index += 4;
                            }

                            break;
                        default: value.Append(escaped); break;
                    }

                    continue;
                }

                if (current == '"')
                {
                    break;
                }

                value.Append(current);
                index++;
            }

            return value.ToString();
        }

        private static string Quote(string text)
        {
            StringBuilder quoted = new StringBuilder("\"");

            foreach (char character in text ?? string.Empty)
            {
                switch (character)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (character < ' '
                            || character == '\u2028'
                            || character == '\u2029'
                            || character == '\ufeff')
                        {
                            quoted.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            quoted.Append(character);
                        }

                        break;
                }
            }

            return quoted.Append('"').ToString();
        }
    }
}
