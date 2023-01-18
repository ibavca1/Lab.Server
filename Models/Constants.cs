namespace Lab.Server.Models
{
    public enum MSG_Sections_status
    {
        section_disabled,                           //  0 раздел отключен                                           (не на охране)
        section_disarmed,                           //  1 раздел не на охране                                       (не на охране)
        section_armed,                              //  2 раздел на охране                                          (на охране)    (восстановлен)
        section_alarmed                             //  3 раздел в тревоге                                          (на охране)    (нарушен)
    }
    public enum iot_error_code
    {
        ok,
        device_is_offline,
        device_is_busy,
        transmission_error,
        reception_error,
        device_is_check_in,
        device_is_check_out
    }
    public enum IOT_conversation_status
    {
        idle,
        is_waiting,
        in_progress,
        successfully_completed,
        execution_error
    }
    public enum MessageType
    {
        msg_none = 0,
        msg_securige_legacy = 1,
        msg_securige_classic = 2,
        msg_securige_classic_with_zones = 3,
        msg_securige_dual = 4,
        msg_securige_dual_new = 5,
        msg_surgard_cid = 6,
        msg_barier_bin = 7,
        msg_barier_cid = 8,
        msg_ajax = 9,
        msg_andromeda = 10,
        msg_andromeda_macroscop = 11
    }
    public enum SecurigeProtocolVersion
    {
        none = 0,
        securige_1 = 1,
        securige_2 = 2,
        iot = 3,
        securige_bolid = 4,
        uni_spl_msg = 7
    }
    public enum Return_Code
    {
        ok_and_receiving,
        ok_and_close_connection,
        socket_disconnect,
        short_packet_length,
        not_valid_packet,
        size_error_in_packet_header,
        not_all_packet_received,
        packet_crc_error,
        size_error_in_message_header,
        message_crc_error,
        wrong_receipt,
        wrong_frame
    }
    public enum PacketType
    {
        cmd_receipt = 0x00,
        cmd_send = 0x03,
        cmd_keep_alive = 0x0A,
        cmd_hello = 0x0B,
        cmd_bye = 0x0C
    }
}
