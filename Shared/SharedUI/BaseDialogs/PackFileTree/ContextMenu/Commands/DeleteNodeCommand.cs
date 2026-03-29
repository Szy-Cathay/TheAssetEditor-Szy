using System.Windows;
using Shared.Core.PackFiles;
using Shared.Core.Services;

namespace Shared.Ui.BaseDialogs.PackFileTree.ContextMenu.Commands
{
    public class DeleteNodeCommand(IPackFileService packFileService) : IContextMenuCommand
    {
        public string GetDisplayName(TreeNode node) => LocalizationManager.Instance.Get("ContextMenu.Delete");
        public bool IsEnabled(TreeNode node) => true;

        public void Execute(TreeNode _selectedNode)
        {
            if (_selectedNode.FileOwner.IsCaPackFile)
            {
                MessageBox.Show(LocalizationManager.Instance.Get("Msg.UnableToEditPackfile"));
                return;
            }

            if (MessageBox.Show(LocalizationManager.Instance.Get("Msg.DeleteFile"), "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (_selectedNode.NodeType == NodeType.File)
                    packFileService.DeleteFile(_selectedNode.FileOwner, _selectedNode.Item);
                else if (_selectedNode.NodeType == NodeType.Directory)
                    packFileService.DeleteFolder(_selectedNode.FileOwner, _selectedNode.GetFullPath());
            }
        }
    }
}
