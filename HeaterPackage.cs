using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace GPU_Heater_VS
{
    public class HeaterOptions : DialogPage
    {
        [Category("GPU-Heater Settings")]
        [DisplayName("Backend API Base URL")]
        [Description("GPU-Heater Server Address (e.g., http://127.0.0.1:2004)")]
        public string ApiBaseUrl { get; set; } = "http://127.0.0.1:2004";

        [Category("GPU-Heater Settings")]
        [DisplayName("Language")]
        [Description("UI Language (e.g., en, tr)")]
        public string Language { get; set; } = "en";

        [Category("GPU-Heater Settings")]
        [DisplayName("Enable Auto-Complete")]
        [Description("Enable ghost text suggestions.")]
        public bool AutoCompleteEnabled { get; set; } = true;
    }

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(HeaterToolWindow), Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindSolutionExplorer)]
    [ProvideOptionPage(typeof(HeaterOptions), "GPU-Heater", "General", 0, 0, true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideBindingPath]
    public sealed class HeaterPackage : AsyncPackage
    {
        public const string PackageGuidString = "107712fe-e529-4166-951a-084f5f1102ae";
        public static readonly Guid CommandSetGuid = new Guid("b46a3264-1043-46f9-b855-9c7129b2e4a1");

        public const int cmdidHeaterToolWindow = 0x0100;
        public const int cmdidFixError = 0x0101;
        public const int cmdidExplainCode = 0x0102;
        public const int cmdidGenerateTests = 0x0103;
        public const int cmdidRefactor = 0x0104;
        public const int cmdidTrainSnippet = 0x0105;
        public const int cmdidInspectFolder = 0x0106;

        public static HeaterPackage Instance { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Instance = this;
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                RegisterCommand(commandService, cmdidHeaterToolWindow, ShowToolWindow);
                RegisterCommand(commandService, cmdidFixError, (s, e) => ExecuteEditorAction("fixError"));
                RegisterCommand(commandService, cmdidExplainCode, (s, e) => ExecuteEditorAction("explain"));
                RegisterCommand(commandService, cmdidGenerateTests, (s, e) => ExecuteEditorAction("tests"));
                RegisterCommand(commandService, cmdidRefactor, (s, e) => ExecuteEditorAction("refactor"));
                RegisterCommand(commandService, cmdidTrainSnippet, (s, e) => ExecuteTrainSnippet());
                RegisterCommand(commandService, cmdidInspectFolder, (s, e) => ExecuteInspectFolder());
            }
        }

        private void RegisterCommand(OleMenuCommandService commandService, int commandId, EventHandler handler)
        {
            var menuCommandID = new CommandID(CommandSetGuid, commandId);
            var menuItem = new OleMenuCommand(handler, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        private void ShowToolWindow(object sender, EventArgs e)
        {
            this.JoinableTaskFactory.Run(async delegate
            {
                await this.JoinableTaskFactory.SwitchToMainThreadAsync();

                ToolWindowPane window = this.FindToolWindow(typeof(HeaterToolWindow), 0, true);
                if (window?.Frame == null)
                {
                    throw new NotSupportedException("ToolWindow olusturulamadi.");
                }

                IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
                ErrorHandler.ThrowOnFailure(windowFrame.Show());
            });
        }

        private void ExecuteEditorAction(string actionType)
        {
            this.JoinableTaskFactory.Run(async delegate
            {
                await this.JoinableTaskFactory.SwitchToMainThreadAsync();
                var dte = (DTE)await GetServiceAsync(typeof(DTE));
                if (dte != null)
                {
                    EditorActions.ExecuteAction(actionType, dte);
                }
            });
        }

        private void ExecuteTrainSnippet()
        {
            this.JoinableTaskFactory.Run(async delegate
            {
                await this.JoinableTaskFactory.SwitchToMainThreadAsync();
                var dte = (DTE)await GetServiceAsync(typeof(DTE));
                if (dte != null)
                {
                    ProjectActions.TrainSnippet(dte);
                }
            });
        }

        private void ExecuteInspectFolder()
        {
            this.JoinableTaskFactory.Run(async delegate
            {
                await this.JoinableTaskFactory.SwitchToMainThreadAsync();
                var dte = (DTE)await GetServiceAsync(typeof(DTE));
                if (dte != null)
                {
                    ProjectActions.InspectFolder(dte);
                }
            });
        }

        public HeaterOptions GetOptions()
        {
            return (HeaterOptions)GetDialogPage(typeof(HeaterOptions));
        }
    }
}