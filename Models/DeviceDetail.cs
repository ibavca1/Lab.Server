using Lab.Server.Shared;
using System.Text;

namespace Lab.Server.Models
{
    public class DeviceDetail
    {        
        private IServer _server;
        public DeviceDetail(IServer server)
        {
            _server = server;
            //Id= Guid.NewGuid();
        }
        public Guid Id { get; set; }
        public bool IsOnline { get; set; }
        public string? DeviceNumber { get; set; }
        public string? HardwareId { get; set; }
        public List<int>? Objects { get; set; }
        public int TimeoutAlert { get; set; }
        public int TimeoutTest { get; set; }
        public int AlertPulse { get; set; }
        public int RecoveryTimeout { get; set; }
        public bool ControlTamper { get; set; }
        public bool ControlSpeaker { get; set; }
        public bool IndicationAlert { get; set; }
        public List<DeviceTest>? DeviceTests { get; set; }
        public List<Keyboard>? keyboards { get; set; }
        public List<Section>? Sections { get; set; }
        public List<User>? Users { get; set; }
        public List<Communication>? Communications { get; set; }
        public Status? Status { get; set; }
        public bool Done { get; set; }
        public bool IsBusy { get; set; }
        public DeviceCommand Command { get; set; } 
        public object? ExecuteCommand(DeviceCommand command) => command switch
        {
            DeviceCommand.GetConfig => GetConfig(),
            DeviceCommand.GetModel => GetModel(),
            DeviceCommand.GetModes => GetModes(),
            DeviceCommand.GetLog => GetLog(),
            DeviceCommand.GetStatus => GetStatus(),
            DeviceCommand.GetGsm => GetGsm(),
            DeviceCommand.GetGps => GetGps(),
            DeviceCommand.GetExtenders => GetExtenders(),
            DeviceCommand.GetKeyboards => GetKeyboards(),
            DeviceCommand.GetSensors => GetSensors(),
            DeviceCommand.GetTemperatures => GetTemperatures(),
            DeviceCommand.GetRelay => GetRelay(),
            DeviceCommand.GetUsers => GetUsers(),
            _ => throw new ArgumentException("Invalid enum value for command", nameof(command))
        };

        #region Command
        // using System.Xml.Serialization;
        // XmlSerializer serializer = new XmlSerializer(typeof(Config));
        // using (StringReader reader = new StringReader(xml))
        // {
        //    var test = (Config)serializer.Deserialize(reader);
        // }
        private string GetConfig()
        {
            IsBusy = true;
            Command = DeviceCommand.GetConfig;
            var command = "get config\r\n";
            byte [] data = Encoding.UTF8.GetBytes(command);
            _server.send_iot_message(ulong.Parse(HardwareId), uint.Parse(DeviceNumber), data, 1);
            return "";
        }
        private string GetModel()
        {
            return "";
        }
        private string GetModes()
        {
            return "";
        }
        private string GetLog()
        {
            return "";
        }
        private string GetStatus()
        {
            return "";
        }
        private string GetGsm()
        {
            return "";
        }
        private string GetGps()
        {
            return "";
        }
        private string GetExtenders()
        {
            return "";
        }
        private string GetKeyboards()
        {
            return "";
        }
        private string GetSensors()
        {
            return "";
        }
        private string GetTemperatures()
        {
            return "";
        }
        private string GetRelay()
        {
            return "";
        }
        private string GetUsers()
        {
            return "";
        }
        #endregion
    }
}
