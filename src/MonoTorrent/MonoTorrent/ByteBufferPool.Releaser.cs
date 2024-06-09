﻿//
// ByteBufferPool.Releaser.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;

namespace MonoTorrent
{
    public partial class ByteBufferPool
    {
        public readonly struct Releaser : IDisposable
        {
            readonly int Counter;
            readonly ByteBuffer Buffer;
            readonly ByteBufferPool Pool;

            internal Releaser (ByteBufferPool pool, ByteBuffer buffer)
            {
                Pool = pool;
                Buffer = buffer;
                Counter = Buffer.Counter;
            }

            public void Dispose ()
            {
                if (Buffer == null)
                    return;

                if (Counter != Buffer.Counter)
                    throw new InvalidOperationException ("This buffer has been double-freed, which implies it was used after a previews free.");

                Buffer.Counter++;

                var size = Buffer.Segment.Count;
                if (size == ByteBufferPool.SmallMessageBufferSize) {
                    using (Pool.SmallMessageBuffersLock.Enter ())
                        Pool.SmallMessageBuffers.Push (Buffer);
                } else if (size == ByteBufferPool.LargeMessageBufferSize) {
                    using (Pool.LargeMessageBuffersLock.Enter ())
                        Pool.LargeMessageBuffers.Push (Buffer);
                } else {
                    using (Pool.MassiveBuffersLock.Enter ())
                        Pool.MassiveBuffers.Enqueue (Buffer);
                }
            }
        }
    }
}
