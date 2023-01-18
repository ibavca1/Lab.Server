using Lab.Server.Models;
using Lab.Server.Shared;
using System.Xml.Serialization;

namespace Lab.Server.Remote.Dto
{
    [XmlRoot(ElementName = "config")]
    public class Config
    {
        private IServer _server;
        public Config(IServer server)
        {
            _server = server;
        }
        [XmlElement(ElementName = "device")]
        public Device? Device { get; set; }

        [XmlElement(ElementName = "extenders")]
        public Extenders? Extenders { get; set; }

        [XmlElement(ElementName = "keyboards")]
        public Keyboards? Keyboards { get; set; }
        public DeviceDetail ToDeviceDetail()
        {
            return new DeviceDetail(_server)
            {

            };
        }
    }
}
