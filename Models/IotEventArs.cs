namespace Lab.Server.Models
{
    public class IotEventArgs
    {
        public UInt64 hardware_id { get; set; }
        public UInt32 object_id { get; set; }
        public int city_id { get; set; }
        public string? data { get; set; }
        public iot_error_code status { get; set; }
        public int task_id { get; set; }
        public DeviceCommand command { get; set; }
    }
}
