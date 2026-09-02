using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GPU_Heater_VS
{
    public static class ApiClient
    {
        private static readonly HttpClient _client = new HttpClient();

        public static async Task<JObject> PostAsync(string endpoint, object payload)
        {
            var options = HeaterPackage.Instance?.GetOptions();
            string baseUrl = options?.ApiBaseUrl ?? "http://127.0.0.1:2004";

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync($"{baseUrl}{endpoint}", content);

            string responseString = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseString);
        }
    }

    public class EditorActions
    {
        public static async void ExecuteAction(string actionType, DTE dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var doc = dte.ActiveDocument;
            var selection = (TextSelection)doc.Selection;
            string selectedText = selection.Text;

            if (string.IsNullOrEmpty(selectedText) && actionType != "fixError")
                selectedText = selection.Parent.StartPoint.CreateEditPoint().GetText(selection.Parent.EndPoint);

            string endpoint = actionType == "fixError" ? "/api/coder/ide/fix-error" : "/api/coder/ide/inline-action";

            object payload;
            if (actionType == "fixError")
            {
                string errorText = Microsoft.VisualBasic.Interaction.InputBox("Enter Error Log:", "Debug", "");
                payload = new
                {
                    use_knowledge_base = true,
                    workspace_path = Path.GetDirectoryName(dte.Solution.FullName),
                    error = errorText,
                    file_content = selection.Parent.StartPoint.CreateEditPoint().GetText(selection.Parent.EndPoint),
                    current_file = doc.FullName
                };
            }
            else
            {
                payload = new
                {
                    use_knowledge_base = true,
                    workspace_path = Path.GetDirectoryName(dte.Solution.FullName),
                    selected_code = selectedText,
                    action_type = actionType,
                    language = doc.Language
                };
            }

            try
            {
                var response = await ApiClient.PostAsync(endpoint, payload);
                string result = (string)(response["fix"] ?? response["result"] ?? "");

                if (actionType == "explain")
                {
                    string escapedResult = JsonConvert.SerializeObject(result);
                    string js = $"window.postMessage({{ type: 'addBotResponse', title: 'Explanation for Selected Code', content: {escapedResult} }}, '*');";
                    HeaterToolWindowControl.Instance?.ExecuteWebScriptAsync(js);
                }
                else
                {
                    MessageBox.Show(result, "Heater AI Result");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server Unreachable: {ex.Message}", "Error");
            }
        }
    }

    public class ProjectActions
    {
        public static async void InspectFolder(DTE dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            string folderPath = Path.GetDirectoryName(dte.Solution.FullName);

            if (string.IsNullOrEmpty(folderPath))
            {
                MessageBox.Show("No folder found to inspect.", "Warning");
                return;
            }

            var payload = new { path = folderPath };
            var response = await ApiClient.PostAsync("/api/coder/inspect", payload);

            if (response["success"] != null && (bool)response["success"])
            {
                string data = (string)response["data"];
                dte.ItemOperations.NewFile("General\\Text File", "FolderAnalysis.md", Constants.vsViewKindTextView);
                var selection = (TextSelection)dte.ActiveDocument.Selection;
                selection.Insert($"# Folder Analysis ({folderPath})\n\n{data}");
            }
            else
            {
                MessageBox.Show($"Error: {(string)response["error"]}", "Inspection Failed");
            }
        }

        public static async void TrainSnippet(DTE dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var doc = dte.ActiveDocument;
            var selection = (TextSelection)doc.Selection;
            string content = selection.Text;

            if (string.IsNullOrWhiteSpace(content))
            {
                content = selection.Parent.StartPoint.CreateEditPoint().GetText(selection.Parent.EndPoint);
            }

            var payload = new
            {
                content = content,
                filename = doc.Name
            };

            var response = await ApiClient.PostAsync("/api/coder/ide/train-snippet", payload);

            if (response["success"] != null && (bool)response["success"])
            {
                MessageBox.Show($"✅ {(string)response["message"]}", "Training Successful");
            }
        }
    }

    public class AutoCompleteService
    {
        public async Task<string> FetchGhostTextAsync(string documentText, int cursorPosition)
        {
            var options = HeaterPackage.Instance?.GetOptions();
            if (options != null && !options.AutoCompleteEnabled) return null;

            int maxPrefix = Math.Max(0, cursorPosition - 1000);
            int maxSuffix = Math.Min(documentText.Length - cursorPosition, 1000);

            string prefix = documentText.Substring(maxPrefix, cursorPosition - maxPrefix);
            string suffix = documentText.Substring(cursorPosition, maxSuffix);

            var payload = new { prefix, suffix };

            try
            {
                var response = await ApiClient.PostAsync("/api/coder/ide/fim-complete", payload);
                if (response["success"] != null && (bool)response["success"])
                {
                    return ((string)response["completion"])?.Trim();
                }
            }
            catch { }

            return null;
        }
    }

    public partial class HeaterToolWindowControl : UserControl
    {
        public static HeaterToolWindowControl Instance { get; private set; }

        public HeaterToolWindowControl()
        {
            InitializeComponent();
            Instance = this;
            _ = InitializeWebViewAsync();
        }

        public async void ExecuteWebScriptAsync(string script)
        {
            if (webView?.CoreWebView2 != null)
            {
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        private async System.Threading.Tasks.Task InitializeWebViewAsync()
        {
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GPU_Heater_VS",
                    "WebView2Data"
                );
                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(environment);

                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                string finalHtml = BuildHtmlContent();
                webView.NavigateToString(finalHtml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GPU-Heater WebView Init Error: {ex}");
            }
        }

        private string BuildHtmlContent()
        {
            string htmlContent = ReadResourceText("ui.html");
            if (string.IsNullOrEmpty(htmlContent)) return "<html><body style='color:white; background:#1e1e1e;'><h2>UI dosyasi bulunamadi.</h2></body></html>";

            var options = HeaterPackage.Instance?.GetOptions();
            string apiBase = options?.ApiBaseUrl ?? "http://127.0.0.1:2004";

            string langJson = ReadResourceText("lang.json");
            if (string.IsNullOrEmpty(langJson)) langJson = "{}";

            string markedJs = ReadResourceText("marked.min.js");
            string codiconCss = ReadResourceText("codicon.min.css");
            if (string.IsNullOrEmpty(codiconCss)) codiconCss = ReadResourceText("codicon.css");

            byte[] fontBytes = ReadResourceBytes("codicon.ttf");
            if (fontBytes != null && fontBytes.Length > 0)
            {
                string fontBase64 = Convert.ToBase64String(fontBytes);
                string embeddedFontFace = $@"
                    @font-face {{
                        font-family: 'codicon';
                        src: url('data:font/truetype;charset=utf-8;base64,{fontBase64}') format('truetype');
                        font-weight: normal;
                        font-style: normal;
                    }}";

                codiconCss = Regex.Replace(codiconCss, @"@font-face\s*\{[^}]*\}", "");
                codiconCss = $"{embeddedFontFace}\n{codiconCss}";
            }

            string injectCode = $@"
                window.vscode = {{
                    postMessage: function(msg) {{
                        window.chrome.webview.postMessage(msg);
                    }}
                }};
                window.acquireVsCodeApi = function() {{ return window.vscode; }};
                const API_BASE = '{apiBase}';
                window.i18nData = {langJson};
            ";

            string injectedHead = $@"
                <script>{injectCode}</script>
                <style>{codiconCss}</style>
                <script>{markedJs}</script>
            ";

            htmlContent = Regex.Replace(htmlContent, @"<link[^>]*href=[""'][^""']*codicon[^""']*[""'][^>]*>", "");
            htmlContent = Regex.Replace(htmlContent, @"<script[^>]*src=[""'][^""']*marked[^""']*[""'][^>]*></script>", "");

            return htmlContent.Replace("<head>", "<head>" + injectedHead);
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string jsonString = e.WebMessageAsJson;
                var data = JObject.Parse(jsonString);
                string msgType = (string)data["type"] ?? "";

                if (msgType == "getChatContext")
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        var dte = (DTE)Package.GetGlobalService(typeof(DTE));
                        string workspacePath = dte?.Solution?.FullName != null ? Path.GetDirectoryName(dte.Solution.FullName) : "";
                        string activeFile = dte?.ActiveDocument?.FullName ?? "";

                        string activeFileContent = "";
                        if (dte?.ActiveDocument != null)
                        {
                            var selection = (TextSelection)dte.ActiveDocument.Selection;
                            activeFileContent = selection.Parent.StartPoint.CreateEditPoint().GetText(selection.Parent.EndPoint);
                        }

                        var response = new
                        {
                            type = "chatContextResponse",
                            workspacePath = workspacePath,
                            activeFile = activeFile,
                            activeFileContent = activeFileContent
                        };

                        webView.CoreWebView2.PostWebMessageAsJson(JsonConvert.SerializeObject(response));
                    });
                }
                else if (msgType == "toggleAutoComplete")
                {
                    bool isEnabled = (bool)(data["value"] ?? false);
                    var options = HeaterPackage.Instance?.GetOptions();
                    if (options != null)
                    {
                        options.AutoCompleteEnabled = isEnabled;
                        options.SaveSettingsToStorage();
                    }
                }
            }
            catch { }
        }

        private string ReadResourceText(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return "";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private byte[] ReadResourceBytes(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return null;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}