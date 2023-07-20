using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace MediFiler_V2.Code.Utilities;

public sealed class CustomMediaTransportControls :  MediaTransportControls
{
    // ...

    protected override void OnApplyTemplate()
    {
        // Find the custom button and create an event handler for its Click event.
        Debug.WriteLine("HELLO VIDEO");
        //var muteButton = GetTemplateChild("MuteButton") as Button;
        //muteButton.Click += MuteButton_Click;
        //base.OnApplyTemplate();
    }

    //...
}