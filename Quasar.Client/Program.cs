using Quasar.Client.IO;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Quasar.Client
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // enable TLS 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // 设置未处理的异常模式，强迫所有的Windows Forms错误通过我们的处理程序。
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // 添加用于处理UI线程异常的事件处理程序
            Application.ThreadException += HandleThreadException;

            // 添加用于处理非UI线程异常的事件处理程序
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

            Application.EnableVisualStyles();
            //为 false，则新控件使用基于 GDI 的 System.Windows.Forms.TextRenderer 类。
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new QuasarApplication());
        }

        private static void HandleThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Debug.WriteLine(e);
            try
            {
                string batchFile = BatchFile.CreateRestartBatch(Application.ExecutablePath);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true,
                    FileName = batchFile
                };
                Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 处理未处理的异常的方法是重新启动应用程序，希望它们不再发生。
        /// </summary>
        /// <param name="sender">未处理的异常事件的来源。</param>
        /// <param name="e">异常事件的参数。 </param>
        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.IsTerminating)
            {
                Debug.WriteLine(e);
                try
                {
                    string batchFile = BatchFile.CreateRestartBatch(Application.ExecutablePath);

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true,
                        FileName = batchFile
                    };
                    Process.Start(startInfo);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
                finally
                {
                    Environment.Exit(0);
                }
            }
        }
    }
}
