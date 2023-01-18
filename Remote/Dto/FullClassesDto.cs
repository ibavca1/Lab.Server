using System.Xml.Serialization;

namespace Lab.Server.Remote.Dto;

// using System.Xml.Serialization;
// XmlSerializer serializer = new XmlSerializer(typeof(Config));
// using (StringReader reader = new StringReader(xml))
// {
//    var test = (Config)serializer.Deserialize(reader);
// }

[XmlRoot(ElementName = "server")]
public class Server
{

    [XmlElement(ElementName = "number")]
    public int Number { get; set; }

    [XmlElement(ElementName = "ip")]
    public string Ip { get; set; }

    [XmlElement(ElementName = "port")]
    public int Port { get; set; }
}

[XmlRoot(ElementName = "servers")]
public class Servers
{

    [XmlElement(ElementName = "server")]
    public List<Server> Server { get; set; }
}

[XmlRoot(ElementName = "master")]
public class Master
{

    [XmlElement(ElementName = "code")]
    public int Code { get; set; }

    [XmlElement(ElementName = "key")]
    public object Key { get; set; }
}

[XmlRoot(ElementName = "technician")]
public class Technician
{

    [XmlElement(ElementName = "code")]
    public int Code { get; set; }

    [XmlElement(ElementName = "key")]
    public object Key { get; set; }
}

[XmlRoot(ElementName = "remote")]
public class Remote
{

    [XmlElement(ElementName = "apermissions")]
    public int Apermissions { get; set; }

    [XmlElement(ElementName = "dpermissions")]
    public int Dpermissions { get; set; }
}

[XmlRoot(ElementName = "operator")]
public class Operator
{

    [XmlElement(ElementName = "remote")]
    public Remote Remote { get; set; }
}

[XmlRoot(ElementName = "local")]
public class Local
{

    [XmlElement(ElementName = "apermissions")]
    public int Apermissions { get; set; }

    [XmlElement(ElementName = "dpermissions")]
    public int Dpermissions { get; set; }
}

[XmlRoot(ElementName = "guest")]
public class Guest
{

    [XmlElement(ElementName = "local")]
    public Local Local { get; set; }
}

[XmlRoot(ElementName = "timers")]
public class Timers
{

    [XmlElement(ElementName = "alarm")]
    public int Alarm { get; set; }

    [XmlElement(ElementName = "security")]
    public int Security { get; set; }

    [XmlElement(ElementName = "test")]
    public int Test { get; set; }

    [XmlElement(ElementName = "recovery")]
    public int Recovery { get; set; }

    [XmlElement(ElementName = "pg_minquery")]
    public int PgMinquery { get; set; }

    [XmlElement(ElementName = "pg_maxquery")]
    public int PgMaxquery { get; set; }

    [XmlElement(ElementName = "pg_waiting")]
    public int PgWaiting { get; set; }

    [XmlElement(ElementName = "rsrvdsim")]
    public int Rsrvdsim { get; set; }

    [XmlElement(ElementName = "repeatnumber")]
    public int Repeatnumber { get; set; }

    [XmlElement(ElementName = "checkout")]
    public int Checkout { get; set; }

    [XmlElement(ElementName = "checkin")]
    public int Checkin { get; set; }
}

[XmlRoot(ElementName = "sms")]
public class Sms
{

    [XmlElement(ElementName = "alert")]
    public int Alert { get; set; }

    [XmlElement(ElementName = "switchon")]
    public int Switchon { get; set; }

    [XmlElement(ElementName = "armed")]
    public int Armed { get; set; }

    [XmlElement(ElementName = "mains")]
    public int Mains { get; set; }

    [XmlElement(ElementName = "lowbattery")]
    public int Lowbattery { get; set; }
}

[XmlRoot(ElementName = "flag")]
public class Flag
{

    [XmlElement(ElementName = "alarmedisplay")]
    public int Alarmedisplay { get; set; }

    [XmlElement(ElementName = "dubip")]
    public int Dubip { get; set; }

    [XmlElement(ElementName = "ethernet")]
    public int Ethernet { get; set; }

    [XmlElement(ElementName = "tamper")]
    public int Tamper { get; set; }

    [XmlElement(ElementName = "buzzer")]
    public int Buzzer { get; set; }
}

[XmlRoot(ElementName = "object")]
public class Object
{

    [XmlElement(ElementName = "number")]
    public int Number { get; set; }

    [XmlElement(ElementName = "objectnumber")]
    public int Objectnumber { get; set; }

    [XmlElement(ElementName = "timers")]
    public Timers Timers { get; set; }

    [XmlElement(ElementName = "permissions")]
    public int Permissions { get; set; }
}

[XmlRoot(ElementName = "objects")]
public class Objects
{

    [XmlElement(ElementName = "object")]
    public List<Object> Object { get; set; }
}

[XmlRoot(ElementName = "section")]
public class Section
{

    [XmlElement(ElementName = "number")]
    public int Number { get; set; }

    [XmlElement(ElementName = "loops")]
    public string Loops { get; set; }
}

[XmlRoot(ElementName = "sections")]
public class Sections
{

    [XmlElement(ElementName = "section")]
    public Section Section { get; set; }
}

[XmlRoot(ElementName = "loop")]
public class Loop
{

    [XmlElement(ElementName = "number")]
    public int Number { get; set; }

    [XmlElement(ElementName = "zone")]
    public int Zone { get; set; }

    [XmlElement(ElementName = "type")]
    public int Type { get; set; }

    [XmlElement(ElementName = "status")]
    public int Status { get; set; }

    [XmlElement(ElementName = "zones")]
    public int Zones { get; set; }
}

[XmlRoot(ElementName = "loops")]
public class Loops
{

    [XmlElement(ElementName = "loop")]
    public List<Loop> Loop { get; set; }
}

[XmlRoot(ElementName = "relay")]
public class Relay
{

    [XmlElement(ElementName = "number")]
    public int Number { get; set; }

    [XmlElement(ElementName = "switch")]
    public int Switch { get; set; }

    [XmlElement(ElementName = "type")]
    public int Type { get; set; }

    [XmlElement(ElementName = "permissions")]
    public int Permissions { get; set; }
}

[XmlRoot(ElementName = "relays")]
public class Relays
{

    [XmlElement(ElementName = "relay")]
    public List<Relay> Relay { get; set; }
}

[XmlRoot(ElementName = "owled")]
public class Owled
{

    [XmlElement(ElementName = "number")]
    public int Number { get; set; }

    [XmlElement(ElementName = "switch")]
    public int Switch { get; set; }

    [XmlElement(ElementName = "type")]
    public int Type { get; set; }

    [XmlElement(ElementName = "permissions")]
    public int Permissions { get; set; }
}

[XmlRoot(ElementName = "owleds")]
public class Owleds
{

    [XmlElement(ElementName = "owled")]
    public Owled Owled { get; set; }
}

[XmlRoot(ElementName = "user")]
public class User
{

    [XmlElement(ElementName = "number")]
    public int Number { get; set; }

    [XmlElement(ElementName = "code")]
    public object Code { get; set; }

    [XmlElement(ElementName = "key")]
    public string Key { get; set; }

    [XmlElement(ElementName = "local")]
    public Local Local { get; set; }

    [XmlElement(ElementName = "remote")]
    public Remote Remote { get; set; }
}

[XmlRoot(ElementName = "users")]
public class Users
{

    [XmlElement(ElementName = "user")]
    public User? User { get; set; }
}

[XmlRoot(ElementName = "keyboards")]
public class Keyboards
{
}

[XmlRoot(ElementName = "extenders")]
public class Extenders
{
}

