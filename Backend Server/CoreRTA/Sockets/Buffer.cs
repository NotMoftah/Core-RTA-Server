using System.Diagnostics;
using System.Text;
using System;


namespace CoreRTA
{

    public class Buffer
    {
        private long size;
        private long offset;
        private byte[] buffer;

        // Is the buffer empty?
        public bool IsEmpty => (buffer == null) || (size == 0);
        // Bytes memory buffer
        public byte[] Data => buffer;
        // Bytes memory buffer capacity
        public long Capacity => buffer.Length;
        // Bytes memory buffer size
        public long Size => size;
        // Bytes memory buffer offset
        public long Offset => offset;

        // Buffer indexer operator
        public byte this[int index] => buffer[index];

        // Initialize a new expandable buffer with zero capacity
        public Buffer() { buffer = new byte[0]; size = 0; offset = 0; }
        // Initialize a new expandable buffer with the given capacity
        public Buffer(long capacity) { buffer = new byte[capacity]; size = 0; offset = 0; }
        // Initialize a new expandable buffer with the given data
        public Buffer(byte[] data) { this.buffer = data; size = data.Length; offset = 0; }

        #region Memory buffer methods

        // Get string from the current buffer
        public override string ToString()
        {
            return ExtractString(0, size);
        }

        // Clear the current buffer and its offset
        public void Clear()
        {
            size = 0;
            offset = 0;
        }

        // Extract the string from buffer of the given offset and size
        public string ExtractString(long offset, long size)
        {
            Debug.Assert(((offset + size) <= Size), "Invalid offset & size!");
            if ((offset + size) > Size)
                throw new ArgumentException("Invalid offset & size!", nameof(offset));

            return Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
        }

        // Remove the buffer of the given offset and size
        public void Remove(long offset, long size)
        {
            Debug.Assert(((offset + size) <= Size), "Invalid offset & size!");
            if ((offset + size) > Size)
                throw new ArgumentException("Invalid offset & size!", nameof(offset));

            Array.Copy(buffer, offset + size, buffer, offset, this.size - size - offset);
            this.size -= size;
            if (this.offset >= (offset + size))
                this.offset -= size;
            else if (this.offset >= offset)
            {
                this.offset -= this.offset - offset;
                if (this.offset > Size)
                    this.offset = Size;
            }
        }

        // Reserve the buffer of the given capacity
        public void Reserve(long capacity)
        {
            Debug.Assert((capacity >= 0), "Invalid reserve capacity!");
            if (capacity < 0)
                throw new ArgumentException("Invalid reserve capacity!", nameof(capacity));

            if (capacity > Capacity)
            {
                byte[] data = new byte[Math.Max(capacity, 2 * Capacity)];
                Array.Copy(this.buffer, 0, data, 0, size);
                this.buffer = data;
            }
        }

        // Resize the current buffer
        public void Resize(long size)
        {
            Reserve(size);
            this.size = size;
            if (offset > this.size)
                offset = this.size;
        }

        #endregion

        #region Buffer I/O methods

        // Append the given buffer
        public long Append(byte[] buffer)
        {
            Reserve(size + buffer.Length);
            Array.Copy(buffer, 0, this.buffer, size, buffer.Length);
            size += buffer.Length;
            return buffer.Length;
        }

        // Append the given buffer fragment
        public long Append(byte[] buffer, long offset, long size)
        {
            Reserve(this.size + size);
            Array.Copy(buffer, offset, this.buffer, this.size, size);
            this.size += size;
            return size;
        }

        // Append the given text in UTF-8 encoding
        public long Append(string text)
        {
            Reserve(size + Encoding.UTF8.GetMaxByteCount(text.Length));
            long result = Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, (int)size);
            size += result;
            return result;
        }

        #endregion
    }
}
