// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Sequences;

namespace System.Text.Formatting
{
    public static class SequenceFormatterExtensions
    {
        public static SequenceFormatter<TSequence> CreateFormatter<TSequence>(this TSequence sequence, SymbolTable symbolTable = null) where TSequence : ISequence<Memory<byte>>
        {
            return new SequenceFormatter<TSequence>(sequence, symbolTable);
        }
    }

    public class SequenceFormatter<TSequence> : ITextOutput where TSequence : ISequence<Memory<byte>>
    {
        ISequence<Memory<byte>> _buffers;
        SymbolTable _symbolTable;

        SequencePosition _currentSequencePosition = default;
        int _currentWrittenBytes;
        SequencePosition _previousSequencePosition = default;
        int _previousWrittenBytes;
        int _totalWritten;

        public SequenceFormatter(TSequence buffers, SymbolTable symbolTable)
        {
            _symbolTable = symbolTable;
            _buffers = buffers;
            _currentSequencePosition = _buffers.Start;
            _previousWrittenBytes = -1;
        }

        private Memory<byte> Current
        {
            get
            {
                if (!_buffers.TryGet(ref _currentSequencePosition, out Memory<byte> result, advance: false)) { throw new InvalidOperationException(); }
                return result;
            }
        }
        private Memory<byte> Previous
        {
            get
            {
                if (!_buffers.TryGet(ref _previousSequencePosition, out Memory<byte> result, advance: false)) { throw new InvalidOperationException(); }
                return result;
            }
        }
        private bool NeedShift => _previousWrittenBytes != -1;

        SymbolTable ITextOutput.SymbolTable => _symbolTable;

        public int TotalWritten => _totalWritten;

        Memory<byte> IOutput.GetMemory(int minimumLength)
        {
            if (minimumLength == 0) minimumLength = 1;
            if (minimumLength > Current.Length - _currentWrittenBytes)
            {
                if (NeedShift) throw new NotImplementedException("need to allocate temp array");

                _previousSequencePosition = _currentSequencePosition;
                _previousWrittenBytes = _currentWrittenBytes;

                if (!_buffers.TryGet(ref _currentSequencePosition, out Memory<byte> span))
                {
                    throw new InvalidOperationException();
                }
                _currentWrittenBytes = 0;
            }
            return Current.Slice(_currentWrittenBytes);
        }

        Span<byte> IOutput.GetSpan(int minimumLength) => ((IOutput)this).GetMemory(minimumLength).Span;

        void IOutput.Advance(int bytes)
        {
            var current = Current;
            if (NeedShift)
            {
                var previous = Previous;
                var spaceInPrevious = previous.Length - _previousWrittenBytes;
                if (spaceInPrevious < bytes)
                {
                    current.Slice(0, spaceInPrevious).Span.CopyTo(previous.Span.Slice(_previousWrittenBytes));
                    current.Slice(spaceInPrevious, bytes - spaceInPrevious).Span.CopyTo(current.Span);
                    _previousWrittenBytes = -1;
                    _currentWrittenBytes = bytes - spaceInPrevious;
                }
                else
                {
                    current.Slice(0, bytes).Span.CopyTo(previous.Span.Slice(_previousWrittenBytes));
                    _currentSequencePosition = _previousSequencePosition;
                    _currentWrittenBytes = _previousWrittenBytes + bytes;
                }

            }
            else
            {
                if (current.Length - _currentWrittenBytes < bytes) throw new NotImplementedException();
                _currentWrittenBytes += bytes;
            }

            _totalWritten += bytes;
        }
    }
}
