using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShortcutInstaller
{
    [RunInstaller(true)]
    public class ShortcutInstaller : Installer
    {
        const string SHORTCUT = "SHORTCUT";
        const string STARTUP = "STARTUP";

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);
        }

        public override void Commit(IDictionary savedState)
        {
            base.Commit(savedState);

            if (!Context.Parameters.ContainsKey(SHORTCUT) ||
                !Context.Parameters.ContainsKey(STARTUP))
            {
                MessageBox.Show("The parameter has not been provided for specified class.");
                return;
            }

            bool deleteDesktopShortcut = Context.Parameters[SHORTCUT] == string.Empty;
            bool deleteStartupShortcut = Context.Parameters[STARTUP] == string.Empty;

            if (deleteDesktopShortcut == true)
            {
                const string fileName = "Embark.lnk";
                var desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                delete(desktopFolder, fileName);
            }

            if (deleteStartupShortcut == true)
            {
                const string fileName = "EmbarkDetector.lnk";
                var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

                delete(startupFolder, fileName);
            }
            else
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\Embark\\Embark";

                Process process = new Process();
                process.StartInfo.FileName = "EmbarkStart.exe";
                process.StartInfo.WorkingDirectory = dir;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
            }
        }

        public override void Uninstall(IDictionary savedState)
        {
            if (!Context.Parameters.ContainsKey(STARTUP))
            {
                MessageBox.Show("The parameter has not been provided for specified class.");
                return;
            }

            bool startupShortcut = Context.Parameters[STARTUP] != string.Empty;

            if (startupShortcut)
            {
                try
                {
                    foreach (Process proc in Process.GetProcessesByName("EmbarkStart"))
                    {
                        proc.Kill();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            RemoveConfig();

            base.Uninstall(savedState);
        }

        private void delete(string folder, string name)
        {
            var shortcutFullName = $"{folder}\\{name}";
            FileInfo shortcut = new FileInfo(shortcutFullName);

            if (shortcut.Exists)
            {
                try
                {
                    shortcut.Delete();
                }
                catch
                {
                    MessageBox.Show($"The shortcut \"{shortcutFullName}\" could not be deleted.");
                }
            }
        }

        private void RemoveConfig()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dir = $"{dir}\\Embark";

            try
            {
                File.Delete($"{dir}\\embark_config.ini");
                Directory.Delete($"{dir}");
            }
            catch(Exception e)
            {
                var msg = "Could not remove configuration file. To manually complete uninstallation, delete the folder \"AppData\\local\\Embark\".";
                msg += Environment.NewLine;
                msg += $"ERROR: {e.Message}";
                MessageBox.Show(msg, "Uninstall Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
