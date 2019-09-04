using AlternatePushChannel.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AlternatePushChannel.SampleApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            SetUpSubscription();
        }

        private string _subscriptionJson;

        private async void SetUpSubscription()
        {
            try
            {
                // If your app's min version is lower than 15063, you have to check whether PushManager is supported
                if (PushManager.IsSupported)
                {
                    var subscription = await PushManager.Subscribe(WebPush.PublicKey, "myChannel1");

                    _subscriptionJson = subscription.ToJson();
                    ButtonPushToSelf.IsEnabled = true;

                    TextBoxSubscriptionJson.Text = _subscriptionJson;
                }
                else
                {
                    TextBoxSubscriptionJson.Text = "PushManager is not supported on this version of Windows.";
                }
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.ToString()).ShowAsync();
            }
        }

        private async void ButtonPushToSelf_Click(object sender, RoutedEventArgs e)
        {
            ButtonPushToSelf.IsEnabled = false;

            try
            {
                await WebPush.SendAsync(_subscriptionJson, "Push from myself");
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.ToString()).ShowAsync();
            }

            ButtonPushToSelf.IsEnabled = true;
        }
    }
}
