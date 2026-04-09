using System.Windows.Input;
using Shared.Ui.Common.MenuSystem;
using GameWorld.Core.Services;
using Editors.KitbasherEditor.Core.MenuBarViews;

namespace Editors.KitbasherEditor.UiCommands
{
    public class RedoCommand : ITransientKitbasherUiCommand
    {
        public string ToolTip { get; set; } = "Redo";
        public ActionEnabledRule EnabledRule => ActionEnabledRule.Custom;
        public Hotkey? HotKey { get; } = new Hotkey(Key.Z, ModifierKeys.Control | ModifierKeys.Shift);

        private readonly CommandExecutor _commandExecutor;

        public RedoCommand(CommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        public void Execute() => _commandExecutor.Redo();
    }
}
