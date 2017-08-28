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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        String deviceID;
        bool bleConnected = false;

        public MainPage()
        {
            this.InitializeComponent();
        }


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

        private async void FindAdaFruitBLE()
        {
            TextBox outputTextbox = (TextBox)this.FindName("maintextoutput");

            String findStuff = "System.DeviceInterface.Bluetooth.ServiceGuid:= \"{6e400001-b5a3-f393-e0a9-e50e24dcca9e}\" AND System.Devices.InterfaceEnabled:= System.StructuredQueryType.Boolean#True";
            var devices = await DeviceInformation.FindAllAsync(findStuff);

            foreach(DeviceInformation device in devices)
            {
                
                deviceID = device.Id;

                if(device.IsEnabled)
                {
                    outputTextbox.Text += "Found Paired Device. Attempting Connection.\n";
                }
                else
                {
                    outputTextbox.Text += "Can Not Find Paired Device. Please Pair in Windows Settings, Under Bluetooth.\n";
                }
            }

            UARTEngine.Instance.DeviceConnectionUpdated += Instance_DeviceConnectionUpdated;
            UARTEngine.Instance.ValueChangeCompleted += Instance_ValueChangeCompleted;
            UARTEngine.Instance.InitializeServiceAsync(deviceID);


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
                    maintextoutput.Text += "Connected To KCi STAR.\nPress Enter in the textfield below to start. (If a callbox you must press the program button on the board first.)\n";
                    
                }
                else
                {
                    bleConnected = false;
                    maintextoutput.Text += "Disconnected From Bluetooth Module. Check Bluetooth Nodule LED's.\nPress Connect to resume communications after resolving connection issues.\n";
                }
            });
        }

        private async void Instance_ValueChangeCompleted(String data)
        {
            // Serialize UI update to the the main UI thread.
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {

               

                maintextoutput.Text += data;
                //maintextoutput.SelectionStart = maintextoutput.Text.Length;
                maintextoutput.Select(maintextoutput.Text.Length-1, 1);

            });
        }

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
            
            
        }

        private void inputText_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if(e.Key == Windows.System.VirtualKey.Enter && bleConnected)
            {
                UARTEngine.Instance.SendTXValue((inputText.Text) + '\r');
                inputText.Text = "";
                e.Handled = true;
            }
            else if(e.Key == Windows.System.VirtualKey.Enter && !bleConnected)
            {
                maintextoutput.Text += "\nError: Not connected to Bluetooth Module. Please reconnect.\n";
                e.Handled = true;
            }
        }
    }
}
