﻿using FluidSharp.Layouts;
using FluidSharp.Widgets;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace FluidSharp.Animations
{

    public class Frame : Animation
    {

        // Frame
        public string Name { get; set; }
        public TimeSpan FrameStart { get; set; }
        public TimeSpan FrameEnd => FrameStart + Duration;

        public TimeSpan GetDurationPct(float pct) => FrameStart + Duration * pct;

        public Frame(string name, float durationMS, Easing? easing = null)
            : this(Speed, name, TimeSpan.Zero, TimeSpan.FromMilliseconds(durationMS), easing)
        { }

        public Frame(string name, float startMS, float durationMS, Easing? easing = null)
            : this(Speed, name, TimeSpan.FromMilliseconds(startMS), TimeSpan.FromMilliseconds(durationMS), easing)
        { }

        public Frame(string name, TimeSpan start, TimeSpan duration, Easing? easing = null)
            : this(Speed, name, start, duration, easing)
        {
        }

        private Frame(float speed, string name, TimeSpan start, TimeSpan duration, Easing? easing = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            FrameStart = start * speed;
            Duration = duration * speed;
            Easing = easing;
        }

        public Frame Reverse()
        {
            StartValue = 1; EndValue = 0;
            return this;
        }

        public Frame SetStartEnd(float start, float end)
        {
            StartValue = start;
            EndValue = end;
            return this;
        }

    }

    public class State
    {

        public string Name { get; set; }

        public Animation.Coordinated Animation = new Animation.Coordinated(DateTime.Now);

        public State(string name) => Name = name;

        public bool Started { get; set; }
        public bool Completed => Started && Animation.Completed;

        public Animation.Coordinated? AnimationIfStarted => Started ? Animation : null;

        public void Reset()
        {
            Started = false;
        }

    }

    public class StateAnimation : Animation
    {

        public List<State> States { get; set; } = new List<State>();

        public State? CurrentState { get; private set; }

        public State Add(State state)
        {
            States.Add(state);
            return state;
        }

        public void StartState(State state)
        {
            state.Animation.Start();
            CurrentState = state;
            state.Started = true;
        }

    }



    public class Animation
    {

        public static Animation CompletedAnimation = new Animation(DateTime.MinValue, DefaultDuration);

        public static TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(250);
        public static float Speed = 1f;

        public DateTime StartTime { get; set; }
        public DateTime EndTime => StartTime + Duration;

        public TimeSpan Duration { get; set; }

        public float StartValue { get; set; } = 0;
        public float EndValue { get; set; } = 1;
        public float Delta => EndValue - StartValue;

        public Easing? Easing { get; set; }

        public bool Started => GetPctComplete() > 0;
        private bool completedfired;
        public bool Completed => GetPctComplete() >= 1;
        public Func<Task>? OnCompleted;

        protected Animation() { }

        public virtual void Start()
        {
            StartTime = DateTime.Now;
        }

        public virtual void Stop()
        {
            StartTime = DateTime.Now.Add(-Duration * 2);
        }

        public Animation(DateTime startTime, TimeSpan duration, float start = 0, float end = 1, Easing? easing = null, Func<Task>? onCompleted = null)
        {
            StartTime = startTime;
            Duration = duration;
            StartValue = start;
            EndValue = end;
            Easing = easing;
            OnCompleted = onCompleted;
        }

        public Animation Inverse() => new Animation(StartTime, Duration, EndValue, StartValue, Easing, OnCompleted);

        public Animation MakeOffset(float pct)
        {
            return new Animation(StartTime + Duration * pct, Duration, StartValue, EndValue, Easing);
        }

        private double GetPctComplete()
        {
            var pct = DateTime.Now.Subtract(StartTime).TotalMilliseconds / Duration.TotalMilliseconds;
            if (pct < 0) pct = 0;
            if (pct >= 1)
            {
                pct = 1;
                if (!completedfired && OnCompleted != null)
                {
                    completedfired = true;
                    Task.Run(() => OnCompleted());
                }
            }
            return pct;
        }

        public float GetValue()
        {
            var value = (float)(StartValue + GetPctComplete() * Delta);
            if (Easing != null) value = Easing.Ease(value);
            return value;
        }

        public static Widget Wrap<T>(Animation? animation, Func<Animation, T, Widget> wrap, T contents)
            where T : Widget
        {
            if (animation == null) return contents;
            return wrap(animation, contents);
        }

        public static Widget Wrap<T>(Animation? animation1, Func<Animation, T, Widget> wrap1, Animation? animation2, Func<Animation, T, Widget> wrap2, T contents)
            where T : Widget
        {
            if (animation1 != null)
                return wrap1(animation1, contents);
            else if (animation2 != null)
                return wrap2(animation2, contents);
            else
                return contents;
        }

        #region Stagger calculation

        public static float Stagger(int part, int partcount, int overlappingpartcount, float pct)
        {

            if (overlappingpartcount < 0) throw new ArgumentOutOfRangeException(nameof(overlappingpartcount));
            if (part > partcount - 1) throw new ArgumentOutOfRangeException(nameof(part));

            // 0               1
            // partcount: 2, overlappingpartcount: 1
            // --------- 
            //          --------

            // partcount: 2, overlappingpartcount: 2
            // ----------
            //      ------------

            // partcount: 2, overlappingpartcount: 3
            // -----------
            //     -------------

            // partcount: 3, overlappingpartcount: 2
            // --------
            //      --------
            //          --------


            // partcount: 7, overlappingpartcount: 3
            // ------
            //   ------
            //     ------
            //       ------
            //         ------
            //           ------
            //             ------

            var totalsegments = 1f + (float)(partcount - 1) / overlappingpartcount;
            var segmentlength = 1f / totalsegments;
            var delta = segmentlength / overlappingpartcount;

            var position = (pct - delta * part) / segmentlength;
            if (position < 0) return 0;
            if (position > 1) return 1;
            return position;

        }

        #endregion

        public class Coordinated : Animation
        {

            private Dictionary<string, Frame> Frames = new Dictionary<string, Frame>();

            public Coordinated(DateTime startTime, params Frame[] frames)
            {
                StartTime = startTime;
                foreach (var frame in frames)
                    AddFrame(frame);
            }

            public Frame AddFrame(Frame frame)
            {
                //                if (frame.FrameStart == TimeSpan.Zero)
                frame.FrameStart = frame.FrameStart + Duration;
                return NewFrame(frame);
            }

            public Frame NewFrame(Frame frame)
            {
                frame.StartTime = StartTime.Add(frame.FrameStart);
                var minduration = frame.FrameEnd;
                if (Duration < minduration) Duration = minduration;
                Frames.Add(frame.Name, frame);
                return frame;
            }

            public Frame AddFrame(string name, string startframe, float pctstartduration, string endframe, float pctendduration, Easing? easing = null)
            {
                var t1 = this[startframe].GetDurationPct(pctstartduration) / Speed;
                var t2 = this[endframe].GetDurationPct(pctendduration) / Speed;
                return NewFrame(new Frame(name, t1, t2 - t1, easing));
            }

            public Frame this[string name] => Frames[name];

            public bool TryGetFrame(string name, out Frame frame) => Frames.TryGetValue(name, out frame);

            public void ScaleTo(TimeSpan duration)
            {
                var scale = duration / Duration;
                foreach (var frame in Frames.Values)
                {
                    frame.FrameStart *= scale;
                    frame.Duration *= scale;
                    frame.StartTime = StartTime.Add(frame.FrameStart);
                }
                Duration = duration;
            }

            public void AddFrames(TimeSpan start, Coordinated coordinated)
            {
                foreach (var frame in coordinated.Frames.Values)
                {
                    frame.FrameStart += start;
                    NewFrame(frame);
                }
            }

            public override void Start()
            {
                StartTime = DateTime.Now;
                foreach (var frame in Frames.Values)
                    frame.StartTime = StartTime.Add(frame.FrameStart);
            }

            public override void Stop()
            {
                StartTime = DateTime.Now.AddHours(-1);
                foreach (var frame in Frames.Values)
                    frame.StartTime = StartTime;
            }

            public void MoveTo(string name, bool allowmoveback)
            {
                var framestart = Frames[name].FrameStart;
                var time = DateTime.Now.Add(-framestart);
                if (!allowmoveback && time > StartTime) return;

                StartTime = time;
                foreach (var frame in Frames.Values)
                    frame.StartTime = StartTime.Add(frame.FrameStart);
            }

        }

    }
}
