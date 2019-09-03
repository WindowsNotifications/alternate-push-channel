# Alternate Push Channels (Web Push for Windows apps)
How to use the alternate push channel for Windows apps


# Super quick start

1. Clone this repository
1. Open the VS solution located in `src`
1. Build, deploy, and launch the app
1. Copy your *SubscriptionJson* from the app
1. Go to [this website](https://interactivenotifs.azurewebsites.net/webpush), paste your *SubscriptionJson*, type a message, and click Send
1. A notification should appear on your computer!


# Integrate into your own apps...

## 1. Create a new Windows app

Create a new UWP app.


## 2. Add the library

Our AlternatePushChannel library helps simplify some of the code around encryption (located in this repository in `src\AlternatePushChannel.Library`).


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

    // Decrypt the content
    string payload = PushManager.GetDecryptedContent(notification);

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
                    },

                    new AdaptiveText()
                    {
                        Text = payload
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


## 9. Pushing to your app from your own server

Follow any online instructions for sending web push notifications from your server's choice of language (the server-side code is identical to web push notifications). For C#, we like the [WebPush NuGet package](https://www.nuget.org/packages/WebPush/) for its simplicity and ease-of-use.

Here's a C# sample using the NuGet package...

```csharp
public static class WebPush
{
    // Keys generated from step #3 (don't store private in public source code)
    // Note that this is the same public key you include in your app in step #4
    private const string PublicKey = "BGg3UxX...";
    private const string PrivateKey = "_RwmE...";

    private static WebPushClient _webPushClient = new WebPushClient();

    public class Subscription
    {
        public string Endpoint { get; set; }
        public SubscriptionKeys Keys { get; set; }
    }

    public class SubscriptionKeys
    {
        public string P256DH { get; set; }
        public string Auth { get; set; }
    }

    public static async Task SendAsync(string subscriptionJson, string payload)
    {
        var subscription = JsonConvert.DeserializeObject<Subscription>(subscriptionJson);

        try
        {
            await _webPushClient.SendNotificationAsync(
                subscription: new PushSubscription(
                    endpoint: subscription.Endpoint,
                    p256dh: subscription.Keys.P256DH,
                    auth: subscription.Keys.Auth),
                payload: payload,
                vapidDetails: new VapidDetails(
                    subject: "mailto:nothanks@microsoft.com",
                    publicKey: PublicKey,
                    privateKey: PrivateKey));
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw ex;
        }
        return;
    }
}
```