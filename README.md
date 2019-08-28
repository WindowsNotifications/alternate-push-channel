# Alternate Push Channels (Web Push for Windows apps)
How to use the alternate push channel for Windows apps


## 1. Create a new Windows app

Create a new UWP app.


## 2. Add the library

Our AlternatePushChannel library helps simplify some of the code around encryption.


## 3. Create your application server keys

To use alternate push channels, you need to have a pair of public and private server keys, exactly like Web Push.

You can easily generate a public and private key at this website: https://web-push-codelab.glitch.me/


## 4. Create the push subscription

Create a subscription, passing in your public server key. You also get to create up to 1,000 different channels, so pick a channel of your choice. Note that if you ever change the public server key, you'll first have to unregister and then re-subscribe the push channel (otherwise it'll return a cached push channel with the old key).

You'll want to grab the subscriptionJson as you'll need that to send a push notification.

```csharp
var subscription = await PushManager.Subscribe("YOUR_PUBLIC_KEY", "myChannel1");

var subscriptionJson = subscription.ToJson();

// TODO: Display the subscriptionJson or send that to your push server
```


## 5. Handle background task activation

Your app's background task will be activated when you receive a push (the foreground event listener doesn't work). In your App.xaml.cs, add the following code and call this from your app's constructor

```csharp
public App()
{
    this.InitializeComponent();
    this.Suspending += OnSuspending;

    // Call the new method
    RegisterPushBackgroundTask();
}

// New method
private void RegisterPushBackgroundTask()
{
    try
    {
        const string PushBackgroundTaskName = "Push";
        if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == PushBackgroundTaskName))
        {
            var builder = new BackgroundTaskBuilder();
            builder.Name = PushBackgroundTaskName;
            builder.SetTrigger(new PushNotificationTrigger());
            builder.Register();
        }
    }
    catch (Exception ex)
    {
    }
}

// New method
protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
{
    RawNotification notification = (RawNotification)args.TaskInstance.TriggerDetails;

    // Show a notification
    // You'll need Microsoft.Toolkit.Uwp.Notifications NuGet package installed for this code
    ToastContent content = new ToastContent()
    {
        Visual = new ToastVisual()
        {
            BindingGeneric = new ToastBindingGeneric()
            {
                Children =
                {
                    new AdaptiveText()
                    {
                        Text = "It worked!!!"
                    }
                }
            }
        }
    };

    ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(content.GetXml()));
}
```


## 6. Launch app!

Launch your app and grab your subscription details!


## 7. Test pushing to your app

Go to https://interactivenotifs.azurewebsites.net/webpush, enter your public and private key from earlier, paste in your subscription JSON from your app, and click send!


## 8. You should see a notification appear!

Your background task should be triggered and a notification should appear!