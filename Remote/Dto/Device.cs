using System.Xml.Serialization;

namespace Lab.Server.Remote.Dto
{
    [XmlRoot(ElementName = "device")]
    public class Device
    {

        [XmlElement(ElementName = "serialnumber")]
        public string? Serialnumber { get; set; }

        [XmlElement(ElementName = "imei")]
        public double Imei { get; set; }

        [XmlElement(ElementName = "servers")]
        public Servers? Servers { get; set; }
        //TODO: Создать 2 класса
        [XmlElement(ElementName = "guardphone")]
        public object? Guardphone { get; set; }

        [XmlElement(ElementName = "userphone")]
        public object? Userphone { get; set; }

        [XmlElement(ElementName = "master")]
        public Master? Master { get; set; }

        [XmlElement(ElementName = "technician")]
        public Technician? Technician { get; set; }

        [XmlElement(ElementName = "operator")]
        public Operator? Operator { get; set; }

        [XmlElement(ElementName = "guest")]
        public Guest? Guest { get; set; }

        [XmlElement(ElementName = "timers")]
        public Timers? Timers { get; set; }

        [XmlElement(ElementName = "sms")]
        public Sms? Sms { get; set; }

        [XmlElement(ElementName = "flag")]
        public Flag? Flag { get; set; }

        [XmlElement(ElementName = "objects")]
        public Objects? Objects { get; set; }

        [XmlElement(ElementName = "sections")]
        public Sections? Sections { get; set; }

        [XmlElement(ElementName = "loops")]
        public Loops? Loops { get; set; }

        [XmlElement(ElementName = "relays")]
        public Relays? Relays { get; set; }

        [XmlElement(ElementName = "owleds")]
        public Owleds? Owleds { get; set; }

        [XmlElement(ElementName = "users")]
        public Users? Users { get; set; }
    }
}
