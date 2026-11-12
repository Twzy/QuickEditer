// 文件名：Core/Config.cs

using System;
using System.IO;
using System.Text;

namespace QuickEditer.Core
{
    public class Config
    {
        /// <summary>应用版本号</summary>
        public const string AppVersion = "v 1.0.261112";
        static Config()
        {
            if (!Directory.Exists(BaseDir))
            {
                Directory.CreateDirectory(BaseDir);
            }

            if (!Directory.Exists(ArduinoCliDir))
            {
                Directory.CreateDirectory(ArduinoCliDir);
            }
            if (!Directory.Exists(ProjectsDir))
            {
                Directory.CreateDirectory(ProjectsDir);
            }

            if (!Directory.Exists(certsDir))
            {
                Directory.CreateDirectory(certsDir);
            }
        }

        public static string BaseDir { get; private set; } = Path.Combine((string.IsNullOrEmpty(Program.ShareDir) || !Directory.Exists(Program.ShareDir) ? AppDomain.CurrentDomain.BaseDirectory : Program.ShareDir), "Data");


        /// <summary>
        ///  Arduino_cli 原始路径用于复制arduino文件
        /// </summary>
        public static string RawArduinoCliDir { get; private set; } =
             Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Arduinolib");



        /// <summary>
        ///  Arduino_cli 路径
        /// </summary>
        public static string ArduinoCliDir { get; private set; } =
        Path.Combine(BaseDir, "ArduinoCore");


        /// <summary>
        /// 存放工程项目
        /// </summary>
        public static string ProjectsDir { get; private set; } =
            Path.Combine(BaseDir, "Projects");



        #region 用来存放https证书
        public static string certsDir { get; private set; } = Path.Combine(BaseDir, "certs");

        public static string CertPem { get; private set; } = Path.Combine(certsDir, "ssl.pem");
        public static string CertKey { get; private set; } = Path.Combine(certsDir, "ssl.key");

        #endregion

        public static bool HasHttpsCert =>
        File.Exists(CertPem) && File.Exists(CertKey);


        public static void AddRecentProject(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            Cfg.RecentProjects.RemoveAll(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
            Cfg.RecentProjects.Insert(0, name);
            if (Cfg.RecentProjects.Count > 5)
                Cfg.RecentProjects.RemoveRange(5, Cfg.RecentProjects.Count - 5);
            ConfigAll();
        }


        #region 全局配置

        public static ConfigData Cfg { get; set; } = new ConfigData();

        public static string CfgFilePath = Path.Combine(BaseDir, "setting.xml");

        public static void CreatConfigFile()
        {
            ConfigData cd = new ConfigData();
            Tools.WriteByXML(cd, CfgFilePath);
        }

        public static void Reload()
        {
            ReadConfig();
        }

        public static void ReadConfig()
        {
            try
            {
                if (!File.Exists(CfgFilePath))
                {
                    CreatConfigFile();
                }
                Cfg = Tools.ReadByXML<ConfigData>(CfgFilePath);
            }
            catch (Exception ex)
            {
                CreatConfigFile();
                Cfg = Tools.ReadByXML<ConfigData>(CfgFilePath);
            }

            ReadAuth();
        }

        public static void ConfigAll()
        {
            Tools.WriteByXML(Cfg, CfgFilePath);
        }
        #endregion

        #region 密码管理
        public static AuthData Auth { get; set; } = new AuthData();

        public static string AuthFilePath = Path.Combine(BaseDir, "auth.xml");

        public static void ReadAuth()
        {
            try
            {
                if (!File.Exists(AuthFilePath))
                {
                    Tools.WriteByXML(new AuthData(), AuthFilePath);
                }
                Auth = Tools.ReadByXML<AuthData>(AuthFilePath);
            }
            catch
            {
                Tools.WriteByXML(new AuthData(), AuthFilePath);
                Auth = Tools.ReadByXML<AuthData>(AuthFilePath);
            }
        }

        public static void SaveAuth()
        {
            Tools.WriteByXML(Auth, AuthFilePath);
        }
        #endregion
    }
}