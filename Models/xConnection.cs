using System.Net.Sockets;
using System.Text;

namespace Lab.Server.Models
{
    public class device_registration
    {
        public UInt64 hardware_id { get; set; }
        public List<ObjectRegistration> objects;

        public xConnection connection { get; set; }

        public device_registration()
        {
            objects = new List<ObjectRegistration>();
        }
    }

    public class CleanerTimer : System.Timers.Timer
    {
        private xConnection connection;

        public CleanerTimer(xConnection connection)
        {
            this.connection = connection;
        }

        public xConnection Connection
        {
            get { return connection; }
        }

    }
    public class xConnection : IDisposable
    {
        public ReaderWriterLockSlim connectionLock;

        public ReaderWriterLockSlim cleaner_timer_lock;
        public CleanerTimer cleaner_timer;

        public Socket socket { get; set; }

        public UInt64 epoch { get; set; }

        public DateTime rx_cb_date { get; set; }
        public int rx_cb_counter = -1;
        public byte[] rx_cb_buffer;
        public byte[] buffer;
        public int remaining_bytes;

        public device_registration registration { get; set; }

        public int task_id { get; set; }

        public IOT_conversation_status tx_status { get; set; }
        public UInt32 tx_object_id { get; set; }
        public UInt16 tx_packet_id { get; set; }
        public UInt16 tx_frame_number { get; set; }
        public byte[] tx_buffer;
        public IOT_conversation_status rx_status { get; set; }
        public UInt16 rx_packet_id { get; set; }
        public UInt16 rx_frame_number { get; set; }
        public byte[] rx_buffer;
        public DeviceCommand Command { get; set; }
        public event Action<object, IotEventArgs> IotComplited;
        public void Execute()
        {
            //if (Command == DeviceCommand.Idle) return;
            IotComplited?.Invoke(this, new IotEventArgs
            {
                command = Command,
                hardware_id = registration.hardware_id,
                data = Encoding.UTF8.GetString(rx_buffer)
            });
        }
        public xConnection()
        {
            connectionLock = new ReaderWriterLockSlim();
            cleaner_timer_lock = new ReaderWriterLockSlim();
            cleaner_timer = new CleanerTimer(this);

            rx_cb_date = DateTime.Now;
            remaining_bytes = 0;
            tx_status = IOT_conversation_status.idle;
            rx_status = IOT_conversation_status.idle;

        }

        public void Dispose()
        {
            if (socket != null)
            {
                socket = null;
            }

            cleaner_timer.Dispose();
            cleaner_timer_lock.Dispose();
            connectionLock.Dispose();
        }
    }
}
