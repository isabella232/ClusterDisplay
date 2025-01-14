﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Utils;
using Debug = UnityEngine.Debug;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Class responsible to generate multiple <see cref="FrameData"/> messages from a single big
    /// <see cref="FrameDataBuffer"/> and handle retransmission of packets if needed.
    /// </summary>
    class FrameDataSplitter: IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="udpAgent">Object responsible for network access on which we are sending individual fragments of
        /// the whole frame data.</param>
        /// <param name="reuseFrameDataBuffers">Does the <see cref="FrameDataSplitter"/> setup an internal pool to reuse
        /// buffers used to store the frame data?</param>
        /// <param name="retransmitHistory">Number of frames we keep in history to be retransmitted (must be >= 2).</param>
        /// <exception cref="ArgumentException">If retransmitHistory &lt; 2.</exception>
        public FrameDataSplitter(IUdpAgent udpAgent, bool reuseFrameDataBuffers = false, 
        	int retransmitHistory = NetworkingHelpers.DefaultRetransmitHistoryLength)
        {
            if (retransmitHistory < 2)
            {
                throw new ArgumentException("retransmitHistory need to be >= 2.");
            }
            if (!udpAgent.ReceivedMessageTypes.Contains(MessageType.RetransmitFrameData))
            {
                throw new ArgumentException("UdpAgent does not support receiving required MessageType.RetransmitFrameData");
            }

            UdpAgent = udpAgent;
            m_MaxDataPerMessage = UdpAgent.MaximumMessageSize - Marshal.SizeOf<FrameData>();
            if (reuseFrameDataBuffers)
            {
                m_FrameDataBufferPool = new ConcurrentObjectPool<FrameDataBuffer>(
                    () => new FrameDataBuffer(), null, null, buf => buf.Dispose());
            }
            m_SentFramesInformation = new SentFrameInformation[retransmitHistory];
            for (int i = 0; i < retransmitHistory; ++i)
            {
                m_SentFramesInformation[i] = new();
            }
            m_NewestSentFramesInformationIndex = retransmitHistory - 1;

            UdpAgent.AddPreProcess(UdpAgentPreProcessPriorityTable.RetransmitFrameDataProcessing, PreProcessReceivedMessage);
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            // Unregister from pre-processing received messages
            if (UdpAgent != null)
            {
                UdpAgent.RemovePreProcess(PreProcessReceivedMessage);
            }

            // Dispose of FrameDataBuffer we still know about (no one should be using them anyway, so let's
            // proactively dispose of them instead of waiting for GC).
            lock (m_SentFramesInformation)
            {
                for (int sentInformationIndex = 0; sentInformationIndex < m_SentFramesInformation.Length;
                     ++sentInformationIndex)
                {
                    var sentFrameInformation = m_SentFramesInformation[sentInformationIndex];
                    if (sentFrameInformation.DataBuffer != null)
                    {
                        Debug.Assert(sentFrameInformation.DataBuffer.IsValid);
                        DoneOfFrameDataBuffer(sentFrameInformation.DataBuffer);
                        sentFrameInformation.DataBuffer = null;
                    }
                    sentFrameInformation.Dispose();
                }
            }

            m_FrameDataBufferPool?.Clear();
        }

        /// <summary>
        /// Network access object on which we are sending individual fragments of the whole frame data.
        /// </summary>
        public IUdpAgent UdpAgent { get; }

        /// <summary>
        /// Send the specified frame over the network (splitting it in multiple smaller packets that can then be
        /// reassembled).
        /// </summary>
        /// <param name="frameIndex">Index of the frame this message contains information about.</param>
        /// <param name="frameData">The data to be transmitted.  Caller of this method shouldn't reuse it for anything
        /// else until it is returned through the <see cref="ConcurrentObjectPool{T}"/> passed in the constructor.
        /// </param>
        /// <exception cref="ArgumentException">If <paramref name="frameIndex"/> is not equal to the
        /// <paramref name="frameIndex"/> of the previous call + 1.</exception>
        /// <remarks><paramref name="frameData"/> should originate from <see cref="GetNewFrameDataBuffer"/> if not
        /// <c>null</c>.</remarks>
        public void SendFrameData(ulong frameIndex, ref FrameDataBuffer frameData)
        {
            lock (m_SentFramesInformation)
            {
                // First things, let's add an entry to m_SentFramesInformation.
                var previousLast = m_SentFramesInformation[m_NewestSentFramesInformationIndex];
                if (previousLast.DataBuffer != null && previousLast.FrameIndex + 1 != frameIndex)
                {
                    throw new ArgumentException("Non consecutive frameIndex detected, previous one was " +
                        previousLast.FrameIndex + " and the new one is " + frameIndex);
                }

                var newFrameToSend = m_SentFramesInformation[m_OldestSentFramesInformationIndex];
                if (newFrameToSend.DataBuffer != null)
                {
                    Debug.Assert(newFrameToSend.DataBuffer.IsValid); // Otherwise it was disposed of before we were done which is bad...
                    DoneOfFrameDataBuffer(newFrameToSend.DataBuffer);
                }
                newFrameToSend.FrameIndex = frameIndex;
                newFrameToSend.DataBuffer = frameData;
                newFrameToSend.ClearDatagramSentTimestamps();

                m_OldestSentFramesInformationIndex =
                    (m_OldestSentFramesInformationIndex + 1) % m_SentFramesInformation.Length;
                m_NewestSentFramesInformationIndex =
                    (m_NewestSentFramesInformationIndex + 1) % m_SentFramesInformation.Length;
                ++m_SentFrames;

                // We can now send the messages necessary to transmit the complete frameData.
                int nbrDatagrams = frameData.Length / m_MaxDataPerMessage;
                if (frameData.Length % m_MaxDataPerMessage > 0)
                {
                    ++nbrDatagrams;
                }
                for (int datagramIndex = 0; datagramIndex < nbrDatagrams; ++datagramIndex)
                {
                    SendDatagramOf(newFrameToSend, datagramIndex);
                }
            }

            frameData = null;
        }

        /// <summary>
        /// Get a "new" <see cref="FrameDataBuffer"/> to be sent to <see cref="SendFrameData"/>.
        /// </summary>
        /// <remarks>Might be a recycled one, but recycled ones are just as good as new ones, so this is ok :)</remarks>
        public FrameDataBuffer GetNewFrameDataBuffer()
        {
            return m_FrameDataBufferPool?.Get() ?? new FrameDataBuffer();
        }

        /// <summary>
        /// Number of recovered <see cref="FrameDataBuffer"/> that are waiting to be reused on the next
        /// <see cref="GetNewFrameDataBuffer"/> call.
        /// </summary>
        /// <remarks>To be used for debugging more than anything else.</remarks>
        public int InactiveFrameDataBufferCount => m_FrameDataBufferPool?.CountInactive ?? 0;

        /// <summary>
        /// Send the specified datagram.
        /// </summary>
        /// <param name="frameInformation">Information about the frame the datagram is part of.</param>
        /// <param name="datagramIndex">Index of datagram in the sequence of datagrams for that frame.</param>
        void SendDatagramOf(SentFrameInformation frameInformation, int datagramIndex)
        {
            // Check that the datagram has not already been sent "not long ago" to avoid unnecessary retransmission when
            // may repeaters ask for retransmission of the same thing.
            if (Stopwatch.GetTimestamp() < frameInformation.GetDatagramSendTime(datagramIndex) + m_ShortRetransmissionDelayTicks)
            {
                return;
            }

            var frameData = new FrameData()
            {
                FrameIndex = frameInformation.FrameIndex,
                DataLength = frameInformation.DataBuffer.Length,
                DatagramIndex = datagramIndex,
                DatagramDataOffset = m_MaxDataPerMessage * datagramIndex
            };
            int dataToSend = Math.Min(frameInformation.DataBuffer.Length - frameData.DatagramDataOffset,
                m_MaxDataPerMessage);
            UdpAgent.SendMessage(MessageType.FrameData, frameData,
                frameInformation.DataBuffer.DataSpan(frameData.DatagramDataOffset, dataToSend));
            frameInformation.SetDatagramSendTime(datagramIndex, Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Preprocess a received message and check for FrameData retransmission request.
        /// </summary>
        /// <param name="received">Received <see cref="ReceivedMessageBase"/> to preprocess.</param>
        /// <returns>Summary of what happened during the pre-processing.</returns>
        PreProcessResult PreProcessReceivedMessage(ReceivedMessageBase received)
        {
            // We are only interested in retransmit requests, everything else should simply pass through
            if (received.Type != MessageType.RetransmitFrameData)
            {
                return PreProcessResult.PassThrough();
            }

            var retransmitMessage = (ReceivedMessage<RetransmitFrameData>)received;
            Debug.Assert(retransmitMessage != null); // since received.Type == MessageType.FrameData

            lock (m_SentFramesInformation) // Remark, we have to keep the lock for the whole time we are retransmitting
            {                              // as otherwise another thread could move the DataBuffer to UnusedFrameDataBuffers.
                                           // Not ideal, but amount of retransmission should be low, so this is not too bad.

                // Validate frame index range
                ulong oldestFrameIndex = m_SentFramesInformation[m_OldestSentFramesInformationIndex].FrameIndex;
                ulong newestFrameIndex = m_SentFramesInformation[m_NewestSentFramesInformationIndex].FrameIndex;
                if (retransmitMessage.Payload.FrameIndex < oldestFrameIndex ||
                    retransmitMessage.Payload.FrameIndex > newestFrameIndex)
                {
                    // Don't send warning message if client ask retransmission for "the next frame", it might simply be
                    // a little bit faster than us and we should be able to send it shortly...
                    if (retransmitMessage.Payload.FrameIndex != newestFrameIndex + 1)
                    {
                        Debug.LogWarning($"Asking to retransmit a frame for which we currently do not have data " +
                            $"anymore: {retransmitMessage.Payload.FrameIndex}, we only have frames in the range of " +
                            $"[{oldestFrameIndex}, {newestFrameIndex}], skipping.");
                    }
                    return PreProcessResult.Stop();
                }

                // Validate we have data for that frame
                int bufferIndex = (m_OldestSentFramesInformationIndex +
                        (int)(retransmitMessage.Payload.FrameIndex - oldestFrameIndex)) % m_SentFramesInformation.Length;
                if (m_SentFrames < (ulong)m_SentFramesInformation.Length)
                {
                    // Not enough frames have been sent yet and content of m_SentFramesInformation is not totally stable,
                    // search among all entries for one that looks to what we are searching.
                    for (int i = 0; i < m_SentFramesInformation.Length; ++i)
                    {
                        if (m_SentFramesInformation[i].FrameIndex == retransmitMessage.Payload.FrameIndex &&
                            m_SentFramesInformation[i].DataBuffer != null)
                        {
                            bufferIndex = i;
                            break;
                        }
                    }
                }
                var frameDataInformation = m_SentFramesInformation[bufferIndex];
                if (frameDataInformation.DataBuffer == null)
                {
                    // Don't report problem for frame 0, this can happen a lot, especially when delayed repeaters is on.
                    if (retransmitMessage.Payload.FrameIndex > 0)
                    {
                        Debug.LogWarning($"Asking to retransmit a frame for which we have no data.");
                    }
                    return PreProcessResult.Stop();
                }

                // Re-send datagrams
                int nbrDatagrams = frameDataInformation.DataBuffer.Length / m_MaxDataPerMessage;
                if (frameDataInformation.DataBuffer.Length % m_MaxDataPerMessage > 0)
                {
                    ++nbrDatagrams;
                }
                int stopDatagramIndex = Math.Min(retransmitMessage.Payload.DatagramIndexIndexEnd, nbrDatagrams);
                for (int datagramIndex = Math.Max(0, retransmitMessage.Payload.DatagramIndexIndexStart);
                     datagramIndex < stopDatagramIndex; ++datagramIndex)
                {
                    SendDatagramOf(frameDataInformation, datagramIndex);
                    UdpAgent.Stats.SentMessageWasRepeat(MessageType.FrameData);
                }
            }

            return PreProcessResult.Stop();
        }

        /// <summary>
        /// Method called when we are done of a <see cref="FrameDataBuffer"/>.
        /// </summary>
        /// <param name="doneOf"><see cref="FrameDataBuffer"/> to be returned to be re-used or disposed of if no one cares
        /// in re-using.</param>
        void DoneOfFrameDataBuffer(FrameDataBuffer doneOf)
        {
            if (m_FrameDataBufferPool != null)
            {
                m_FrameDataBufferPool.Release(doneOf);
            }
            else
            {
                // Let's make future GV work easier by disposing of it immediately...
                doneOf.Dispose();
            }
        }

        /// <summary>
        /// Information stored about each sent frame that we keep to be able to retransmit parts of in case of a need.
        /// </summary>
        class SentFrameInformation: IDisposable
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public SentFrameInformation()
            {
                m_DatagramSentTimestamp = new long[64]; // 64 datagrams should be enough for most frames
            }

            /// <summary>
            /// Index of the frame this struct contains information about.
            /// </summary>
            public ulong FrameIndex { get; set; }
            /// <summary>
            /// The data that was transmitted.
            /// </summary>
            public FrameDataBuffer DataBuffer { get; set; }

            /// <summary>
            /// Clear the array of when was the datagrams composing this frame sent
            /// </summary>
            public void ClearDatagramSentTimestamps()
            {
                Array.Clear(m_DatagramSentTimestamp, 0, m_DatagramSentTimestamp.Length);
            }

            /// <summary>
            /// Returns when was the last time <paramref name="datagramIndex"/> was sent.
            /// </summary>
            /// <param name="datagramIndex">Index of the datagram.</param>
            /// <returns>When was the last time <paramref name="datagramIndex"/> was sent or <c>0</c> if it was never
            /// sent.</returns>
            public long GetDatagramSendTime(int datagramIndex)
            {
                GrowDatagramSentTimestampArrayIfNeeded(datagramIndex);
                return m_DatagramSentTimestamp[datagramIndex];
            }

            /// <summary>
            /// Sets when was the last time <paramref name="datagramIndex"/> was sent.
            /// </summary>
            /// <param name="datagramIndex">Index of the datagram.</param>
            /// <param name="sendTime">When was the last time <paramref name="datagramIndex"/> was sent.</param>
            public void SetDatagramSendTime(int datagramIndex, long sendTime)
            {
                GrowDatagramSentTimestampArrayIfNeeded(datagramIndex);
                m_DatagramSentTimestamp[datagramIndex] = sendTime;
            }

            public void Dispose()
            {
                Debug.Assert(DataBuffer == null, "DataBuffer should have been returned to owner's FrameDataBufferPool before calling dispose.");
                DataBuffer?.Dispose();
                DataBuffer = null;
            }

            /// <summary>
            /// Ensure <see cref="m_DatagramSentTimestamp"/> contains enough entries to store data for
            /// <paramref name="datagramIndex"/> (and grow the array if it is too small).
            /// </summary>
            /// <param name="datagramIndex">Index of the datagram for which we need to store information in
            /// <see cref="m_DatagramSentTimestamp"/>.</param>
            void GrowDatagramSentTimestampArrayIfNeeded(int datagramIndex)
            {
                if (datagramIndex >= m_DatagramSentTimestamp.Length)
                {
                    var newArray = new long[datagramIndex + 16];
                    Array.Copy(m_DatagramSentTimestamp, newArray, m_DatagramSentTimestamp.Length);
                    Array.Clear(newArray, m_DatagramSentTimestamp.Length,
                        newArray.Length - m_DatagramSentTimestamp.Length);
                    m_DatagramSentTimestamp = newArray;
                }
            }

            /// <summary>
            /// <see cref="Stopwatch.GetTimestamp"/> of when was the datagram corresponding to the index last sent.
            /// </summary>
            long[] m_DatagramSentTimestamp;
        }

        /// <summary>
        /// Maximum amount of frame data that can be sent with each <see cref="FrameData"/> through
        /// <see cref="UdpAgent"/>.
        /// </summary>
        readonly int m_MaxDataPerMessage;
        /// <summary>
        /// Minimum delay between two transmission of the same datagram to avoid unnecessary retransmission of datagrams.
        /// </summary>
        /// <remarks>2 milliseconds</remarks>
        readonly long m_ShortRetransmissionDelayTicks = Stopwatch.Frequency * 2 / 1000;
        /// <summary>
        /// <see cref="ConcurrentObjectPool{T}"/> to which we return the <see cref="FrameDataBuffer"/> when we are done
        /// of them (so they can be re-used).  A null one means that every <see cref="FrameDataBuffer"/> will instead be
        /// disposed of.
        /// </summary>
        readonly ConcurrentObjectPool<FrameDataBuffer> m_FrameDataBufferPool;

        /// <summary>
        /// Frame kept in case we need to retransmit sections of.
        /// </summary>
        /// <remarks>Should always be locked before accessing it or any of the <see cref="SentFrameInformation"/> in it.
        /// </remarks>
        SentFrameInformation[] m_SentFramesInformation;
        /// <summary>
        /// Index of the oldest <see cref="SentFrameInformation"/> in m_SentFramesInformation.
        /// </summary>
        /// <remarks>Should always be access with a lock on <see cref="m_SentFramesInformation"/>.</remarks>
        int m_OldestSentFramesInformationIndex;
        /// <summary>
        /// Index of the most recent <see cref="SentFrameInformation"/> in m_SentFramesInformation.
        /// </summary>
        /// <remarks>Should always be access with a lock on <see cref="m_SentFramesInformation"/>.</remarks>
        int m_NewestSentFramesInformationIndex;
        /// <summary>
        /// How many frames have been sent.
        /// </summary>
        ulong m_SentFrames;
    }
}
