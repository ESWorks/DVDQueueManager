using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using DVDOrders.Services;

namespace DVDOrders.Objects
{
    [DataContract(Name = "Settings")]
    public class Settings
    {
        private static Settings _instance;

        [DataMember(Name = "MaxTasks", IsRequired = true)]
        private double _maxTasks = 1;

        [DataMember(Name = "PollInterval", IsRequired = true)]
        private double _pollInterval = 1000;

        [DataMember(Name = "Robots", IsRequired = true)]
        private List<RobotConfig> _robotConfigs;

        [DataMember(Name = "EmailAddress", IsRequired = true)]
        private string _emailAddress = "admin@contoso.com";

        [DataMember(Name = "Watchers", IsRequired = true)]
        private  Dictionary<string, WatcherConfig> _watchers;

        [DataMember(Name = "LabelContent", IsRequired = true)]
        private string _labelFileFormat = "";

        [DataMember(Name = "EmailServer", IsRequired = false)]
        private string _emailServer = "";

        [DataMember(Name = "EmailDomain", IsRequired = false)]
        private string _emailDomain = "";

        [DataMember(Name = "EmailUsername", IsRequired = false)]
        private string _emailUsername = "";

        [DataMember(Name = "EmailPassword", IsRequired = false)]
        private string _emailPassword = "";

        [DataMember(Name = "EmailPort", IsRequired = false)]
        private int _emailPort = 25;

        [DataMember(Name = "EmailSSL", IsRequired = false)]
        private bool _emailSsl;

        [DataMember(Name = "EmailServerAddress", IsRequired = false)]
        private string _emailServerAddress = "";

        [DataMember(Name = "Salt",IsRequired = false)]
        private string _salt;

        [DataMember(Name = "FileFilterCheck", IsRequired = false)]
        private bool _fileFilterCheck = true;

        [DataMember(Name = "DvdQueueFolder", IsRequired = true)]
        private string _dvdQueueFolder = "";

        [DataMember(Name = "QueueRecipient", IsRequired = false)]
        private EmailRecipients _queueRecipient = EmailRecipients.Admins;

        public static void Create()
        {
            if (File.Exists("settings.xml"))
            {
                try
                {
                    _instance = (Settings)typeof(Settings).GetXmlObject(Extensions.ReadXmlToString("settings.xml"));
                }
                catch
                {
                    //
                }
            }
            else
            {
                _instance = new Settings
                {
                    _robotConfigs = new List<RobotConfig>(),

                    _watchers = new Dictionary<string, WatcherConfig>(),

                    _maxTasks = 1,

                    _pollInterval = 1000
                };

                _instance.GetXmlString().SaveXmlToFile("settings.xml");
            }

            if (string.IsNullOrEmpty(_instance._dvdQueueFolder)) _instance._dvdQueueFolder = Environment.CurrentDirectory;

            if (_instance._robotConfigs == null) _instance._robotConfigs = new List<RobotConfig>();

            if (_instance._watchers == null) _instance._watchers = new Dictionary<string,WatcherConfig>();

            if (string.IsNullOrEmpty(_instance._salt))
                _instance._salt = Services.Hash.StringHash(HashType.SHA512, DateTime.Now.Ticks.ToString());

            Save();


        }
        
        public static ReadOnlyCollection<RobotConfig> Robots()
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            return new ReadOnlyCollection<RobotConfig>(_instance._robotConfigs);
        }

        public static RobotConfig Robot(int id)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");
            
            return _instance._robotConfigs.Count(T => T.Id == id) == 0 ? null : _instance._robotConfigs.FirstOrDefault(T => T.Id == id);
        }

        public static bool DeleteRobot(int id)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            if (_instance._robotConfigs.Count(T => T.Id == id) <= 0) return true;

            try
            {
                var robot = _instance._robotConfigs.FirstOrDefault(T => T.Id == id);

                if (robot == null) return false;

                _instance._robotConfigs.Remove(robot);

                Save();

                return true;

            }
            catch
            {
                return false;
            }
        }

        public static bool ModifyRobot(RobotConfig previous, RobotConfig current)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            if (previous == null) return false;

            try
            {
                var index = _instance._robotConfigs.IndexOf(previous);

                _instance._robotConfigs[index] = current;

                Save();

                return true;

            }
            catch
            {
                return false;
            }
        }

        public static bool AddRobot(RobotConfig robot)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            if (_instance._robotConfigs.Count(T => T.Id == robot.Id) > 0) return false;

            try
            {
                _instance._robotConfigs.Add(robot);

                Save();

                return true;

            }
            catch
            {
                return false;
            }
        }

        public static void SetDefaultEmailAddress(string email)
        {
            _instance._emailAddress = email;
            Save();
        }
        public static double MaxTasks => _instance._maxTasks;
        public static void SetMaxTasks(double value)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");
            _instance._maxTasks = value;
            Save();
        }
        public static double PollInterval => _instance._pollInterval;
        public static string EmailAddress => _instance._emailAddress;
        public static EmailRecipients QueueRecipient => _instance._queueRecipient;
        public static string FormatLabelContent => _instance._labelFileFormat;
        public static void SetQueueRecipient(EmailRecipients value)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");
            _instance._queueRecipient = value;
            Save();
        }
        public static void SetPollInterval(double value)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");
            _instance._pollInterval = value;
            Save();
        }
        public static void Save()
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");
            _instance.GetXmlString().SaveXmlToFile("settings.xml");
        }
        public static void SaveWatchers()
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            _instance._watchers.GetXmlString().SaveXmlToFile("watchers.xml");
        }

        public static void AddModWatcherConfig(WatcherConfig watcherConfig)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");
            _instance._watchers[watcherConfig.Id] = watcherConfig;
            Save();
        }

        public static void RemoveWatcherConfig(string id)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");
            _instance._watchers.Remove(id);
            Save();
        }
        public static ReadOnlyCollection<WatcherConfig> GetWatcherConfigs()
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            return new ReadOnlyCollection<WatcherConfig>(_instance._watchers.Values.ToList());
        }
        public static ReadOnlyCollection<WatcherConfig> GetWatcherConfigs(int robotId)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            return new ReadOnlyCollection<WatcherConfig>(_instance._watchers.Values.Where(T=>T.Robot == robotId).ToList());
        }

        public static void SetWatcherConfigEnabled(string identifier, bool value)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            _instance._watchers[identifier].Enabled = value;

            Save();
        }

        public static WatcherConfig GetWatcherConfig(string identifier)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");
            return !_instance._watchers.ContainsKey(identifier) ? null : _instance._watchers[identifier];
        }

        public static bool RobotExists(int robot)
        {
            return _instance._robotConfigs.Any(T => T.Id == robot);
        }

        public static void SetDefaultLabelContent(string text)
        {
            if (_instance == null) throw new Exception("Create instance first before accessing method.");

            _instance._labelFileFormat = text;

            Save();
        }

        public static string GetLabelFormatted(QueueEntry entry)
        {
            var baseline = _instance._labelFileFormat;
            baseline = baseline.Replace("$line1", entry.Line1)
                .Replace("$line2", entry.Line2)
                .Replace("$line3", entry.Line3)
                .Replace("$copies", entry.CopyCount+"")
                .Replace("$source", entry.Source);
            return baseline;
        }

        public static void SetEmailServer(string site, string domain, string username, string password, string address, int port, bool ssl)
        {
            _instance._emailServerAddress = address;
            _instance._emailDomain = domain;
            _instance._emailPassword = SecurePassword.EncryptPassword(password, _instance._salt);
            _instance._emailUsername = username;
            _instance._emailPort = port;
            _instance._emailSsl = ssl;
            _instance._emailServer = site;
        }

        public static string EmailServerAddress => _instance._emailServerAddress;
        public static string EmailServerDomain => _instance._emailDomain;
        public static string EmailServerPassword => SecurePassword.DecryptPassword(_instance._emailPassword, _instance._salt);
        public static string EmailServerUsername => _instance._emailUsername;
        public static string EmailServerSite => _instance._emailServer;
        public static int EmailServerPort => _instance._emailPort;
        public static bool EmailServerSsl=> _instance._emailSsl;
        public static bool FileFilterRename => _instance._fileFilterCheck;

        public static string DvdQueueFolder => _instance._dvdQueueFolder;

        public static void SetCheckFileFilterRename(bool? isChecked)
        {
            if(_instance == null) return;
            _instance._fileFilterCheck = isChecked ?? false;
            Save();
        }

        public static void SetDvdQueueFolder(string location)
        {
            if (_instance == null) return;
            _instance._dvdQueueFolder = location;
            Save();
        }
    }

    public static class SecurePassword
    {
        private static byte[] GetEntropyBytes(string value)
        {
            return Encoding.Unicode.GetBytes(value);
        }

        private static string EncryptString(SecureString input, byte[] entropy)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.Unicode.GetBytes(ToInsecureString(input)), entropy, DataProtectionScope.CurrentUser));
        }

        private static SecureString DecryptString(string encryptedData, byte[] entropy)
        {
            try
            {
                return ToSecureString(Encoding.Unicode.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encryptedData), entropy, DataProtectionScope.CurrentUser)));
            }
            catch
            {
                return new SecureString();
            }
        }

        private static SecureString ToSecureString(string input)
        {
            var secureString = new SecureString();
            foreach (var c in input)
                secureString.AppendChar(c);
            secureString.MakeReadOnly();
            return secureString;
        }

        private static string ToInsecureString(SecureString input)
        {
            var substr = Marshal.SecureStringToBSTR(input);
            try
            {
                return Marshal.PtrToStringBSTR(substr);
            }
            finally
            {
                Marshal.ZeroFreeBSTR(substr);
            }
        }

        public static string EncryptPassword(string password, string salt)
        {
            return EncryptString(ToSecureString(password), GetEntropyBytes(salt));
        }

        public static string DecryptPassword(string password, string salt)
        {
            return ToInsecureString(DecryptString(password, GetEntropyBytes(salt)));
        }
    }
}
