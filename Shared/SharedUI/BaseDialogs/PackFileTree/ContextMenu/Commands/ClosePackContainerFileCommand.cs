using System.Windows;
using Shared.Core.PackFiles;
using Shared.Core.Services;

namespace Shared.Ui.BaseDialogs.PackFileTree.ContextMenu.Commands
{
    public class ClosePackContainerFileCommand(IPackFileService packFileService) : IContextMenuCommand
    {
        public string GetDisplayName(TreeNode node) => LocalizationManager.Instance.Get("ContextMenu.Close");
        public bool IsEnabled(TreeNode node) => true;

        public void Execute(TreeNode selectedNode)
        {
            if (MessageBox.Show(LocalizationManager.Instance.Get("Msg.ClosePackfile"), "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                packFileService.UnloadPackContainer(selectedNode.FileOwner);
        }
    }


}
