using CoreLib.Tools.Logging;
using System.Xml.Serialization;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;

namespace VSRO_CONTROL_API.Settings
{
    public static class SettingsLoader
    {
        public static ApiSettings? Settings;
        public static bool _isLoaded = false;

        public static bool LoadSettings()
        {
            try
            {
                using var stream = File.OpenRead("Settings/settings.xml");
                var serializer = new XmlSerializer(typeof(ApiSettings));
                if (stream.Length == 0)
                {
                    throw new FileNotFoundException();
                }

                Settings = (ApiSettings?)serializer.Deserialize(stream);

                if (Settings != null) _isLoaded = true;
                else _isLoaded = false;


                return _isLoaded;
            }
            catch (Exception ex)
            {
                Logger.Info(typeof(SettingsLoader), $"Error occurred while loading API Settings!: {ex.Message}");
                return false;
            }

        }

        public static bool SaveSettings()
        {
            try
            {
                if (Settings == null)
                    return false;

                var serializer = new XmlSerializer(typeof(ApiSettings));

                var settingsPath = "Settings/settings.xml";

                var dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using var stream = File.Create(settingsPath);
                serializer.Serialize(stream, Settings);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Info(typeof(SettingsLoader), $"Error occurred while saving API Settings!: {ex.Message}");
                return false;
            }
        }

        public static string FormatPlayerMessage(string template, Proxy proxy)
        {
            if (proxy?.Session == null)
                return template;

            var time = proxy.Session.AccumulatedPlayTime;

            return template
                .Replace("{NAME}", proxy.Session.CharacterName ?? "Unknown")
                .Replace("{TIME}", time.ToString(@"hh\:mm\:ss"))
                .Replace("{HOURS}", ((int)time.TotalHours).ToString())
                .Replace("{MINUTES}", time.Minutes.ToString())
                .Replace("{SECONDS}", time.Seconds.ToString());
        }

        
    }


}

