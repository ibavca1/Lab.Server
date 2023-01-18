using Lab.Server.Models;
using System.ComponentModel;

namespace Lab.Server.ViewModels
{
    public interface IDeviceListViewModel
    {
        int CountDevices { get; }
        List<DeviceDetail> DevicesList { get; }
        DeviceDetail? GetDeviceDetail(string hw);
        event PropertyChangedEventHandler PropertyChanged;
        void AddOrUpdate(DeviceDetail device);
        void Remove(string hw);
    }
}
