// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.ExceptionServices;
using System.Threading;

namespace System.IO.Pipes
{
    internal sealed class ReadWriteCompletionSource : PipeCompletionSource<int>
    {
        private readonly bool _isWrite;
        private readonly PipeStream _pipeStream;

        private bool _isMessageComplete;
        private int _numBytes; // number of buffer read OR written

        internal ReadWriteCompletionSource(PipeStream stream, ReadOnlyMemory<byte> bufferToPin, bool isWrite)
            : base(stream._threadPoolBinding!, bufferToPin)
        {
            _pipeStream = stream;
            _isWrite = isWrite;
            _isMessageComplete = true;
        }

        internal override void SetCompletedSynchronously()
        {
            if (!_isWrite)
            {
                _pipeStream.UpdateMessageCompletion(_isMessageComplete);
            }

            TrySetResult(_numBytes);
        }

        protected override void AsyncCallback(uint errorCode, uint numBytes)
        {
            _numBytes = (int)numBytes;

            // Allow async read to finish
            if (!_isWrite)
            {
                switch (errorCode)
                {
                    case Interop.Errors.ERROR_BROKEN_PIPE:
                    case Interop.Errors.ERROR_PIPE_NOT_CONNECTED:
                    case Interop.Errors.ERROR_NO_DATA:
                        errorCode = 0;
                        break;
                }
            }

            // For message type buffer.
            if (errorCode == Interop.Errors.ERROR_MORE_DATA)
            {
                errorCode = 0;
                _isMessageComplete = false;
            }
            else
            {
                _isMessageComplete = true;
            }

            base.AsyncCallback(errorCode, numBytes);
        }

        protected override void HandleError(int errorCode) =>
            TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(_pipeStream.WinIOError(errorCode)));
    }
}
