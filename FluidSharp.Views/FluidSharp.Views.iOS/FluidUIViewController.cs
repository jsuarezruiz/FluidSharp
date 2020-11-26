﻿using CoreGraphics;
using FluidSharp.Layouts;
using FluidSharp.Views.iOS;
using FluidSharp.Views.iOS.Services;
using Foundation;
using System;
using System.Threading.Tasks;
using UIKit;

namespace FluidSharp.Views.iOS
{
    public class FluidUIViewController : UIViewController
    {

        public readonly IWidgetSource WidgetSource;
        private FluidWidgetView FluidWidgetView;

        private KeyboardTracker KeyboardTracker;
        private nfloat OriginalInsetBottom;

        private Action<Margins> OnDeviceMarginsChanged;

        public FluidUIViewController(IWidgetSource widgetSource, Action<Margins> onDeviceMarginsChanged)
        {
            WidgetSource = widgetSource;
            OnDeviceMarginsChanged = onDeviceMarginsChanged;
            KeyboardTracker = new KeyboardTracker(h =>
            {
                if (h == 0)
                {
                    AdditionalSafeAreaInsets = new UIEdgeInsets(0, 0, 0, 0);
                }
                else
                {
                    if (AdditionalSafeAreaInsets.Bottom == 0)
                        OriginalInsetBottom = View.SafeAreaInsets.Bottom;
                    AdditionalSafeAreaInsets = new UIEdgeInsets(0, 0, h - OriginalInsetBottom, 0);
                }
                View.LayoutIfNeeded();
            });
        }

        public override void ViewDidLayoutSubviews() => Task.Run(RequestRedraw);

        public Task RequestRedraw() => FluidWidgetView.VisualState.RequestRedraw();

        public override void LoadView()
        {
            FluidWidgetView = new FluidWidgetView();
            FluidWidgetView.WidgetSource = WidgetSource;
            View = FluidWidgetView;
        }

        public override void ViewWillAppear(bool animated) => KeyboardTracker.RegisterForKeyboardNotifications();
        public override void ViewWillDisappear(bool animated) => KeyboardTracker.UnregisterForKeyboardNotifications();

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            // Perform any additional setup after loading the view, typically from a nib.
        }

        public override void DidReceiveMemoryWarning()
        {
            base.DidReceiveMemoryWarning();
            // Release any cached data, images, etc that aren't in use.
        }

        public override void ViewSafeAreaInsetsDidChange()
        {
            var sai = View.SafeAreaInsets;
            OnDeviceMarginsChanged(new Margins((float)sai.Left, (float)sai.Top, (float)sai.Right, (float)sai.Bottom));
            base.ViewSafeAreaInsetsDidChange();
        }


    }
}