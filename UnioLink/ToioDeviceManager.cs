﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;


// https://toio.github.io/toio-spec/docs/ble_communication_overview.html
// https://docs.microsoft.com/ja-jp/windows/uwp/devices-sensors/gatt-client
namespace ToioBridge
{
    public class Toio
    {
        public static readonly Guid ServiceUUID = new Guid("10B20100-5B3B-4571-9508-CF3EFCD7BBAE");

        public static readonly Guid CharacteristicUUID_IDInformation = new Guid("10B20101-5B3B-4571-9508-CF3EFCD7BBAE");
        public static readonly Guid CharacteristicUUID_MotionOrMagneticSensorInformation = new Guid("10B20106-5B3B-4571-9508-CF3EFCD7BBAE");
        public static readonly Guid CharacteristicUUID_ButtonInformation = new Guid("10B20107-5B3B-4571-9508-CF3EFCD7BBAE");
        public static readonly Guid CharacteristicUUID_BatteryInformation = new Guid("10B20108-5B3B-4571-9508-CF3EFCD7BBAE");
        public static readonly Guid CharacteristicUUID_MotorControl = new Guid("10B20102-5B3B-4571-9508-CF3EFCD7BBAE");
        public static readonly Guid CharacteristicUUID_LightControl = new Guid("10B20103-5B3B-4571-9508-CF3EFCD7BBAE");
        public static readonly Guid CharacteristicUUID_SoundControl = new Guid("10B20104-5B3B-4571-9508-CF3EFCD7BBAE");
        public static readonly Guid CharacteristicUUID_Configuration = new Guid("10B201FF-5B3B-4571-9508-CF3EFCD7BBAE");

        public ulong Address { private set; get; }

        // Service
        private GattDeviceService Service = null;
        // Information
        private GattCharacteristic CharacteristicIDInformation = null;
        private GattCharacteristic CharacteristicMotionOrMagneticSensorInformation = null;
        private GattCharacteristic CharacteristicButtonInformation = null;
        private GattCharacteristic CharacteristicBatteryInformation = null;
        // Control
        private GattCharacteristic CharacteristicMotorControl = null;
        private GattCharacteristic CharacteristicLightControl = null; // TODO
        private GattCharacteristic CharacteristicSoundControl = null; // TODO
        private GattCharacteristic CharacteristicConfiguration = null; // TODO

        private List<GattCharacteristic> characteristics = new List<GattCharacteristic>();

        public byte IDPositionID { private set; get; }
        public ushort IDCubeCenterX { private set; get; }
        public ushort IDCubeCenterY { private set; get; }
        public ushort IDCubeAngle { private set; get; }
        public ushort IDSensorX { private set; get; }
        public ushort IDSensorY { private set; get; }
        public ushort IDSensorAngle { private set; get; }

        public static readonly byte MotionSensorInformationRequestType = 0x81;//{ private set; get; }
        public static readonly byte MotionSensorInformationType = 0x01;//{ private set; get; }
        public byte MotionSensorLevelDetection { private set; get; }
        public byte MotionSensorCollisionDetection { private set; get; }
        public byte MotionSensorDoubleClickDetection { private set; get; }
        public byte MotionSensorPostureDetection { private set; get; }

        public static readonly byte MagneticSensorInformationRequestType = 0x82;//{ private set; get; }
        public static readonly byte MagneticSensorInformationType = 0x02;//{ private set; get; }
        public byte MagneticSensorStatus { private set; get; }

        public byte ButtonID { private set; get; }
        public byte ButtonStatus { private set; get; }

        public byte BatteryLife { private set; get; }

        public byte LightControlType;
        public byte LightControlDuration;
        public byte LightControlCount;
        public byte LightControlID;
        public byte LightValueRed;
        public byte LightValueGreen;
        public byte LightValueBlue;

        public byte SoundControlType;
        public byte SoundEffectID;
        public byte SoundVolume;

        public Toio(ulong address, GattDeviceService service)
        {
            Address = address;
            Service = service;

            // Initialize required characteristics
            Task task = Task.Run(async () =>
            {
                var characteristics = await Service.GetCharacteristicsForUuidAsync(Toio.CharacteristicUUID_IDInformation, BluetoothCacheMode.Uncached);
                if (characteristics.Status == GattCommunicationStatus.Success)
                {
                    CharacteristicIDInformation = characteristics.Characteristics.FirstOrDefault();
                    CharacteristicIDInformation.ValueChanged += CharacteristicIDInformation_ValueChanged;
                    await CharacteristicIDInformation.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    this.characteristics.Add(CharacteristicIDInformation);
                }

                characteristics = await Service.GetCharacteristicsForUuidAsync(Toio.CharacteristicUUID_MotionOrMagneticSensorInformation);
                if (characteristics.Status == GattCommunicationStatus.Success)
                {
                    CharacteristicMotionOrMagneticSensorInformation = characteristics.Characteristics.FirstOrDefault();
                    CharacteristicMotionOrMagneticSensorInformation.ValueChanged += CharacteristicMotionOrMagneticSensorInformation_ValueChanged;
                    await CharacteristicMotionOrMagneticSensorInformation.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    this.characteristics.Add(CharacteristicMotionOrMagneticSensorInformation);
                }

                characteristics = await Service.GetCharacteristicsForUuidAsync(Toio.CharacteristicUUID_ButtonInformation);
                if (characteristics.Status == GattCommunicationStatus.Success)
                {
                    CharacteristicButtonInformation = characteristics.Characteristics.FirstOrDefault();
                    CharacteristicButtonInformation.ValueChanged += CharacteristicButtonInformation_ValueChanged;
                    await CharacteristicButtonInformation.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    this.characteristics.Add(CharacteristicButtonInformation);
                }

                characteristics = await Service.GetCharacteristicsForUuidAsync(Toio.CharacteristicUUID_BatteryInformation);
                if (characteristics.Status == GattCommunicationStatus.Success)
                {
                    CharacteristicBatteryInformation = characteristics.Characteristics.FirstOrDefault();
                    CharacteristicBatteryInformation.ValueChanged += CharacteristicBatteryInformation_ValueChanged;
                    await CharacteristicBatteryInformation.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    this.characteristics.Add(CharacteristicBatteryInformation);
                }

                characteristics = await Service.GetCharacteristicsForUuidAsync(Toio.CharacteristicUUID_MotorControl);
                if (characteristics.Status == GattCommunicationStatus.Success)
                {
                    CharacteristicMotorControl = characteristics.Characteristics.FirstOrDefault();
                    this.characteristics.Add(CharacteristicMotorControl);
                }

                characteristics = await Service.GetCharacteristicsForUuidAsync(Toio.CharacteristicUUID_LightControl);
                if (characteristics.Status == GattCommunicationStatus.Success)
                {
                    CharacteristicLightControl = characteristics.Characteristics.FirstOrDefault();
                    //await CharacteristicLightControl.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.)
                    this.characteristics.Add(CharacteristicLightControl);
                }

                characteristics = await Service.GetCharacteristicsForUuidAsync(Toio.CharacteristicUUID_SoundControl);
                if (characteristics.Status == GattCommunicationStatus.Success)
                {
                    CharacteristicSoundControl = characteristics.Characteristics.FirstOrDefault();
                    this.characteristics.Add(CharacteristicSoundControl);
                }

                characteristics = await Service.GetCharacteristicsForUuidAsync(Toio.CharacteristicUUID_Configuration);
                if (characteristics.Status == GattCommunicationStatus.Success)
                {
                    CharacteristicConfiguration = characteristics.Characteristics.FirstOrDefault();
                    this.characteristics.Add(CharacteristicConfiguration);
                }

            });
            task.Wait();
        }

        private void CharacteristicButtonInformation_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data = ReadDataOnValueChanged(args);
            ButtonID = data[0];
            ButtonStatus = data[1];

            Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod().Name} ButtonID:{ButtonID}, ButtonStatus:{ButtonStatus}");
        }

        private void CharacteristicMotionOrMagneticSensorInformation_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data = ReadDataOnValueChanged(args);
            if (data[0] == MotionSensorInformationType)
            {
                MotionSensorLevelDetection = data[1];
                MotionSensorCollisionDetection = data[2];
                MotionSensorDoubleClickDetection = data[3];
                MotionSensorPostureDetection = data[4];

                Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod().Name} MotionSensorInformationType:{MotionSensorInformationType}, MotionSensorLevelDetection:{MotionSensorLevelDetection}, MotionSensorCollisionDetection:{MotionSensorCollisionDetection}, MotionSensorDoubleClickDetection:{MotionSensorDoubleClickDetection}, MotionSensorPostureDetection:{MotionSensorPostureDetection}");
            }
            else if (data[0] == MagneticSensorInformationType)
            {
                MagneticSensorStatus = data[1];

                Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod().Name} MagneticSensorInformationType:{MagneticSensorInformationType}, MagneticSensorStatus:{MagneticSensorStatus}");
            }
        }

        private void CharacteristicIDInformation_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data = ReadDataOnValueChanged(args);
            IDPositionID = data[0];
            IDCubeCenterX = BitConverter.ToUInt16(data, 1);
            IDCubeCenterY = BitConverter.ToUInt16(data, 3);
            IDCubeAngle = BitConverter.ToUInt16(data, 5);
            IDSensorX = BitConverter.ToUInt16(data, 7);
            IDSensorY = BitConverter.ToUInt16(data, 9);
            IDSensorAngle = BitConverter.ToUInt16(data, 11);

            Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod().Name} IDPositionID:{IDPositionID}, IDCubeCenterX:{IDCubeCenterX}, IDCubeCenterY:{IDCubeCenterY}, IDCubeCenterX:{IDCubeCenterX}, IDCubeAngle:{IDCubeAngle}, IDSensorX:{IDSensorX}, IDSensorY:{IDSensorY}, IDSensorAngle:{IDSensorAngle}");
        }

        private void CharacteristicBatteryInformation_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            BatteryLife = ReadDataOnValueChanged(args)[0];
        }

        private byte[] ReadDataOnValueChanged(GattValueChangedEventArgs args)
        {
            byte[] data = new byte[args.CharacteristicValue.Length];
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
            return data;
        }

        private async Task<byte[]> ReadDataFromGattCharacteristicAsync(GattCharacteristic characteristic)
        {
            byte[] input = null;
            GattReadResult result = await characteristic.ReadValueAsync();
            if (result.Status == GattCommunicationStatus.Success)
            {
                var reader = DataReader.FromBuffer(result.Value);
                input = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(input);
            }
            return input;
        }

        public byte ReadBatteryLife()
        {
            if (CharacteristicBatteryInformation == null)
                return 0;

            Task task = Task.Run(async () =>
            {
                byte[] values = await ReadDataFromGattCharacteristicAsync(CharacteristicBatteryInformation);
                BatteryLife = values[0];
                Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod().Name} BatteryLife {BatteryLife}");
            });
            task.Wait();

            return BatteryLife;
        }


        //public void MotorControl(bool isForwardLeft, byte speedLeft, bool isForwardRight, byte speedRight)
        //{
        //    const byte MotorControlType = 0x01;
        //    const byte MortorTargetIDLeft = 0x01;
        //    const byte MortorTargetIDRight = 0x02;
        //    byte[] data = new byte[7]
        //    {
        //        MotorControlType, MortorTargetIDLeft, (isForwardLeft ? (byte)0x01 : (byte)0x02), speedLeft, MortorTargetIDRight, (isForwardRight ? (byte)0x01 : (byte)0x02), speedRight
        //    };
        //    var writer = new DataWriter();
        //    writer.WriteBytes(data);
        //    Task task = Task.Run(async () =>
        //    {
        //        GattCommunicationStatus result = await CharacteristicMotorControl.WriteValueAsync(writer.DetachBuffer());
        //        if (result == GattCommunicationStatus.Success)
        //        {
        //            // Successfully wrote to device
        //            Debug.WriteLine("MotorControl Success");
        //        }
        //        else
        //        {
        //            Debug.WriteLine("MotorControl Failure");
        //        }
        //    });
        //    task.Wait();
        //}

        public byte[] Read(string uuid)
        {
            return null;
        }

        public void Write(string uuid, byte[] data)
        {
            //foreach (GattCharacteristic gc in characteristics)
            //{
            //    string u = gc.Uuid.ToString();
            //    Debug.WriteLine(u);
            //}
            GattCharacteristic c = characteristics.Find(x => x.Uuid.ToString() == uuid);
            if (c != null)
                Write(c, data);
        }

        private void Write(GattCharacteristic c, byte[] data)
        {
            if (c == null)
                return;
            if (data == null || data.Length == 0)
                return;

            Task task = Task.Run(async () =>
            {
                GattCommunicationStatus result = await c.WriteValueAsync(data.AsBuffer());
                if (result == GattCommunicationStatus.Success)
                {
                    // Successfully wrote to device
                    Debug.WriteLine("Write Success");
                }
                else
                {
                    Debug.WriteLine("Write Failure");
                }
            });
            task.Wait();
        }

        public void ReadRequest(string uuid, byte data)
        {
            Write(uuid, new byte[] { data });
        }
    }


    class ToioDeviceManager
    {
        private BluetoothLEAdvertisementWatcher watcher;
        private List<Toio> toioList = new List<Toio>();

        private static ToioDeviceManager instance = null;
        private static readonly object lockObj = new object();

        public delegate void NewlyFound();
        private NewlyFound newlyFound = null;

        private ToioDeviceManager()
        {
        }

        public static ToioDeviceManager Instance {
            get {
                lock (lockObj)
                {
                    if (instance == null)
                    {
                        instance = new ToioDeviceManager();
                    }
                    return instance;
                }
            }
        }

        public void Search(int period, NewlyFound f = null)
        {
            watcher = new BluetoothLEAdvertisementWatcher();
            watcher.Received += Watcher_Received;
            watcher.ScanningMode = BluetoothLEScanningMode.Passive;
            if (f != null)
                newlyFound += f;
            watcher.Start();
            Thread.Sleep(period);
            watcher.Stop();
        }


        private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (toioList.Count(t => t.Address == args.BluetoothAddress) > 0)
            {
                return;
            }

            var bleServiceUUIDs = args.Advertisement.ServiceUuids;
            foreach (var uuid in bleServiceUUIDs)
            {
                if (uuid == Toio.ServiceUUID)
                {
                    Task task = Task.Run(async () =>
                    {
                        BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);

                        GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesForUuidAsync(Toio.ServiceUUID);

                        if (result.Status == GattCommunicationStatus.Success)
                        {
                            var service = result.Services[0];

                            Toio toio = new Toio(args.BluetoothAddress, service);

                            // Test
                            byte battery = toio.ReadBatteryLife();


                            lock (lockObj)
                            {
                                toioList.Add(toio);
                            }

                            if (newlyFound != null)
                                newlyFound();
                        }
                        //var services = await bluetoothLeDevice.GetGattServicesForUuidAsync(Toio.ServiceUUID);


                    });
                    task.Wait();
                }
            }
        }

        public int GetToioCount()
        {
            int n = 0;
            lock (lockObj)
            {
                n = toioList.Count;
            }
            return n;
        }

        public Toio GetToio(int n)
        {
            Toio t = null;
            lock (lockObj)
            {
                t = toioList.ElementAt(n);
            }
            return t;
        }

    }
}