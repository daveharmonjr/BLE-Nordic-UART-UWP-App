using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

//UUIDS
//RX ID =           6e400003-b5a3-f393-e0a9-e50e24dcca9e
//TX ID =           6e400002-b5a3-f393-e0a9-e50e24dcca9e
//UART Service ID = 6e400001-b5a3-f393-e0a9-e50e24dcca9e



namespace BLEApp1
{

    

    //Declare Event Handlers for receiving data from the BLE UART Device & Connection Status
    public delegate void ValueChangeCompletedHandler(String charIn);
    public delegate void DeviceConnectionUpdatedHandler(bool isConnected, string error);


    public class UARTEngine
    {
        //GUIDS
        Guid rxGUID = new Guid("6e400003-b5a3-f393-e0a9-e50e24dcca9e");
        Guid txGUID = new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
        Guid uartGUID = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

        //BLE GATT Objects for Receiving/Sending Data
        private GattDeviceService _service = null;
        private GattCharacteristic _characteristicRX = null;
        private GattCharacteristic _characteristicTX = null;

        private static UARTEngine _instance = new UARTEngine();

        bool initialized = false;

        //Allows other parts of the program to access the instance of this class
        public static UARTEngine Instance
        {
            get { return _instance; }
        }

        public event ValueChangeCompletedHandler ValueChangeCompleted;
        public event DeviceConnectionUpdatedHandler DeviceConnectionUpdated;

        public void Deinitialize()
        {

            //Remove event handler and set characteristic to null
            if(_characteristicRX != null)
            {
                _characteristicRX.ValueChanged -= Oncharacteristic_ValueChanged;
                _characteristicRX = null;
            }

            //Set TX to null
            if(_characteristicTX != null)
            {
                _characteristicTX = null;
            }

            if(_service != null)
            {
                _service.Device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            }

        }



        public async void InitializeServiceAsync(string deviceID)
        {
            try
            {
                //Deinitialize();
                _service = await GattDeviceService.FromIdAsync(deviceID);

                if(_service != null)
                {
                    //Check to make sure we arent already connected
                    if(DeviceConnectionUpdated != null && (_service.Device.ConnectionStatus == BluetoothConnectionStatus.Connected))
                    {
                        DeviceConnectionUpdated(true, null);
                    }

                    _service.Device.ConnectionStatusChanged += OnConnectionStatusChanged;

                    //Setup RX Characteristic and register event handler
                    _characteristicRX = _service.GetCharacteristics(rxGUID)[0];
                    _characteristicRX.ValueChanged += Oncharacteristic_ValueChanged;

                    var currentDescriptorValue = await _characteristicRX.ReadClientCharacteristicConfigurationDescriptorAsync();
                    await _characteristicRX.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                    _characteristicTX = _service.GetCharacteristics(txGUID)[0];

                }

            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Accessing your device failed." + Environment.NewLine + e.Message);

                if (DeviceConnectionUpdated != null)
                {
                    DeviceConnectionUpdated(false, "Accessing device failed: " + e.Message);
                }
            }


        }

        //Send TX Value
        public async void SendTXValue(String data)
        {

            IBuffer sendBuf = Encoding.ASCII.GetBytes(data).AsBuffer();

            if (sendBuf.Length < 21)
            {
                await _characteristicTX.WriteValueAsync(sendBuf, GattWriteOption.WriteWithoutResponse);
            }
            else
            {
                int remainder = data.Length % 20;
                int numLoops = data.Length / 20;

                //Build & Send 20 Byte Packets
                for(int i = 0; i < numLoops; i++)
                {
                    String buff = "";

                    //Build 20 Byte Packet
                    for (int j = 0; j < 20; j++)
                    {
                        
                        buff += data[j + (20*i)];

                        //Send 20 Byte Packet
                        if(j == 19)
                        {
                            sendBuf = Encoding.ASCII.GetBytes(buff).AsBuffer();
                            await _characteristicTX.WriteValueAsync(sendBuf, GattWriteOption.WriteWithoutResponse);
                        }
                    }

                    //Send Remainder at end of loop if there is a remainder
                    if(i == (numLoops - 1) && remainder != 0)
                    {
                        buff = "";
                        for(int k = 0; k < remainder; k++)
                        {
                            buff += data[k + (20 * i) + 20];
                        }

                        sendBuf = Encoding.ASCII.GetBytes(buff).AsBuffer();
                        await _characteristicTX.WriteValueAsync(sendBuf, GattWriteOption.WriteWithoutResponse);

                    }

                }


            }

        }
            



        //Event Handlers for RX Value Changed
        private void Oncharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {

            //Allocate byte buffer for new data received
            var data = new byte[args.CharacteristicValue.Length];

            //Put new data into the buffer
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);

            //Convert received data into a string
            String rxString = System.Text.Encoding.ASCII.GetString(data);

            //Send string to the event
            ValueChangeCompleted(rxString);

        }

        //Event Handler for Connection Value Changed
        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                System.Diagnostics.Debug.WriteLine("Connected");

               
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Disconnected");
               // Deinitialize();
               // initialized = false;
            }

            if (DeviceConnectionUpdated != null)
            {
                DeviceConnectionUpdated(sender.ConnectionStatus == BluetoothConnectionStatus.Connected, null);
            }
        }
    }
}
