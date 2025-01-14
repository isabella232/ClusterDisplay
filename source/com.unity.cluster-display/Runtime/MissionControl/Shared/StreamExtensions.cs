﻿using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl
{
    public static class StreamExtensions
    {
        /// <summary>
        /// As <see cref="PipeStream.ReadAsync(byte[],int,int,System.Threading.CancellationToken)"/>
        /// </summary>
        /// <param name="stream">Extended object.</param>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <param name="offset">The byte offset in buffer at which to begin writing data from the stream.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is
        /// <see cref="CancellationToken.None"/>.</param>
        /// <returns>Have we been able to read <paramref name="count"/> bytes into <paramref name="buffer"/>?</returns>
        public static async ValueTask<bool> ReadAllBytesAsync(this Stream stream, byte[] buffer, int offset,
            int count, CancellationToken cancellationToken)
        {
            while (count > 0)
            {
                int read = await stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                offset += read;
                count -= read;
            }

            return true;
        }

        /// <summary>
        /// As <see cref="PipeStream.ReadAsync(byte[],int,int,System.Threading.CancellationToken)"/>
        /// </summary>
        /// <param name="stream">Extended object.</param>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <param name="offset">The byte offset in buffer at which to begin writing data from the stream.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>Have we been able to read <paramref name="count"/> bytes into <paramref name="buffer"/>?</returns>
        public static ValueTask<bool> ReadAllBytesAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            return stream.ReadAllBytesAsync(buffer, offset, count, CancellationToken.None);
        }

        /// <summary>
        /// Read the given struct from the stream.
        /// </summary>
        /// <param name="stream">Extended object.</param>
        /// <param name="buffer">Temporary byte array used during the process.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is
        /// <see cref="CancellationToken.None"/>.</param>
        /// <typeparam name="T">Type of struct to read from the stream.</typeparam>
        // ReSharper disable once MemberCanBePrivate.Global
        public static async ValueTask<T?> ReadStructAsync<T>(this Stream stream, byte[] buffer,
            CancellationToken cancellationToken) where T: struct
        {
            int sizeOfStruct = Marshal.SizeOf<T>();
            Debug.Assert(buffer.Length >= sizeOfStruct);
            if (await stream.ReadAllBytesAsync(buffer, 0, sizeOfStruct, cancellationToken).ConfigureAwait(false))
            {
                return MemoryMarshal.Read<T>(buffer);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Read the given struct from the stream.
        /// </summary>
        /// <param name="stream">Extended object.</param>
        /// <param name="buffer">Temporary byte array used during the process.</param>
        /// <typeparam name="T">Type of struct to read from the stream.</typeparam>
        public static ValueTask<T?> ReadStructAsync<T>(this Stream stream, byte[] buffer) where T: struct
        {
            return stream.ReadStructAsync<T>(buffer, CancellationToken.None);
        }

        /// <summary>
        /// Read the length of <paramref name="buffer"/> worth bytes.
        /// </summary>
        /// <param name="stream">Stream from which we read.</param>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <returns>Have we been able to fill <paramref name="buffer"/>?</returns>
        public static bool ReadAllBytes(this Stream stream, Span<byte> buffer)
        {
            int count = buffer.Length;
            int offset = 0;
            while (count > 0)
            {
                int read = stream.Read(buffer.Slice(offset, count));
                if (read == 0)
                {
                    return false;
                }

                offset += read;
                count -= read;
            }

            return true;
        }

        /// <summary>
        /// Read the given struct from the stream.
        /// </summary>
        /// <param name="stream">Extended object.</param>
        /// <typeparam name="T">Type of struct to read from the stream.</typeparam>
        public static T ReadStruct<T>(this Stream stream) where T: struct
        {
            var outVal = new T();
            var span = MemoryMarshal.CreateSpan(ref outVal, 1);
            var buffer = MemoryMarshal.AsBytes(span);
            if (!stream.ReadAllBytes(buffer))
            {
                throw new InvalidOperationException("Failed to read from stream.");
            }
            return outVal;
        }

        /// <summary>
        /// Write the given struct from the stream.
        /// </summary>
        /// <param name="stream">Extended object.</param>
        /// <param name="toWrite">Struct to write.</param>
        /// <param name="buffer">Temporary byte array used during the process.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is
        /// <see cref="CancellationToken.None"/>.</param>
        /// <typeparam name="T">Type of struct to write to the stream.</typeparam>
        // ReSharper disable once MemberCanBePrivate.Global
        public static ValueTask WriteStructAsync<T>(this Stream stream, T toWrite, byte[] buffer,
            CancellationToken cancellationToken) where T: struct
        {
            int sizeOfStruct = Marshal.SizeOf<T>();
            Debug.Assert(buffer.Length >= sizeOfStruct);
            MemoryMarshal.Write(buffer, ref toWrite);
            return stream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, sizeOfStruct), cancellationToken);
        }

        /// <summary>
        /// Write the given struct from the stream.
        /// </summary>
        /// <param name="stream">Extended object.</param>
        /// <param name="toWrite">Struct to write.</param>
        /// <param name="buffer">Temporary byte array used during the process.</param>
        /// <typeparam name="T">Type of struct to write to the stream.</typeparam>
        public static ValueTask WriteStructAsync<T>(this Stream stream, T toWrite, byte[] buffer) where T: struct
        {
            return stream.WriteStructAsync(toWrite, buffer, CancellationToken.None);
        }

        /// <summary>
        /// Write the given struct from the stream.
        /// </summary>
        /// <param name="stream">Extended object.</param>
        /// <param name="toWrite">Struct to write.</param>
        /// <typeparam name="T">Type of struct to write to the stream.</typeparam>
        public static void WriteStruct<T>(this Stream stream, T toWrite) where T: struct
        {
            var span = MemoryMarshal.CreateSpan(ref toWrite, 1);
            var buffer = MemoryMarshal.AsBytes(span);
            stream.Write(buffer);
        }
    }
}
