﻿using FluidSharp.Layouts;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluidSharp.Widgets.Native
{
    public abstract class NativeViewWidget : Widget
    {

        public override SKSize Measure(MeasureCache measureCache, SKSize boundaries)
        {
            if (measureCache.NativeViewManager == null)
            {
                Console.WriteLine($"Warning: NativeViewManager is null measuring NativeViewWidget {this.GetType().Name}");
                return new SKSize();
            }
            var childsize = measureCache.NativeViewManager.Measure(this, boundaries);
            return childsize;
        }

        public override SKRect PaintInternal(LayoutSurface layoutsurface, SKRect rect)
        {
            if (layoutsurface.Canvas != null)
            {
                if (layoutsurface.MeasureCache.NativeViewManager == null)
                {
                    Console.WriteLine($"Warning: NativeViewManager is null updating NativeViewWidget {this.GetType().Name}");
                }
                else
                {
                    layoutsurface.MeasureCache.NativeViewManager.UpdateNativeView(this, rect);
                }
            }
            return rect;
        }

    }
}
