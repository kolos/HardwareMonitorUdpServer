using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorUdpServer
{
    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    public class Monitor
    {
        private Computer _computer;
        int[] _monitored_ids;
        int _value_idx;
        float[] _values;
        string _udp_client_ip;
        int _udp_client_port = 35432;
        public Monitor(int[] monitored_ids, string clientIp)
        {
            _monitored_ids = monitored_ids;
            _udp_client_ip = clientIp;
            _values = new float[monitored_ids.Length];

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = false,
                IsNetworkEnabled = true,
                IsStorageEnabled = true
            };

            _computer.Open();
        }

        public void Update()
        {
            UpdateSensor();
            StoreMonitoredSensors();
            SendValues();
        }

        ~Monitor()
        {
            _computer.Close();
        }
        private void StoreSensorFiltered(int idx, ISensor sensor)
        {
            if(_monitored_ids.Contains(idx))
            {
                // Console.WriteLine($"{_value_idx}: {idx}\t{sensor.Name}");
                _values[_value_idx++] = (float)sensor.Value;
            }
        }

        private void UpdateSensor()
        {
            _computer.Accept(new UpdateVisitor());
        }

        private void SendValues()
        {
            using (UdpClient udp = new UdpClient(_udp_client_ip, _udp_client_port))
            {
                byte[] buff = new byte[_values.Length * sizeof(float)];
                Buffer.BlockCopy(_values, 0, buff, 0, _values.Length * sizeof(float));
                udp.Send(buff, buff.Length);
            }
        }

        private void StoreMonitoredSensors()
        {
            int sensor_idx = 0; _value_idx = 0;
            foreach (IHardware hardware in _computer.Hardware)
            {
                foreach (IHardware subhardware in hardware.SubHardware)
                {
                    foreach (ISensor sensor in subhardware.Sensors)
                    {
                        StoreSensorFiltered(sensor_idx++, sensor);
                    }
                }

                foreach (ISensor sensor in hardware.Sensors)
                {
                    StoreSensorFiltered(sensor_idx++, sensor);
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var monitored_ids = new int[]
            {
                27,// fan rpm
                40,// cpu load
                58, // cpu temp
                63, // cpu power
                65, // soc power
                78, // mem used
                84, // fps
                85, // gpu ded
                86, // gpu shared
                87, // 3d usage
                111, // ssd activity
                117 // network down
            };

            var monitor = new Monitor(monitored_ids, "192.168.0.128");
            
            while(true)
            {
                monitor.Update();
                Thread.Sleep(2000);
            }
        }
    }
}
