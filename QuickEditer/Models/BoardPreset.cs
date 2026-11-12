using System.Collections.Generic;

namespace QuickEditer.Models
{
    public class BoardPreset
    {
        public string Name { get; set; } = "";
        public string Fqbn { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>内置常用开发板列表</summary>
        public static List<BoardPreset> BuiltIn { get; } = new()
        {
            new BoardPreset { Name = "ESP32 Dev Module", Fqbn = "esp32:esp32:esp32", Description = "通用 ESP32 开发板" },
            new BoardPreset { Name = "ESP32-S3 Dev Module", Fqbn = "esp32:esp32:esp32s3", Description = "ESP32-S3 开发板" },
            new BoardPreset { Name = "ESP32-C3 Dev Module", Fqbn = "esp32:esp32:esp32c3", Description = "ESP32-C3 开发板" },
            new BoardPreset { Name = "NodeMCU 1.0 (ESP-12E)", Fqbn = "esp8266:esp8266:nodemcuv2", Description = "ESP8266 NodeMCU" },
            new BoardPreset { Name = "Wemos D1 mini", Fqbn = "esp8266:esp8266:d1_mini", Description = "ESP8266 D1 mini" },
            new BoardPreset { Name = "Arduino Uno", Fqbn = "arduino:avr:uno", Description = "Arduino Uno" },
            new BoardPreset { Name = "Arduino Nano", Fqbn = "arduino:avr:nano", Description = "Arduino Nano" },
        };
    }
}