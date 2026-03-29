using System.Windows;
using Shared.Core.PackFiles;
using Shared.Core.Services;
using Shared.Ui.Common;

namespace Shared.Ui.BaseDialogs.PackFileTree.ContextMenu.Commands
{
    public class CopyToEditablePackCommand(IPackFileService packFileService) : IContextMenuCommand
    {
        public string GetDisplayName(TreeNode node) => LocalizationManager.Instance.Get("ContextMenu.CopyToEditablePack");
        public bool IsEnabled(TreeNode node) => true;

        public void Execute(TreeNode _selectedNode)
        {
            if (packFileService.GetEditablePack() == null)
            {
                MessageBox.Show(LocalizationManager.Instance.Get("Msg.NoEditablePack"));
                return;
            }

            using (new WaitCursor())
            {
                var files = _selectedNode.GetAllChildFileNodes();
                foreach (var file in files)
                    packFileService.CopyFileFromOtherPackFile(file.FileOwner, file.GetFullPath(), packFileService.GetEditablePack());
            }
        }
    }


}
