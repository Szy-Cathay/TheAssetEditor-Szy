using System.Windows;
using Editors.KitbasherEditor.Core.MenuBarViews;
using Shared.Ui.Common.MenuSystem;

namespace Editors.KitbasherEditor.UiCommands
{
    public class OpenBlenderShortcutsHelpCommand : ITransientKitbasherUiCommand
    {
        public string ToolTip { get; set; } = "Blender操作说明";
        public ActionEnabledRule EnabledRule => ActionEnabledRule.Always;
        public Hotkey? HotKey { get; } = null;

        public void Execute()
        {
            var window = new BlenderShortcutsHelpWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();
        }
    }
}