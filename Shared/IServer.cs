using Lab.Server.Models;

namespace Lab.Server.Shared
{
    public interface IServer
    {
        iot_error_code send_iot_message(UInt64 hardware_id, UInt32 object_number, byte[] task_data, int task_id);
    }
}
