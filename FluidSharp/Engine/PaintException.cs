﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace FluidSharp.Engine
{
    public class PaintException : Exception
    {

        private IDictionary Details;
        public override IDictionary Data => Details;

        public PaintException(string message, Exception innerException) : base(message, innerException)
        {
            Details = innerException?.Data;
        }

        public PaintException(string message, Exception innerException, Dictionary<string, string> details) : base(message, innerException)
        {
            Details = details ?? innerException?.Data;
        }

    }
}
