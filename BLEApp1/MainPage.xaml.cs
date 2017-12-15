using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;


namespace BLEApp1
{
    //UI Code and Callbacks go here this is "main()"
    public sealed partial class MainPage : Page
    {

        String deviceID;                                //Used to store the "name" of the Bluetooth Device
        bool bleConnected = false;                      //Callsbacks set this to let other parts of the program know connection status
        bool bleReconnecting = false;                   //Used to track if the ble is reconnecting to display a different message to user
        bool dataModeOn = false;                        //Currently unused. Will be for enabling setting of more advanced parameters in future
        int backspaceCount = 0;                         //Used to deal with STAR's backspace method, see UARTEngine for more info

        UARTEngine bleEngine = UARTEngine.Instance;     //Main object for BLE Connection/Setup/etc

        public MainPage()
        {
            this.InitializeComponent();                 //Starts the UI etc.
        }


        //This auto-scrolls the output windows down. This could be improved to make smoother looking.
        private void maintextoutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(maintextoutput, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f,true);
                break;
            }
        

        }

        //This function is called when connect button is pressed. Connects to bluetooth device.
        private async void FindAdaFruitBLE()
        {
            int deviceCount = 0;

            if (!bleConnected)
            {
                TextBox outputTextbox = (TextBox)this.FindName("maintextoutput");

                String findStuff = "System.DeviceInterface.Bluetooth.ServiceGuid:= \"{6e400001-b5a3-f393-e0a9-e50e24dcca9e}\" AND System.Devices.InterfaceEnabled:= System.StructuredQueryType.Boolean#True";
                var devices = await DeviceInformation.FindAllAsync(findStuff);

                //Get number of BLE Devices Connected/Paired
                deviceCount = devices.Count;

                //Check to see if the user has paired the BLE Device
                if (deviceCount == 0)
                {
                    outputTextbox.Text += "Please Pair the Bluetooth Device.\n";
                }
                //If the BLW device with the correct id's is paired
                else
                {
                    foreach (DeviceInformation device in devices)
                    {

                        deviceID = device.Id;

                        if (device.IsEnabled)
                        {
                            outputTextbox.Text += "Found Paired Device. Attempting Connection.\n";

                        }
                        else
                        {
                            outputTextbox.Text += "Can Not Find Paired Device. Please Pair in Windows Settings, Under Bluetooth.\n";
                        }
                    }

                    //Start All BLE Event Handlers/Callbacks
                    bleEngine.DeviceConnectionUpdated += Instance_DeviceConnectionUpdated;
                    bleEngine.ValueChangeCompleted += Instance_ValueChangeCompleted;
                    //Start BLE Connection
                    bleEngine.InitializeServiceAsync(deviceID);
                }


            }
            else if(bleReconnecting)
            {
                maintextoutput.Text += "Lost Connection. Check Power. Orange LED should be on/blinking if there is power.\n";
            }
            else
            {
                maintextoutput.Text += "Already Connected. If the blue light is off on the device try restarting the program.\n";
            }


        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FindAdaFruitBLE();
        }

        private async void Instance_DeviceConnectionUpdated(bool isConnected, string error)
        {
            // Serialize UI update to the the main UI thread.
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (error != null)
                {
                    ShowErrorDialog(error, "Connect error.");
                   
                }

                if (isConnected)
                {
                    bleConnected = true;
                    bleReconnecting = false;
                    maintextoutput.Text += "Connected To KCi STAR.\nPress Enter in the textfield below to start. (If a callbox you must press the program button on the board first.)\n";
                    connectButton.Content = "Connected";
                    
                }
                else
                {
                    bleReconnecting = true;
                    maintextoutput.Text += "Disconnected From Bluetooth Module. Check Bluetooth Nodule LED's, and Bluetooth Settings in Windows.\nPress Connect again.\n";
                    connectButton.Content = "Reconnecting";
                    
                }
            });
        }

        private async void Instance_ValueChangeCompleted(String data)
        {
            // Serialize UI update to the the main UI thread.
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {

                //If a backspace is received.

                //The STAR sends "\b \b" in response to a backspace, since bluetooth can receive the "\b \b" as seperated packets ie. "\b" then a packet of " \b" it has to count the # of backspaces.
                //Every two \b chars received it backspaces one space
                if(data.Contains('\b'))
                {
                    for(int i = 0; i < data.Length; i++)
                    {
                        if(data[i] == '\b')
                        {
                            backspaceCount++;
                            if(backspaceCount == 2)
                            {
                                maintextoutput.Text = maintextoutput.Text.Remove(maintextoutput.Text.Length - 1);
                                backspaceCount = 0;
                            }
                        }
                    }

                }
                else
                {
                    maintextoutput.Text += data;
                    //maintextoutput.SelectionStart = maintextoutput.Text.Length;
                    maintextoutput.Select(maintextoutput.Text.Length - 1, 1);
                }
               



            });
        }

        //Use this function for pop ups
        private async void ShowErrorDialog(string message, string title)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var dialog = new MessageDialog(message, title);
                await dialog.ShowAsync();
            });
        }

        private void inputText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(bleConnected && bleReconnecting == false)
            {
                if (inputText.Text != "")
                {
                    //Send a character when a letter is typed
                    bleEngine.SendTXValue(inputText.Text);
                    inputText.Text = "";
                }
            }


            
        }

        //This function is called whenever a key is pressed
        private void inputText_KeyDown(object sender, KeyRoutedEventArgs e)
        {

            


            //If Connected
            if(bleConnected && bleReconnecting == false)
            {
                //Check to see if the enter key is pressed and the program has connected to a BLE device
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    //Send a carriage return when enter is pressed
                    bleEngine.SendTXValue("\r");
                    inputText.Text = "";
                    
                    //Set Event Handler to true so it doesn't trigger multiple times
                    e.Handled = true;
                }
                else if(e.Key == Windows.System.VirtualKey.Back)
                {
                    bleEngine.SendTXValue("\b");
                }
                
               


            }
            //If not connected
            else
            {
                maintextoutput.Text += "\nError: Not connected to Bluetooth Module. Please reconnect.\n";
                e.Handled = true;
            }

           
          
        }

        //Change the Baud rate of the BLE Transceiver
        private void changeBaud(int baudRate)
        {

            if(baudRate == 9600)
            {
                //Send Command Activation
                UARTEngine.Instance.SendTXValue("+++" + "\r\n");
                //Send Baudrate Change
                UARTEngine.Instance.SendTXValue("AT+BAUDRATE=9600" + "\r\n");
                //End Command Activation
                UARTEngine.Instance.SendTXValue("+++" + "\r\n");
            }
            else if (baudRate == 115200)
            {
                //Send Command Activation
                UARTEngine.Instance.SendTXValue("+++" + "\r\n");
                //Send Baudrate Change
                UARTEngine.Instance.SendTXValue("AT+BAUDRATE=115200" + "\r\n");
                //End Command Activation
                UARTEngine.Instance.SendTXValue("+++" + "\r\n");
            }



        }


    }
}
