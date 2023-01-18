using Lab.Server.Models;
using Lab.Server.Remote.Dto;
using System.Xml.Serialization;

namespace Lab.Server.ViewModels
{
    public class DeviceListViewModel : ViewModelBase, IDeviceListViewModel
    {
        private List<DeviceDetail> _deviceList = new();
        public int CountDevices => _deviceList.Count;

        public List<DeviceDetail> DevicesList
        {
            get { return _deviceList; }
            set
            {
                SetValue(ref _deviceList, value);
            }
        }

        public DeviceDetail? GetDeviceDetail(string hw)
        {
            return _deviceList.FirstOrDefault(d=>d.HardwareId == hw);
        }

        public void AddOrUpdate(DeviceDetail device)
        {
            if (device.Id.Equals(Guid.Empty) && !_deviceList.Any(x => x.HardwareId == device.HardwareId))
            {
                device.Id = Guid.NewGuid();
            }
            else
            {
                _deviceList.Remove(device);
            }
            device.Command = DeviceCommand.Idle;
            _deviceList.Add(device);
            OnPropertyChanged(nameof(DevicesList));
        }
        public void Remove(string hw)
        {
            var device = _deviceList.FirstOrDefault(x => x.HardwareId == hw);
            if (device != null)
            {
                _deviceList.Remove(device);
            }
            OnPropertyChanged(nameof(DevicesList));
        }
    }
}
