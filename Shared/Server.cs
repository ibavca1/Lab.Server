using Lab.Server.Models;
using System.Net.Sockets;
using System.Net;
using Lab.Server.Helpers;
using System.Security.Cryptography;
using System.Timers;
using Lab.Server.ViewModels;
using System.Xml.Serialization;
using Lab.Server.Remote.Dto;

namespace Lab.Server.Shared
{
    public class Server : IServer
    {

        IPEndPoint? endPoint = null;
        Socket? _serverSocket = null;
        int _backlog = 2000;
        int _bufferSize = 1024;
        object lockSocket = new object();
        object lockDate = new object();
        object lock_rx_status = new object();
        object lock_tx_status = new object();
        List<xConnection> _sockets = new List<xConnection>();
        const int MaxStackLimit = 1024;


        byte prefix = 0xDE;
        byte preamble = 0xAD;

        byte[] AES_KEY = { 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x1c, 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6 };
        byte[] AES_IV = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        Int32 PACKET_HEADER_SIZE = 6;
        Int32 MIN_PACKET_SIZE = 8;

        Int32 MAX_CHUNK_NUMBER = 51;

        Int32 MSG_HEADER_SIZE = 21;
        Int32 MSG_SERVICE_BLOCK_SIZE = 23;
        Int32 MAX_IOT_TX_BUFFER_SIZE = 793;
        UInt64 epoch_counter = 0;
        int _inactive_connection_lifetime = 0;
        Dictionary<UInt64, device_registration> registrations = new Dictionary<UInt64, device_registration>();
        ReaderWriterLockSlim registrationsLock = new ReaderWriterLockSlim();
        private IDeviceListViewModel _deviceListViewModel;

        public delegate void IotCallback(object sender, EventArgs e);
        private void SendData(object sender, EventArgs e)
        {

        }

        public Server(IDeviceListViewModel deviceListViewModel)
        {
            _deviceListViewModel = deviceListViewModel;
            Task.Run(() =>
            {
                try
                {
                    endPoint = new IPEndPoint(IPAddress.Any, 19001);
                    _serverSocket = new Socket(endPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _serverSocket.Bind(endPoint);
                    _serverSocket.Listen(_backlog);
                    _serverSocket.BeginAccept(acceptCallback, _serverSocket);
                }
                catch (Exception e)
                {

                }
            });
        }

        void ReloadCleanerTimer(xConnection connection)
        {
            connection.cleaner_timer_lock.EnterWriteLock();
            try
            {
                connection.cleaner_timer.Stop();
                if (_inactive_connection_lifetime > 0)
                {
                    connection.cleaner_timer.Interval = TimeSpan.FromSeconds(_inactive_connection_lifetime).TotalMilliseconds;
                    connection.cleaner_timer.Start();
                }
            }
            finally
            {
                connection.cleaner_timer_lock.ExitWriteLock();
            }
        }
        void RemoveCleanerTimer(xConnection connection)
        {
            connection.cleaner_timer_lock.EnterWriteLock();
            try
            {
                connection.cleaner_timer.Stop();
                connection.cleaner_timer.Close();
            }
            finally
            {
                connection.cleaner_timer_lock.ExitWriteLock();
            }
        }
        void CleanerTimerHandler(object info, ElapsedEventArgs e)
        {
            CleanerTimer timer = (CleanerTimer)info;

            xConnection connection = timer.Connection;

            connection.connectionLock.EnterUpgradeableReadLock();
            try
            {
                if (connection != null)
                {
                    device_registration registration = connection.registration;
                    if (registration != null)
                    {
                        UInt64 hardware_id = registration.hardware_id;
                        if (hardware_id != 0)
                        {
                            check_out_device(hardware_id);
                        }
                    }

                    if (connection.socket != null)
                    {
                        connection.socket.Close();                              // Callback run but no data, close the connection
                    }

                    RemoveCleanerTimer(connection);
                }
            }
            finally
            {
                connection.connectionLock.ExitUpgradeableReadLock();
            }
        }
        void AddCleanerTimer(xConnection connection)
        {
            if (_inactive_connection_lifetime > 0)
            {
                connection.cleaner_timer_lock.EnterWriteLock();
                try
                {
                    connection.cleaner_timer.AutoReset = false;
                    connection.cleaner_timer.Elapsed += CleanerTimerHandler;
                    connection.cleaner_timer.Interval = TimeSpan.FromSeconds(_inactive_connection_lifetime).TotalMilliseconds;
                    connection.cleaner_timer.Start();
                }
                finally
                {
                    connection.cleaner_timer_lock.ExitWriteLock();
                }
            }
        }
        void acceptCallback(IAsyncResult result)
        {
            Socket Socket = (Socket)result.AsyncState;

            xConnection Connection = new xConnection();

            try
            {
                Connection.socket = Socket.EndAccept(result);
            }
            catch (Exception e)
            {
                //Log.Error("Ошибка установки входящего соединения  " + e.Message);
                _serverSocket.BeginAccept(acceptCallback, _serverSocket);
                return;
            }

            Connection.connectionLock.EnterWriteLock();
            try
            {
                Connection.rx_cb_date = DateTime.Now;
                Connection.rx_cb_buffer = new byte[_bufferSize];
                Connection.buffer = new byte[2 * _bufferSize];
                Connection.epoch = ++epoch_counter;
                AddCleanerTimer(Connection);
            }
            finally
            {
                Connection.connectionLock.ExitWriteLock();
            }

            try
            {
                Connection.socket.BeginReceive(Connection.rx_cb_buffer, 0, Connection.rx_cb_buffer.Length, SocketFlags.None, ReceiveCallback, Connection);
            }
            catch (Exception e)
            {
                //Log.Error("Ошибка установки входящего соединения  " + e.Message);
                if (Connection.socket != null)
                {
                    //Log.Error("Входящее соединение закрыто  " + Connection.socket.RemoteEndPoint + " ==> " + Connection.socket.LocalEndPoint);
                    Connection.socket.Close();
                }
            }
            _serverSocket.BeginAccept(acceptCallback, _serverSocket);
            return;

        }
        void ReceiveCallback(IAsyncResult result)
        {
            xConnection Connection;
            int received_bytes;
            Return_Code return_code;
            UInt16 packet_id;
            PacketType packet_cmd = PacketType.cmd_receipt;
            SecurigeProtocolVersion protocol_version = SecurigeProtocolVersion.none;
            MessageType message_type;


            Connection = (xConnection)result.AsyncState;                         // get our connection from the callback

            try
            {
                received_bytes = Connection.socket.EndReceive(result);          // grab our buffer and count the number of bytes receives
            }
            catch
            {
                received_bytes = 0;
            }

            Connection.connectionLock.EnterUpgradeableReadLock();
            try
            {
                Connection.connectionLock.EnterWriteLock();
                try
                {
                    Connection.rx_cb_counter += 1;
                    Connection.rx_cb_date = DateTime.Now;
                }
                finally
                {
                    Connection.connectionLock.ExitWriteLock();
                }

                ReloadCleanerTimer(Connection);

                if (received_bytes == 0)                                            // make sure we've read something, 
                {                                                               // if we haven't it supposadly means that the client disconnected.
                    return_code = Return_Code.socket_disconnect;
                }
                else
                {
                    if (received_bytes < MIN_PACKET_SIZE)
                    {
                        return_code = Return_Code.short_packet_length;
                    }
                    else
                    {
                        int index = 0;
                        return_code = Return_Code.not_valid_packet;

                        Connection.connectionLock.EnterWriteLock();
                        try
                        {
                            Buffer.BlockCopy(Connection.rx_cb_buffer, 0, Connection.buffer, Connection.remaining_bytes, received_bytes);
                            received_bytes += Connection.remaining_bytes;
                            Connection.remaining_bytes = 0;
                        }
                        finally
                        {
                            Connection.connectionLock.ExitWriteLock();
                        }

                        while (index <= (received_bytes - MIN_PACKET_SIZE))
                        {
                            if ((Connection.buffer[index] != prefix) ||
                                (Connection.buffer[index + 1] != preamble))
                            {
                                ++index;
                                continue;
                            }

                            byte chunk_number = Connection.buffer[index + 2];
                            if (chunk_number > MAX_CHUNK_NUMBER)
                            {
                                return_code = Return_Code.size_error_in_packet_header;
                                break;
                            }

                            int data_size = chunk_number << 4;
                            if ((index + MIN_PACKET_SIZE + data_size) > received_bytes)
                            {
                                return_code = Return_Code.not_all_packet_received;
                                break;
                            }

                            UInt16 packet_received_CRC;
                            UInt16 packet_calculated_CRC;

                            packet_received_CRC = Connection.buffer[index + PACKET_HEADER_SIZE + 1 + data_size];
                            packet_received_CRC <<= 8;
                            packet_received_CRC |= Connection.buffer[index + PACKET_HEADER_SIZE + data_size];

                            ReadOnlySpan<byte> packet_crc_content = new ReadOnlySpan<byte>(Connection.buffer, index, PACKET_HEADER_SIZE + data_size);
                            packet_calculated_CRC = Util.FastCRC16(ref packet_crc_content, packet_crc_content.Length);
                            if (packet_received_CRC != packet_calculated_CRC)
                            {
                                return_code = Return_Code.packet_crc_error;
                                break;
                            }

                            packet_id = Connection.buffer[index + 4];
                            packet_id <<= 8;
                            packet_id |= Connection.buffer[index + 3];

                            packet_cmd = (PacketType)Connection.buffer[index + 5];
                            switch (packet_cmd)
                            {
                                case PacketType.cmd_receipt:
                                    {
                                        switch (Connection.tx_status)
                                        {
                                            case IOT_conversation_status.idle:
                                                {
                                                    Connection.connectionLock.EnterWriteLock();
                                                    try
                                                    {
                                                        Connection.tx_status = IOT_conversation_status.execution_error;
                                                    }
                                                    finally
                                                    {
                                                        Connection.connectionLock.ExitWriteLock();
                                                    }
                                                    return_code = Return_Code.wrong_receipt;
                                                    break;
                                                }
                                            case IOT_conversation_status.is_waiting:
                                                {
                                                    Connection.connectionLock.EnterWriteLock();
                                                    try
                                                    {
                                                        Connection.tx_status = IOT_conversation_status.execution_error;
                                                    }
                                                    finally
                                                    {
                                                        Connection.connectionLock.ExitWriteLock();
                                                    }
                                                    return_code = Return_Code.wrong_receipt;
                                                    break;
                                                }
                                            case IOT_conversation_status.in_progress:
                                                {
                                                    UInt64 hardware_id = 0;

                                                    if (Connection.registration == null)
                                                    {
                                                        return_code = Return_Code.wrong_receipt;
                                                    }
                                                    else
                                                    {
                                                        hardware_id = Connection.registration.hardware_id;
                                                        if (hardware_id == 0)
                                                        {
                                                            return_code = Return_Code.wrong_receipt;
                                                        }
                                                    }

                                                    if (Connection.tx_packet_id != packet_id)
                                                    {
                                                        return_code = Return_Code.wrong_receipt;
                                                    }

                                                    if (return_code == Return_Code.wrong_receipt)
                                                    {
                                                        Connection.connectionLock.EnterWriteLock();
                                                        try
                                                        {
                                                            Connection.tx_status = IOT_conversation_status.execution_error;
                                                        }
                                                        finally
                                                        {
                                                            Connection.connectionLock.ExitWriteLock();
                                                        }
                                                        break;
                                                    }

                                                    UInt16 frame_number;


                                                    byte[] buffer = Connection.tx_buffer.Take(MAX_IOT_TX_BUFFER_SIZE).ToArray();

                                                    Connection.connectionLock.EnterWriteLock();
                                                    try
                                                    {
                                                        Connection.tx_packet_id += 1;
                                                        Connection.tx_frame_number += 1;
                                                        frame_number = Connection.tx_frame_number;
                                                        Connection.tx_buffer = Connection.tx_buffer.Skip(MAX_IOT_TX_BUFFER_SIZE).ToArray();
                                                        if (Connection.tx_buffer.Length == 0)
                                                        {
                                                            frame_number |= 0x8000;
                                                            Connection.tx_status = IOT_conversation_status.successfully_completed;
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        Connection.connectionLock.ExitWriteLock();
                                                    }

                                                    byte[] Payload = iot_message_adding(hardware_id, Connection.tx_object_id, DateTime.Now, frame_number, buffer);
                                                    byte[] Packet = channel_package_assembly(Payload, PacketType.cmd_send, Connection.tx_packet_id);
                                                    Connection.socket.BeginSend(Packet, 0, Packet.Length, 0, SendCallback, Connection);
                                                    return_code = Return_Code.ok_and_receiving;
                                                    break;
                                                }
                                            case IOT_conversation_status.successfully_completed:
                                                {
                                                    UInt64 hardware_id = 0;

                                                    if (Connection.registration == null)
                                                    {
                                                        return_code = Return_Code.wrong_receipt;
                                                    }
                                                    else
                                                    {
                                                        hardware_id = Connection.registration.hardware_id;
                                                        if (hardware_id == 0)
                                                        {
                                                            return_code = Return_Code.wrong_receipt;
                                                        }
                                                    }

                                                    if (Connection.tx_packet_id != packet_id)
                                                    {
                                                        return_code = Return_Code.wrong_receipt;
                                                    }

                                                    if (return_code == Return_Code.wrong_receipt)
                                                    {
                                                        Connection.connectionLock.EnterWriteLock();
                                                        try
                                                        {
                                                            Connection.tx_status = IOT_conversation_status.execution_error;
                                                        }
                                                        finally
                                                        {
                                                            Connection.connectionLock.ExitWriteLock();
                                                        }

                                                        break;
                                                    }

                                                    Connection.connectionLock.EnterWriteLock();
                                                    try
                                                    {
                                                        Connection.tx_status = IOT_conversation_status.idle;
                                                        Connection.rx_status = IOT_conversation_status.is_waiting;
                                                    }
                                                    finally
                                                    {
                                                        Connection.connectionLock.ExitWriteLock();
                                                    }
                                                    return_code = Return_Code.ok_and_receiving;
                                                    break;
                                                }
                                            case IOT_conversation_status.execution_error:
                                                {
                                                    return_code = Return_Code.wrong_receipt;
                                                    break;
                                                }
                                        }
                                        break;
                                    }
                                case PacketType.cmd_send:
                                    {
                                        if (chunk_number < 2)
                                        {
                                            message_type = MessageType.msg_none;
                                            protocol_version = SecurigeProtocolVersion.none;
                                            if (chunk_number == 0)
                                            {
                                                return_code = Return_Code.ok_and_receiving;
                                            }
                                        }
                                        else
                                        {
                                            ReadOnlySpan<byte> CryptedPayLoad = new ReadOnlySpan<byte>(Connection.buffer, index + PACKET_HEADER_SIZE, data_size);

                                            var rm = new RijndaelManaged
                                            {
                                                Mode = CipherMode.CBC,
                                                Padding = PaddingMode.None,
                                                BlockSize = 128,
                                                KeySize = 128,
                                                Key = AES_KEY,
                                                IV = AES_IV
                                            };
                                            ICryptoTransform trans = rm.CreateDecryptor(rm.Key, rm.IV);
                                            byte[] DecryptedPayLoad = trans.TransformFinalBlock(CryptedPayLoad.ToArray(), 0, CryptedPayLoad.Length);

                                            int msgs_dataSize;
                                            msgs_dataSize = DecryptedPayLoad[2];
                                            msgs_dataSize <<= 8;
                                            msgs_dataSize |= DecryptedPayLoad[1];
                                            if (msgs_dataSize > (DecryptedPayLoad.Length - MSG_SERVICE_BLOCK_SIZE))
                                            {
                                                if (chunk_number == 2)
                                                {
                                                    message_type = MessageType.msg_securige_dual;
                                                    protocol_version = SecurigeProtocolVersion.none;
                                                }
                                                else
                                                {
                                                    return_code = Return_Code.size_error_in_message_header;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                UInt16 msgs_received_CRC;
                                                UInt16 msgs_calculated_CRC;

                                                msgs_received_CRC = DecryptedPayLoad[msgs_dataSize + MSG_HEADER_SIZE + 1];
                                                msgs_received_CRC <<= 8;
                                                msgs_received_CRC |= DecryptedPayLoad[msgs_dataSize + MSG_HEADER_SIZE];

                                                ReadOnlySpan<byte> msg_crc_content = new ReadOnlySpan<byte>(DecryptedPayLoad, 0, msgs_dataSize + MSG_HEADER_SIZE);
                                                msgs_calculated_CRC = Util.FastCRC16(ref msg_crc_content, msg_crc_content.Length);
                                                if (msgs_received_CRC != msgs_calculated_CRC)
                                                {
                                                    if (chunk_number == 2)
                                                    {
                                                        message_type = MessageType.msg_securige_dual;
                                                        protocol_version = SecurigeProtocolVersion.none;
                                                    }
                                                    else
                                                    {
                                                        return_code = Return_Code.message_crc_error;
                                                        break;
                                                    }
                                                }
                                                message_type = MessageType.msg_securige_dual_new;
                                                protocol_version = (SecurigeProtocolVersion)DecryptedPayLoad[0];
                                            }

                                            switch (protocol_version)
                                            {
                                                case SecurigeProtocolVersion.securige_2:
                                                    {
                                                        int offset = 0;
                                                        while (offset <= (DecryptedPayLoad.Length - MSG_SERVICE_BLOCK_SIZE))
                                                        {
                                                            int msg_dataSize;
                                                            msg_dataSize = DecryptedPayLoad[offset + 2];
                                                            msg_dataSize <<= 8;
                                                            msg_dataSize |= DecryptedPayLoad[offset + 1];
                                                            if (msg_dataSize > (DecryptedPayLoad.Length - MSG_SERVICE_BLOCK_SIZE))
                                                            {
                                                                return_code = Return_Code.size_error_in_message_header;
                                                                break;
                                                            }
                                                            else
                                                            {
                                                                UInt16 msg_received_CRC;
                                                                UInt16 msg_calculated_CRC;


                                                                msg_received_CRC = DecryptedPayLoad[offset + msg_dataSize + MSG_HEADER_SIZE + 1];
                                                                msg_received_CRC <<= 8;
                                                                msg_received_CRC |= DecryptedPayLoad[offset + msg_dataSize + MSG_HEADER_SIZE];

                                                                ReadOnlySpan<byte> msg_crc_content = new ReadOnlySpan<byte>(DecryptedPayLoad, 0, offset + msg_dataSize + MSG_HEADER_SIZE);
                                                                msg_calculated_CRC = Util.FastCRC16(ref msg_crc_content, msg_crc_content.Length);
                                                                if (msg_received_CRC != msg_calculated_CRC)
                                                                {
                                                                    return_code = Return_Code.message_crc_error;
                                                                    break;
                                                                }
                                                                message_type = MessageType.msg_securige_dual_new;
                                                                protocol_version = (SecurigeProtocolVersion)DecryptedPayLoad[offset];
                                                            }

                                                            UInt64 hardware_id = 0;
                                                            hardware_id = DecryptedPayLoad[offset + 10];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 9];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 8];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 7];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 6];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 5];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 4];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 3];

                                                            UInt32 object_number = 0;
                                                            object_number = DecryptedPayLoad[offset + 14];
                                                            object_number <<= 8;
                                                            object_number |= DecryptedPayLoad[offset + 13];
                                                            object_number <<= 8;
                                                            object_number |= DecryptedPayLoad[offset + 12];
                                                            object_number <<= 8;
                                                            object_number |= DecryptedPayLoad[offset + 11];

                                                            UInt16 event_code = 0;
                                                            event_code = DecryptedPayLoad[offset + 22];
                                                            event_code <<= 8;
                                                            event_code |= DecryptedPayLoad[offset + 21];

                                                            UInt16 event_parameter_1 = 0;
                                                            event_parameter_1 = DecryptedPayLoad[offset + 24];
                                                            event_parameter_1 <<= 8;
                                                            event_parameter_1 |= DecryptedPayLoad[offset + 23];

                                                            UInt32 event_parameter_2 = 0;
                                                            event_parameter_2 = DecryptedPayLoad[offset + 28];
                                                            event_parameter_2 <<= 8;
                                                            event_parameter_2 |= DecryptedPayLoad[offset + 27];
                                                            event_parameter_2 <<= 8;
                                                            event_parameter_2 |= DecryptedPayLoad[offset + 26];
                                                            event_parameter_2 <<= 8;
                                                            event_parameter_2 |= DecryptedPayLoad[offset + 25];

                                                            const byte section_mask = 0x03;
                                                            ReadOnlySpan<byte> sections_status = new ReadOnlySpan<byte>(DecryptedPayLoad, offset + 29, 8);
                                                            for (int i = 0; i < 32; ++i)
                                                            {
                                                                int byte_index = i / 4;
                                                                int group_index = i % 4;

                                                                byte section_status = sections_status[byte_index];
                                                                section_status >>= 2 * group_index;
                                                                //usm.sections_status[i] = (MSG_Sections_status)(section_status & section_mask);
                                                            }

                                                            ReadOnlySpan<byte> zones_status = new ReadOnlySpan<byte>(DecryptedPayLoad, offset + 37, 32);
                                                            for (int i = 0; i < 32; ++i)
                                                            {
                                                                //usm.zones_status[i] = (MSG_Zone_status)zones_status[i];
                                                            }

                                                            const byte tamper_mask = 0x0f;
                                                            byte tampes = DecryptedPayLoad[offset + 69];
                                                            //usm.master_tamper = (MSG_Tamper_status)(tampes & tamper_mask);
                                                            //usm.slave_tamper = (MSG_Tamper_status)((tampes >> 4) & tamper_mask);

                                                            byte master_psu = DecryptedPayLoad[offset + 70];
                                                            byte slave_psu = DecryptedPayLoad[offset + 71];

                                                            const byte psu_mask = 0x0f;
                                                            //usm.master_main_psu = (MSG_PSU_status)(master_psu & psu_mask);
                                                            //usm.master_reserve_psu = (MSG_PSU_status)((master_psu >> 4) & psu_mask);
                                                            //usm.slave_main_psu = (MSG_PSU_status)(slave_psu & psu_mask);
                                                            //usm.slave_reserve_psu = (MSG_PSU_status)((slave_psu >> 4) & psu_mask);

                                                            //usm.transmission_channal = (MSG_TX_channal)DecryptedPayLoad[offset + 77];
                                                            //usm.city = this.city;
                                                            offset += MSG_SERVICE_BLOCK_SIZE + msg_dataSize;
                                                            return_code = Return_Code.ok_and_receiving;

                                                            //SendCommandToSecurigeServer(usm);
                                                            //setUnFilteredData(Connection.rx_cb_date, 1);
                                                        }
                                                        break;
                                                    }
                                                case SecurigeProtocolVersion.iot:
                                                    {
                                                        int offset = 0;
                                                        while (offset <= (DecryptedPayLoad.Length - MSG_SERVICE_BLOCK_SIZE))
                                                        {
                                                            int msg_dataSize;
                                                            msg_dataSize = DecryptedPayLoad[offset + 2];
                                                            msg_dataSize <<= 8;
                                                            msg_dataSize |= DecryptedPayLoad[offset + 1];
                                                            if (msg_dataSize > (DecryptedPayLoad.Length - MSG_SERVICE_BLOCK_SIZE))
                                                            {
                                                                return_code = Return_Code.size_error_in_message_header;
                                                                break;
                                                            }
                                                            else
                                                            {
                                                                UInt16 msg_received_CRC;
                                                                UInt16 msg_calculated_CRC;


                                                                msg_received_CRC = DecryptedPayLoad[offset + msg_dataSize + MSG_HEADER_SIZE + 1];
                                                                msg_received_CRC <<= 8;
                                                                msg_received_CRC |= DecryptedPayLoad[offset + msg_dataSize + MSG_HEADER_SIZE];

                                                                ReadOnlySpan<byte> msg_crc_content = new ReadOnlySpan<byte>(DecryptedPayLoad, 0, offset + msg_dataSize + MSG_HEADER_SIZE);
                                                                msg_calculated_CRC = Util.FastCRC16(ref msg_crc_content, msg_crc_content.Length);
                                                                if (msg_received_CRC != msg_calculated_CRC)
                                                                {
                                                                    return_code = Return_Code.message_crc_error;
                                                                    break;
                                                                }
                                                                message_type = MessageType.msg_securige_dual_new;
                                                                protocol_version = (SecurigeProtocolVersion)DecryptedPayLoad[offset];
                                                            }

                                                            UInt64 hardware_id = 0;
                                                            hardware_id = DecryptedPayLoad[offset + 10];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 9];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 8];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 7];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 6];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 5];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 4];
                                                            hardware_id <<= 8;
                                                            hardware_id |= DecryptedPayLoad[offset + 3];

                                                            UInt32 object_number = 0;
                                                            object_number = DecryptedPayLoad[offset + 14];
                                                            object_number <<= 8;
                                                            object_number |= DecryptedPayLoad[offset + 13];
                                                            object_number <<= 8;
                                                            object_number |= DecryptedPayLoad[offset + 12];
                                                            object_number <<= 8;
                                                            object_number |= DecryptedPayLoad[offset + 11];

                                                            UInt16 frame_number = 0;
                                                            frame_number = DecryptedPayLoad[offset + 20];
                                                            frame_number <<= 8;
                                                            frame_number |= DecryptedPayLoad[offset + 19];

                                                            bool end_of_transmition = (frame_number & (1 << 15)) != 0;
                                                            frame_number &= 0x7fff;

                                                            switch (Connection.rx_status)
                                                            {
                                                                case IOT_conversation_status.idle:
                                                                    {
                                                                        Connection.connectionLock.EnterWriteLock();
                                                                        try
                                                                        {
                                                                            Connection.rx_status = IOT_conversation_status.execution_error;
                                                                        }
                                                                        finally
                                                                        {
                                                                            Connection.connectionLock.ExitWriteLock();
                                                                        }


                                                                        return_code = Return_Code.wrong_frame;
                                                                        break;
                                                                    }
                                                                case IOT_conversation_status.is_waiting:
                                                                    {
                                                                        Connection.connectionLock.EnterWriteLock();
                                                                        try
                                                                        {
                                                                            if (frame_number == 0)
                                                                            {
                                                                                Connection.rx_frame_number = frame_number;
                                                                                Connection.rx_buffer = DecryptedPayLoad.Skip(offset + MSG_HEADER_SIZE).Take(msg_dataSize).ToArray();

                                                                                if (end_of_transmition)
                                                                                {
                                                                                    //TODO: Добавить принятые данные is_waiting
                                                                                    Connection.Execute();
                                                                                    Connection.rx_status = IOT_conversation_status.successfully_completed;
                                                                                }
                                                                                else
                                                                                {
                                                                                    Connection.rx_status = IOT_conversation_status.in_progress;
                                                                                }
                                                                                return_code = Return_Code.ok_and_receiving;
                                                                            }
                                                                            else
                                                                            {
                                                                                Connection.rx_status = IOT_conversation_status.execution_error;
                                                                                return_code = Return_Code.wrong_frame;
                                                                            }
                                                                        }
                                                                        finally
                                                                        {
                                                                            Connection.connectionLock.ExitWriteLock();
                                                                        }
                                                                        break;
                                                                    }
                                                                case IOT_conversation_status.in_progress:
                                                                    {
                                                                        if (frame_number == Connection.rx_frame_number + 1)
                                                                        {
                                                                            byte[] msg_data = DecryptedPayLoad.Skip(offset + MSG_HEADER_SIZE).Take(msg_dataSize).ToArray();
                                                                            byte[] joint_buffer = new byte[Connection.rx_buffer.Length + msg_data.Length];
                                                                            Buffer.BlockCopy(Connection.rx_buffer, 0, joint_buffer, 0, Connection.rx_buffer.Length);
                                                                            Buffer.BlockCopy(msg_data, 0, joint_buffer, Connection.rx_buffer.Length, msg_data.Length);

                                                                            Connection.connectionLock.EnterWriteLock();
                                                                            try
                                                                            {
                                                                                Connection.rx_buffer = joint_buffer;
                                                                                Connection.rx_frame_number = frame_number;
                                                                                if (end_of_transmition)
                                                                                {
                                                                                    //TODO: Добавить принятые данные in_progress
                                                                                    Connection.Execute();
                                                                                    Connection.rx_status = IOT_conversation_status.successfully_completed;

                                                                                }
                                                                            }
                                                                            finally
                                                                            {
                                                                                Connection.connectionLock.ExitWriteLock();
                                                                            }
                                                                            return_code = Return_Code.ok_and_receiving;
                                                                        }
                                                                        else
                                                                        {
                                                                            Connection.connectionLock.EnterWriteLock();
                                                                            try
                                                                            {
                                                                                Connection.rx_status = IOT_conversation_status.execution_error;
                                                                            }
                                                                            finally
                                                                            {
                                                                                Connection.connectionLock.ExitWriteLock();
                                                                            }
                                                                            return_code = Return_Code.wrong_frame;
                                                                        }
                                                                        break;
                                                                    }
                                                                case IOT_conversation_status.successfully_completed:
                                                                    {
                                                                        Connection.connectionLock.EnterWriteLock();
                                                                        try
                                                                        {
                                                                            Connection.rx_status = IOT_conversation_status.execution_error;
                                                                        }
                                                                        finally
                                                                        {
                                                                            Connection.connectionLock.ExitWriteLock();
                                                                        }
                                                                        return_code = Return_Code.wrong_frame;
                                                                        break;
                                                                    }
                                                                case IOT_conversation_status.execution_error:
                                                                    {
                                                                        return_code = Return_Code.wrong_frame;
                                                                        break;
                                                                    }
                                                            }

                                                            offset += MSG_SERVICE_BLOCK_SIZE + msg_dataSize;
                                                            return_code = Return_Code.ok_and_receiving;
                                                        }
                                                        break;
                                                    }
                                            }
                                        }

                                        if (return_code == Return_Code.ok_and_receiving)
                                        {
                                            if (protocol_version != SecurigeProtocolVersion.none)
                                            {
                                                byte[] buffer = channel_package_assembly(null, PacketType.cmd_receipt, packet_id);

                                                try
                                                {
                                                    Connection.socket.BeginSend(buffer, 0, buffer.Length, 0, SendCallback, Connection);
                                                }
                                                catch
                                                {
                                                    return_code = Return_Code.socket_disconnect;
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case PacketType.cmd_keep_alive:
                                    {
                                        var hw = Connection.registration.hardware_id;
                                        var device = _deviceListViewModel.DevicesList.FirstOrDefault(x => x.HardwareId == hw.ToString());
                                        device.IsOnline = true;
                                        _deviceListViewModel.AddOrUpdate(device);
                                        return_code = Return_Code.ok_and_receiving;
                                        break;
                                    }
                                case PacketType.cmd_hello:
                                    {
                                        if (chunk_number < 2)
                                        {
                                            return_code = Return_Code.short_packet_length;
                                            break;
                                        }

                                        ReadOnlySpan<byte> CryptedPayLoad = new ReadOnlySpan<byte>(Connection.buffer, index + PACKET_HEADER_SIZE, data_size);

                                        var rm = new RijndaelManaged
                                        {
                                            Mode = CipherMode.CBC,
                                            Padding = PaddingMode.None,
                                            BlockSize = 128,
                                            KeySize = 128,
                                            Key = AES_KEY,
                                            IV = AES_IV
                                        };
                                        ICryptoTransform trans = rm.CreateDecryptor(rm.Key, rm.IV);
                                        byte[] DecryptedPayLoad = trans.TransformFinalBlock(CryptedPayLoad.ToArray(), 0, CryptedPayLoad.Length);

                                        int msgs_dataSize;
                                        msgs_dataSize = DecryptedPayLoad[2];
                                        msgs_dataSize <<= 8;
                                        msgs_dataSize += DecryptedPayLoad[1];
                                        if (msgs_dataSize > (DecryptedPayLoad.Length - MSG_SERVICE_BLOCK_SIZE))
                                        {
                                            return_code = Return_Code.size_error_in_message_header;
                                            break;
                                        }

                                        UInt16 msgs_received_CRC;
                                        UInt16 msgs_calculated_CRC;


                                        msgs_received_CRC = DecryptedPayLoad[msgs_dataSize + MSG_HEADER_SIZE + 1];
                                        msgs_received_CRC <<= 8;
                                        msgs_received_CRC += DecryptedPayLoad[msgs_dataSize + MSG_HEADER_SIZE];

                                        ReadOnlySpan<byte> msgs_crc_content = new ReadOnlySpan<byte>(DecryptedPayLoad, 0, msgs_dataSize + MSG_HEADER_SIZE);
                                        msgs_calculated_CRC = Util.FastCRC16(ref msgs_crc_content, msgs_crc_content.Length);
                                        if (msgs_received_CRC != msgs_calculated_CRC)
                                        {
                                            return_code = Return_Code.message_crc_error;
                                            break;
                                        }
                                        message_type = MessageType.msg_securige_dual_new;
                                        protocol_version = (SecurigeProtocolVersion)DecryptedPayLoad[0];

                                        UInt32 object_number = 0;
                                        UInt64 hardware_id = 0;

                                        switch (protocol_version)
                                        {
                                            case SecurigeProtocolVersion.none:
                                            case SecurigeProtocolVersion.securige_1:
                                            case SecurigeProtocolVersion.securige_2:
                                                {
                                                    return_code = Return_Code.not_valid_packet;
                                                    break;
                                                }
                                            case SecurigeProtocolVersion.iot:
                                                {
                                                    int offset = 0;
                                                    while (offset <= (DecryptedPayLoad.Length - MSG_SERVICE_BLOCK_SIZE))
                                                    {
                                                        int msg_dataSize;
                                                        msg_dataSize = DecryptedPayLoad[offset + 2];
                                                        msg_dataSize <<= 8;
                                                        msg_dataSize += DecryptedPayLoad[offset + 1];
                                                        if (msg_dataSize > (DecryptedPayLoad.Length - MSG_SERVICE_BLOCK_SIZE))
                                                        {
                                                            return_code = Return_Code.size_error_in_message_header;
                                                            break;
                                                        }
                                                        else
                                                        {
                                                            UInt16 msg_received_CRC;
                                                            UInt16 msg_calculated_CRC;


                                                            msg_received_CRC = DecryptedPayLoad[offset + msg_dataSize + MSG_HEADER_SIZE + 1];
                                                            msg_received_CRC <<= 8;
                                                            msg_received_CRC += DecryptedPayLoad[offset + msg_dataSize + MSG_HEADER_SIZE];

                                                            ReadOnlySpan<byte> msg_crc_content = new ReadOnlySpan<byte>(DecryptedPayLoad, 0, offset + msg_dataSize + MSG_HEADER_SIZE);
                                                            msg_calculated_CRC = Util.FastCRC16(ref msg_crc_content, msg_crc_content.Length);
                                                            if (msg_received_CRC != msg_calculated_CRC)
                                                            {
                                                                return_code = Return_Code.message_crc_error;
                                                                break;
                                                            }
                                                            message_type = MessageType.msg_securige_dual_new;
                                                            protocol_version = (SecurigeProtocolVersion)DecryptedPayLoad[offset];
                                                        }

                                                        //UInt64 hardware_id = 0;
                                                        hardware_id = DecryptedPayLoad[offset + 10];
                                                        hardware_id <<= 8;
                                                        hardware_id += DecryptedPayLoad[offset + 9];
                                                        hardware_id <<= 8;
                                                        hardware_id += DecryptedPayLoad[offset + 8];
                                                        hardware_id <<= 8;
                                                        hardware_id += DecryptedPayLoad[offset + 7];
                                                        hardware_id <<= 8;
                                                        hardware_id += DecryptedPayLoad[offset + 6];
                                                        hardware_id <<= 8;
                                                        hardware_id += DecryptedPayLoad[offset + 5];
                                                        hardware_id <<= 8;
                                                        hardware_id += DecryptedPayLoad[offset + 4];
                                                        hardware_id <<= 8;
                                                        hardware_id += DecryptedPayLoad[offset + 3];


                                                        object_number = DecryptedPayLoad[offset + 14];
                                                        object_number <<= 8;
                                                        object_number += DecryptedPayLoad[offset + 13];
                                                        object_number <<= 8;
                                                        object_number += DecryptedPayLoad[offset + 12];
                                                        object_number <<= 8;
                                                        object_number += DecryptedPayLoad[offset + 11];

                                                        check_in_device(Connection, hardware_id, object_number, true, protocol_version);
                                                        offset += MSG_SERVICE_BLOCK_SIZE + msg_dataSize;

                                                        return_code = Return_Code.ok_and_receiving;
                                                    }
                                                    break;
                                                }

                                        }
                                        var deviceDetail = new DeviceDetail(this)
                                        {
                                            DeviceNumber = object_number.ToString(),
                                            HardwareId = hardware_id.ToString()
                                        };
                                        _deviceListViewModel.AddOrUpdate(deviceDetail);
                                        if (return_code == Return_Code.ok_and_receiving)
                                        {
                                            byte[] buffer = channel_package_assembly(null, PacketType.cmd_receipt, packet_id);

                                            try
                                            {
                                                Connection.socket.BeginSend(buffer, 0, buffer.Length, 0, SendCallback, Connection);
                                            }
                                            catch
                                            {
                                                return_code = Return_Code.socket_disconnect;
                                            }
                                        }
                                        break;
                                    }
                                case PacketType.cmd_bye:
                                    {
                                        ulong hardware_id = 0;
                                        if (Connection.registration != null)
                                        {
                                            hardware_id = Connection.registration.hardware_id;
                                            _deviceListViewModel.Remove(hw: hardware_id.ToString());
                                            check_out_device(hardware_id);
                                        }
                                        return_code = Return_Code.ok_and_close_connection;
                                        break;
                                    }
                            }

                            //Util.LogReccivedBytes(this.GetType().Name, received_bytes, Connection.buffer, Connection);
                            Console.WriteLine("read b = " + received_bytes);

                            index += PACKET_HEADER_SIZE + data_size + 2;
                        }

                        int remaining_bytes = received_bytes - index;
                        if (remaining_bytes > 0)
                        {
                            Connection.connectionLock.EnterWriteLock();
                            try
                            {
                                Buffer.BlockCopy(Connection.buffer, index, Connection.buffer, 0, remaining_bytes);
                                Connection.remaining_bytes = remaining_bytes;
                            }
                            finally
                            {
                                Connection.connectionLock.ExitWriteLock();
                            }
                        }
                    }
                }

            }
            finally
            {
                Connection.connectionLock.ExitUpgradeableReadLock();
            }


            string remont_end_point;
            string local_end_point;


            try
            {
                remont_end_point = Connection.socket.RemoteEndPoint.ToString();
                local_end_point = Connection.socket.LocalEndPoint.ToString();
            }
            catch
            {
                remont_end_point = "";
                local_end_point = "";
            }

            switch (return_code)
            {
                case Return_Code.ok_and_receiving:
                case Return_Code.ok_and_close_connection:
                    {
                        //Log.Debug("Ок  " + remont_end_point + " ==> " + local_end_point);

                        try
                        {
                            Connection.socket.BeginReceive(Connection.rx_cb_buffer, 0, Connection.rx_cb_buffer.Length, SocketFlags.None, ReceiveCallback, Connection);
                        }
                        catch
                        {
                            if (Connection.socket != null)
                            {
                                Connection.socket.Close();
                            }
                            Connection.Dispose();
                        }
                        break;
                    }
                case Return_Code.socket_disconnect:
                    {
                        //Log.Debug("Соединение закрыто  " + remont_end_point + " ==> " + local_end_point);
                        if (Connection.socket != null)
                        {
                            Connection.socket.Close();
                        }
                        Connection.Dispose();
                        break;
                    }
                case Return_Code.short_packet_length:
                    {
                        //Log.Error("Недостаточная длина пакета  " + remont_end_point + " ==> " + local_end_point);
                        break;
                    }
                case Return_Code.not_valid_packet:
                    {
                        //Log.Error("Неверный пакет  " + remont_end_point + " ==> " + local_end_point + " " + received_bytes);
                        if (Connection.socket != null)
                        {
                            Connection.socket.Close();
                        }
                        Connection.Dispose();
                        break;
                    }
                case Return_Code.size_error_in_packet_header:
                    {
                        //Log.Error("Принят не весь пакет  " + remont_end_point + " ==> " + local_end_point);
                        break;
                    }
                case Return_Code.not_all_packet_received:
                    {
                        //Log.Error("Ошибка размера в заголовке пакета  " + remont_end_point + " ==> " + local_end_point);
                        break;
                    }
                case Return_Code.packet_crc_error:
                    {
                        //Log.Error("Ошибка CRC пакета  " + remont_end_point + " ==> " + local_end_point);
                        break;
                    }
                case Return_Code.size_error_in_message_header:
                    {
                        //Log.Error("Ошибка размера в заголовке сообщения  " + remont_end_point + " ==> " + local_end_point);
                        break;
                    }
                case Return_Code.message_crc_error:
                    {
                        //Log.Error("Ошибка CRC сообщения  " + remont_end_point + " ==> " + local_end_point);
                        break;
                    }
                case Return_Code.wrong_receipt:
                    {
                        //Log.Error("Неверная квитанция  " + remont_end_point + " ==> " + local_end_point);
                        break;
                    }
                case Return_Code.wrong_frame:
                    {
                        //Log.Error("Неверный кадр  " + remont_end_point + " ==> " + local_end_point);
                        break;
                    }
            }
            return;
        }
        void SendCallback(IAsyncResult result)
        {
            xConnection Connection;
            int bytes_to_send;

            Connection = (xConnection)result.AsyncState;

            try
            {
                bytes_to_send = Connection.socket.EndSend(result);
            }
            catch (Exception e)
            {
                //Log.Error("Ошибка передачи  " + e.Message);
                if (Connection.socket != null)
                {
                    string remont_end_point;
                    string local_end_point;


                    try
                    {
                        remont_end_point = Connection.socket.RemoteEndPoint.ToString();
                        local_end_point = Connection.socket.LocalEndPoint.ToString();
                    }
                    catch
                    {
                        remont_end_point = "";
                        local_end_point = "";
                    }

                    //Log.Error("Соединение закрыто  " + remont_end_point + " ==> " + local_end_point);

                    Connection.socket.Close();
                    Connection.Dispose();
                }
            }
        }
        byte[] channel_package_assembly(byte[] Payload, PacketType cmd, UInt16 ID_Number)
        {
            UInt16 payloadSize = 0;
            UInt16 chunkSize = 0;


            if (Payload != null)
            {
                chunkSize += (UInt16)(Payload.Length / 16);
                if ((Payload.Length % 16) != 0)
                {
                    chunkSize += 1;
                }
                payloadSize += (UInt16)(chunkSize * 16);
            }

            int index = 0;
            int packetLength = MIN_PACKET_SIZE + payloadSize;

            Span<byte> Packet = packetLength <= MaxStackLimit ? stackalloc byte[packetLength] : new byte[packetLength];

            Packet[index] = prefix;
            index += 1;
            Packet[index] = preamble;
            index += 1;
            Packet[index] = (byte)chunkSize;
            index += 1;

            Packet[index] = (byte)(0x00ff & ID_Number);
            index += 1;
            Packet[index] = (byte)(0x00ff & (ID_Number >> 8));
            index += 1;

            Packet[index] = (byte)cmd;
            index += 1;

            if (Payload != null)
            {
                byte[] Padded_payload = new byte[payloadSize];
                Payload.CopyTo(Padded_payload, 0);
                var rm = new RijndaelManaged
                {
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.None,
                    BlockSize = 128,
                    KeySize = 128,
                    Key = AES_KEY,
                    IV = AES_IV
                };
                ICryptoTransform trans = rm.CreateEncryptor(rm.Key, rm.IV);
                byte[] CryptedPayload = trans.TransformFinalBlock(Padded_payload.ToArray(), 0, Padded_payload.Length);

                for (var i = 0; i < CryptedPayload.Length; ++i)
                {
                    Packet[index + i] = CryptedPayload[i];
                }

                index += CryptedPayload.Length;
            }

            ReadOnlySpan<byte> packet_crc_content = new ReadOnlySpan<byte>(Packet.ToArray(), 0, index);
            UInt16 packet_calculated_CRC = Util.FastCRC16(ref packet_crc_content, packet_crc_content.Length);

            Packet[index] = (byte)(0x00ff & packet_calculated_CRC);
            index += 1;
            Packet[index] = (byte)(0x00ff & (packet_calculated_CRC >> 8));
            index += 1;

            return Packet.ToArray();
        }
        byte[] iot_message_adding(UInt64 hardware_id, UInt32 object_id, DateTime time, UInt16 frame_number, byte[] Buffer)
        {
            int index = 0;
            int payloadLength = MSG_SERVICE_BLOCK_SIZE + Buffer.Length;

            Span<byte> Payload = payloadLength <= MaxStackLimit ? stackalloc byte[payloadLength] : new byte[payloadLength];

            Payload[index] = (byte)SecurigeProtocolVersion.iot;
            index += 1;

            UInt16 data_Size = (UInt16)Buffer.Length;

            Payload[index] = (byte)(0x00ff & data_Size);
            index += 1;
            Payload[index] = (byte)(0x00ff & (data_Size >> 8));
            index += 1;

            Payload[index] = (byte)(0x00000000000000ff & hardware_id);
            index += 1;
            Payload[index] = (byte)(0x00000000000000ff & (hardware_id >> 8));
            index += 1;
            Payload[index] = (byte)(0x00000000000000ff & (hardware_id >> 16));
            index += 1;
            Payload[index] = (byte)(0x00000000000000ff & (hardware_id >> 24));
            index += 1;
            Payload[index] = (byte)(0x00000000000000ff & (hardware_id >> 32));
            index += 1;
            Payload[index] = (byte)(0x00000000000000ff & (hardware_id >> 40));
            index += 1;
            Payload[index] = (byte)(0x00000000000000ff & (hardware_id >> 48));
            index += 1;
            Payload[index] = (byte)(0x00000000000000ff & (hardware_id >> 56));
            index += 1;

            Payload[index] = (byte)(0x000000ff & object_id);
            index += 1;
            Payload[index] = (byte)(0x000000ff & (object_id >> 8));
            index += 1;
            Payload[index] = (byte)(0x000000ff & (object_id >> 16));
            index += 1;
            Payload[index] = (byte)(0x000000ff & (object_id >> 24));
            index += 1;

            DateTime event_time = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            time.Subtract(event_time);
            UInt32 iot_time = (UInt32)time.Second;

            Payload[index] = (byte)(0x000000ff & iot_time);
            index += 1;
            Payload[index] = (byte)(0x000000ff & (iot_time >> 8));
            index += 1;
            Payload[index] = (byte)(0x000000ff & (iot_time >> 16));
            index += 1;
            Payload[index] = (byte)(0x000000ff & (iot_time >> 24));
            index += 1;

            Payload[index] = (byte)(0x00ff & frame_number);
            index += 1;
            Payload[index] = (byte)(0x00ff & (frame_number >> 8));
            index += 1;

            if (Buffer != null)
            {
                for (var i = 0; i < Buffer.Length; ++i)
                {
                    Payload[index + i] = Buffer[i];
                }

                index += Buffer.Length;
            }

            ReadOnlySpan<byte> message_crc_content = new ReadOnlySpan<byte>(Payload.ToArray(), 0, index);
            UInt16 message_calculated_CRC = Util.FastCRC16(ref message_crc_content, message_crc_content.Length);

            Payload[index] = (byte)(0x00ff & message_calculated_CRC);
            index += 1;
            Payload[index] = (byte)(0x00ff & (message_calculated_CRC >> 8));
            index += 1;

            return Payload.ToArray();
        }
        public iot_error_code send_iot_message(UInt64 hardware_id, UInt32 object_number, byte[] task_data, int task_id)
        {
            iot_error_code result;

            if (is_device_busy(hardware_id, object_number))
            {
                result = iot_error_code.device_is_busy;
                return result;
            }

            xConnection Connection = get_device_connection(hardware_id, object_number);

            byte[] buffer = task_data.Take(MAX_IOT_TX_BUFFER_SIZE).ToArray();

            Connection.connectionLock.EnterUpgradeableReadLock();
            try
            {
                UInt16 frame_number = 0;

                IOT_conversation_status tx_status;

                if (task_data.Length > MAX_IOT_TX_BUFFER_SIZE)
                {
                    tx_status = IOT_conversation_status.in_progress;
                }
                else
                {
                    frame_number |= 0x8000;
                    tx_status = IOT_conversation_status.successfully_completed;
                }

                byte[] Payload = iot_message_adding(hardware_id, object_number, DateTime.Now, frame_number, buffer);

                Connection.connectionLock.EnterWriteLock();
                try
                {
                    Connection.task_id = task_id;
                    Connection.tx_object_id = object_number;
                    Connection.tx_packet_id += 1;
                    Connection.tx_status = tx_status;
                    Connection.tx_frame_number = 0;
                    Connection.tx_buffer = task_data.Skip(MAX_IOT_TX_BUFFER_SIZE).ToArray();
                }
                finally
                {
                    Connection.connectionLock.ExitWriteLock();
                }

                byte[] Packet = channel_package_assembly(Payload, PacketType.cmd_send, Connection.tx_packet_id);
                try
                {
                    //TODO: Реализовать нормальное событие
                    Connection.IotComplited += (connection, e) =>
                    {
                        var deviceDetail = _deviceListViewModel.GetDeviceDetail(e.hardware_id.ToString());
                        deviceDetail.Done = true;
                        deviceDetail.IsBusy = false;
                        XmlSerializer serializer = new XmlSerializer(typeof(Config));
                        var xml = e.data.Trim(new char[] {(char)2, (char)3});
                        Config? config;
                        using (StringReader reader = new StringReader(xml))
                        {
                            config = (Config)serializer.Deserialize(reader);
                        }
                        _deviceListViewModel.AddOrUpdate(deviceDetail);
                        Console.WriteLine(e.hardware_id);
                    };

                    Connection.socket.BeginSend(Packet, 0, Packet.Length, 0, SendCallback, Connection);
                    result = iot_error_code.ok;
                }
                catch
                {
                    result = iot_error_code.transmission_error;
                }
            }
            finally
            {
                Connection.connectionLock.ExitUpgradeableReadLock();
            }

            return result;
        }
        xConnection get_device_connection(UInt64 hardware_id, UInt32 object_number)
        {
            xConnection result = null;


            registrationsLock.EnterReadLock();
            try
            {
                if (registrations.ContainsKey(hardware_id))
                {
                    device_registration registration = registrations[hardware_id];

                    foreach (ObjectRegistration obj in registration.objects)
                    {
                        if (obj.number != object_number)
                        {
                            continue;
                        }

                        result = registration.connection;
                    }
                }

                return result;
            }
            finally
            {
                registrationsLock.ExitReadLock();
            }
        }
        void check_in_device(xConnection connection, UInt64 hardware_id, UInt32 object_number, bool iot_ready, SecurigeProtocolVersion protocol)
        {
            registrationsLock.EnterUpgradeableReadLock();
            try
            {
                if (registrations.ContainsKey(hardware_id))
                {
                    var registration = registrations[hardware_id];
                    if (registration.connection.epoch == connection.epoch)
                    {
                        registrationsLock.EnterWriteLock();
                        try
                        {
                            if (registration.objects == null)
                            {
                                registration.objects = new List<ObjectRegistration>();
                            }

                            ObjectRegistration current_object = null;
                            bool object_found = false;

                            int object_counter = registration.objects.Count;
                            if (object_counter > 0)
                            {
                                foreach (var obj in registration.objects)
                                {
                                    if (obj.number == object_number)
                                    {
                                        object_found = true;
                                        current_object = obj;
                                        break;
                                    }
                                }
                            }

                            if (object_found == false)
                            {
                                ObjectRegistration object_registration = new ObjectRegistration
                                {
                                    number = object_number,
                                    iot_ready = iot_ready,
                                    protocol = protocol
                                };
                                registration.objects.Add(object_registration);
                            }
                            else
                            {
                                current_object.iot_ready = iot_ready;
                                current_object.protocol = protocol;
                            }
                        }
                        finally
                        {
                            registrationsLock.ExitWriteLock();
                        }
                    }
                    else
                    {
                        ObjectRegistration object_registration = new ObjectRegistration
                        {
                            number = object_number,
                            iot_ready = iot_ready,
                            protocol = protocol
                        };
                        registrationsLock.EnterWriteLock();
                        try
                        {
                            registration.hardware_id = hardware_id;

                            if (registration.connection != null)
                            {
                                if (registration.connection.socket != null)
                                {
                                    registration.connection.socket.Close();
                                }
                            }
                            registration.connection = connection;
                            connection.registration = registration;

                            if (registration.objects == null)
                            {
                                registration.objects = new List<ObjectRegistration>();
                            }

                            int object_counter = registration.objects.Count;
                            if (object_counter > 0)
                            {
                                registration.objects.Clear();
                            }
                            registration.objects.Add(object_registration);
                        }
                        finally
                        {
                            registrationsLock.ExitWriteLock();
                        }
                    }
                }
                else
                {
                    device_registration registration = new device_registration();

                    registration.hardware_id = hardware_id;
                    registration.connection = connection;
                    registration.objects = new List<ObjectRegistration>();

                    ObjectRegistration object_registration = new ObjectRegistration
                    {
                        number = object_number,
                        iot_ready = iot_ready,
                        protocol = protocol
                    };
                    registration.objects.Add(object_registration);

                    registrationsLock.EnterWriteLock();
                    try
                    {
                        registrations.Add(hardware_id, registration);
                        connection.registration = registration;
                    }
                    finally
                    {
                        registrationsLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                registrationsLock.ExitUpgradeableReadLock();
            }
        }
        void check_out_device(UInt64 hardware_id)
        {
            registrationsLock.EnterUpgradeableReadLock();
            try
            {
                if (registrations.ContainsKey(hardware_id))
                {
                    device_registration registration = registrations[hardware_id];

                    registrationsLock.EnterWriteLock();
                    try
                    {
                        registration.objects.Clear();
                        registrations.Remove(hardware_id);
                        registration.connection.registration = null;
                    }
                    finally
                    {
                        registrationsLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                registrationsLock.ExitUpgradeableReadLock();
            }
        }
        bool is_device_online(UInt64 hardware_id, UInt32 object_number)
        {
            bool result = false;


            registrationsLock.EnterReadLock();
            try
            {
                if (registrations.ContainsKey(hardware_id))
                {
                    device_registration registration = registrations[hardware_id];

                    foreach (ObjectRegistration obj in registration.objects)
                    {
                        if (obj.number != object_number)
                        {
                            continue;
                        }

                        result = true;
                    }
                }

                return result;
            }
            finally
            {
                registrationsLock.ExitReadLock();
            }
        }
        bool is_device_busy(UInt64 hardware_id, UInt32 object_number)
        {
            bool result = true;


            registrationsLock.EnterReadLock();
            try
            {
                if (registrations.ContainsKey(hardware_id))
                {
                    device_registration registration = registrations[hardware_id];

                    foreach (ObjectRegistration obj in registration.objects)
                    {
                        if (obj.number != object_number)
                        {
                            continue;
                        }

                        if ((obj.protocol == SecurigeProtocolVersion.iot) &&
                            (obj.iot_ready == true))
                        {
                            if (((registration.connection.rx_status == IOT_conversation_status.idle) ||
                                 (registration.connection.rx_status == IOT_conversation_status.successfully_completed)) &&
                                 (registration.connection.tx_status == IOT_conversation_status.idle))
                            {
                                result = false;
                            }
                        }
                    }
                }

                return result;
            }
            finally
            {
                registrationsLock.ExitReadLock();
            }
        }

    }
}
