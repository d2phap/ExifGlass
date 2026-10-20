/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2026 DUONG DIEU PHAP
Project homepage: https://github.com/d2phap/ExifGlass

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using System.Buffers;

namespace ExifGlass.Core.ExifTool;

/// <summary>
/// The pipe's segment pool: keeps a few segments for the usual small replies and lets the GC take the rest of a large one.
/// </summary>
internal sealed class BoundedMemoryPool(int segmentSize, int maxKept) : MemoryPool<byte>
{
    #region Fields and constants

    private readonly Lock _lock = new();
    private readonly Stack<byte[]> _kept = new();

    #endregion // Fields and constants


    #region Properties

    public override int MaxBufferSize => int.MaxValue;

    #endregion // Properties


    #region Public methods

    // MemoryPool<byte>.Shared would keep every segment of a 2 MB reply: ~22 MB per thread that ever read one (§8 heap)
    public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
    {
        var size = Math.Max(minBufferSize, segmentSize);
        if (size == segmentSize)
        {
            lock (_lock)
            {
                if (_kept.TryPop(out var array)) return new Owner(this, array);
            }
        }

        return new Owner(this, new byte[size]);
    }

    #endregion // Public methods


    #region Protected and private methods

    protected override void Dispose(bool disposing)
    {
        lock (_lock) _kept.Clear();
    }


    private void Return(byte[] array)
    {
        if (array.Length != segmentSize) return;

        lock (_lock)
        {
            if (_kept.Count < maxKept) _kept.Push(array);
        }
    }

    #endregion // Protected and private methods


    #region Nested types

    private sealed class Owner(BoundedMemoryPool pool, byte[] array) : IMemoryOwner<byte>
    {
        private byte[]? _array = array;

        public Memory<byte> Memory => _array ?? throw new ObjectDisposedException(nameof(Owner));

        public void Dispose()
        {
            var array = Interlocked.Exchange(ref _array, null);
            if (array is not null) pool.Return(array);
        }
    }

    #endregion // Nested types
}
