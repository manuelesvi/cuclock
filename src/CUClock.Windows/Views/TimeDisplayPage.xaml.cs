﻿using Aphorismus.Shared.Messages;
using CommunityToolkit.Mvvm.Messaging;
using CUClock.Windows.Contracts.Services;
using CUClock.Windows.Core;
using CUClock.Windows.Core.Contracts.Services;
using CUClock.Windows.Helpers;
using CUClock.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CUClock.Windows.Views;

public sealed partial class TimeDisplayPage : Page
{
    public TimeDisplayPage()
    {
        ViewModel = App.GetService<TimeDisplayViewModel>();
        InitializeComponent();
        // ... testing localized resources
        System.Diagnostics.Debug.WriteLine(
            "TimeDisplayPage_AnnounceBtn/Content".GetLocalized());
        var isRegistered = WeakReferenceMessenger.Default
            .IsRegistered<PhrasePickedMessage>(this);
        if (!isRegistered)
        {
            WeakReferenceMessenger.Default.Register<PhrasePickedMessage>(this,
                (_, message) => {
                    var payload = string.Format(
                        "AppNotificationSamplePayload".GetLocalized(),
                        AppContext.BaseDirectory, message.Value.Texto);
                    App.GetService<IAppNotificationService>().Show(payload);
                });
        }
        
    }

    public TimeDisplayViewModel ViewModel
    {
        get;
    }

    private void ToggleSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.MillisecondSwitch = msToggle.IsOn;
    }
}