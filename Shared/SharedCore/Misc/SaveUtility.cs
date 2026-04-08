using Shared.Core.PackFiles;

namespace Shared.Core.Misc
{
    public static class SaveUtility
    {
        private static readonly string s_backupFolderPath = "AssetEditor-BackUp";

        public static bool IsFilenameUnique(IPackFileService pfs, string path)
        {
            var editablePack = pfs.GetEditablePack();
            if (editablePack == null)
                throw new Exception("Can not check if filename is unique if no out packfile is selected");

            var file = pfs.FindFile(path, pfs.GetEditablePack());
            return file == null;
        }

        public static string EnsureEnding(string text, string ending)
        {
            text = text.ToLower();
            var hasCorrectEnding = text.EndsWith(ending);
            if (!hasCorrectEnding)
            {
                text = Path.GetFileNameWithoutExtension(text);
                text = text + ending;
            }

            return text;
        }

        public static void CreateFileBackup(string originalFileName)
        {
            if (File.Exists(originalFileName))
            {
                var dirName = Path.GetDirectoryName(originalFileName);
                var fileName = Path.GetFileNameWithoutExtension(originalFileName);
                var extention = Path.GetExtension(originalFileName);
                var uniqeFileName = IndexedFilename(Path.Combine(dirName, s_backupFolderPath, fileName), extention);

                Directory.CreateDirectory(Path.Combine(dirName, s_backupFolderPath));
                File.Copy(originalFileName, uniqeFileName);
            }
        }

        /// <summary>
        /// Create a timestamped backup of the original file and rotate old backups.
        /// Backup naming: {filename}_{yyyyMMdd_HHmmss}.pack
        /// Only backups matching the same source filename are counted for rotation.
        /// </summary>
        public static void CreateBackupWithRotation(string originalFileName, string backupDirectory, int maxBackupCount)
        {
            if (!File.Exists(originalFileName))
                return;

            var fileName = Path.GetFileNameWithoutExtension(originalFileName);
            var extension = Path.GetExtension(originalFileName);

            // Determine backup directory
            var backupDir = string.IsNullOrWhiteSpace(backupDirectory)
                ? Path.Combine(Path.GetDirectoryName(originalFileName), s_backupFolderPath)
                : backupDirectory;

            Directory.CreateDirectory(backupDir);

            // Create timestamped backup
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{fileName}_{timestamp}_backup{extension}";
            var backupFilePath = Path.Combine(backupDir, backupFileName);
            File.Copy(originalFileName, backupFilePath);

            // Rotate: delete oldest backups if count exceeds max
            var prefix = $"{fileName}_";
            var backups = Directory.GetFiles(backupDir, $"{prefix}*{extension}")
                .Where(f => Path.GetFileName(f).StartsWith(prefix) && Path.GetFileName(f).EndsWith(extension))
                .OrderBy(f => File.GetCreationTime(f))
                .ToList();

            while (backups.Count > maxBackupCount)
            {
                File.Delete(backups[0]);
                backups.RemoveAt(0);
            }
        }

        static string IndexedFilename(string stub, string extension)
        {
            var ix = 0;
            string filename = null;
            do
            {
                ix++;
                filename = string.Format("{0}{1}{2}", stub, ix, extension);
            } while (File.Exists(filename));
            return filename;
        }
    }
}
