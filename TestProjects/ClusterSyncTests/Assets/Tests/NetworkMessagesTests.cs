﻿using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Unity.ClusterDisplay.Tests
{
    public class NetworkMessagesTests
    {
        [Test]
        public void RegisteringWithEmitter()
        {
            int sizeOfStruct = Marshal.SizeOf<RegisteringWithEmitter>();
            Assert.That(sizeOfStruct, Is.EqualTo(5));

            var testStruct = new RegisteringWithEmitter
            {
                NodeId = 42,
                IPAddressBytes = 0x12345678
            };
            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<RegisteringWithEmitter>(byteArray);

            Assert.That(roundTrip.NodeId, Is.EqualTo(testStruct.NodeId));
            Assert.That(roundTrip.IPAddressBytes, Is.EqualTo(testStruct.IPAddressBytes));
        }

        [Test]
        public void RepeaterRegistered()
        {
            int sizeOfStruct = Marshal.SizeOf<RepeaterRegistered>();
            Assert.That(sizeOfStruct, Is.EqualTo(6));

            var testStruct = new RepeaterRegistered
            {
                NodeId = 42,
                IPAddressBytes = 0x12345678,
                Accepted = true
            };
            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<RepeaterRegistered>(byteArray);

            Assert.That(roundTrip.NodeId, Is.EqualTo(testStruct.NodeId));
            Assert.That(roundTrip.IPAddressBytes, Is.EqualTo(testStruct.IPAddressBytes));
            Assert.That(roundTrip.Accepted, Is.EqualTo(testStruct.Accepted));
        }

        [Test]
        public void FrameData()
        {
            int sizeOfStruct = Marshal.SizeOf<FrameData>();
            Assert.That(sizeOfStruct, Is.EqualTo(20));

            var testStruct = new FrameData
            {
                FrameIndex = 0x1234567887654321,
                DataLength = 0x76544367,
                DatagramIndex = 0x65433456,
                DatagramDataOffset = 0x54322345
            };
            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<FrameData>(byteArray);

            Assert.That(roundTrip.FrameIndex, Is.EqualTo(testStruct.FrameIndex));
            Assert.That(roundTrip.DataLength, Is.EqualTo(testStruct.DataLength));
            Assert.That(roundTrip.DatagramIndex, Is.EqualTo(testStruct.DatagramIndex));
            Assert.That(roundTrip.DatagramDataOffset, Is.EqualTo(testStruct.DatagramDataOffset));
        }

        [Test]
        public void RetransmitFrameData()
        {
            int sizeOfStruct = Marshal.SizeOf<RetransmitFrameData>();
            Assert.That(sizeOfStruct, Is.EqualTo(16));

            var testStruct = new RetransmitFrameData
            {
                FrameIndex = 0x2345678998765432,
                DatagramIndexIndexStart = 0x65433456,
                DatagramIndexIndexEnd = 0x76544367
            };
            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<RetransmitFrameData>(byteArray);

            Assert.That(roundTrip.FrameIndex, Is.EqualTo(testStruct.FrameIndex));
            Assert.That(roundTrip.DatagramIndexIndexStart, Is.EqualTo(testStruct.DatagramIndexIndexStart));
            Assert.That(roundTrip.DatagramIndexIndexEnd, Is.EqualTo(testStruct.DatagramIndexIndexEnd));
        }

        [Test]
        public void RepeaterWaitingToStartFrame()
        {
            int sizeOfStruct = Marshal.SizeOf<RepeaterWaitingToStartFrame>();
            Assert.That(sizeOfStruct, Is.EqualTo(10));

            var testStruct = new RepeaterWaitingToStartFrame
            {
                FrameIndex = 0x3456789009876543,
                NodeId = 251,
                WillUseNetworkSyncOnNextFrame = true
            };
            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<RepeaterWaitingToStartFrame>(byteArray);

            Assert.That(roundTrip.FrameIndex, Is.EqualTo(testStruct.FrameIndex));
            Assert.That(roundTrip.NodeId, Is.EqualTo(testStruct.NodeId));
            Assert.That(roundTrip.WillUseNetworkSyncOnNextFrame, Is.EqualTo(testStruct.WillUseNetworkSyncOnNextFrame));
        }

        [Test]
        public void EmitterWaitingToStartFrame()
        {
            int sizeOfStruct = Marshal.SizeOf<EmitterWaitingToStartFrame>();
            Assert.That(sizeOfStruct, Is.EqualTo(5 * 8));

            var testStruct = new EmitterWaitingToStartFrame
            {
                FrameIndex = 0x4567890110987654,
            };
            testStruct.WaitingOn0 = 0x5678901221098765;
            testStruct.WaitingOn1 = 0x6789012332109876;
            testStruct.WaitingOn2 = 0x7890123443210987;
            testStruct.WaitingOn3 = 0x8901234554321098;

            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<EmitterWaitingToStartFrame>(byteArray);

            Assert.That(roundTrip.FrameIndex, Is.EqualTo(testStruct.FrameIndex));
            Assert.That(roundTrip.WaitingOn0, Is.EqualTo(testStruct.WaitingOn0));
            Assert.That(roundTrip.WaitingOn1, Is.EqualTo(testStruct.WaitingOn1));
            Assert.That(roundTrip.WaitingOn2, Is.EqualTo(testStruct.WaitingOn2));
            Assert.That(roundTrip.WaitingOn3, Is.EqualTo(testStruct.WaitingOn3));
        }

        [Test]
        public void PropagateQuit()
        {
            int sizeOfStruct = Marshal.SizeOf<PropagateQuit>();
            Assert.That(sizeOfStruct, Is.EqualTo(1)); // It is empty but it looks like size is a minimum of 1

            var testStruct = new PropagateQuit();

            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<PropagateQuit>(byteArray);

            Assert.That(roundTrip, Is.Not.Null);
            Assert.That(roundTrip, Is.TypeOf<PropagateQuit>());
        }

        [Test]
        public void QuitReceived()
        {
            int sizeOfStruct = Marshal.SizeOf<QuitReceived>();
            Assert.That(sizeOfStruct, Is.EqualTo(1));

            var testStruct = new QuitReceived();
            testStruct.NodeId = 42;

            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<QuitReceived>(byteArray);

            Assert.That(roundTrip.NodeId, Is.EqualTo(testStruct.NodeId));
        }

        [Test]
        public void SurveyRepeaters()
        {
            int sizeOfStruct = Marshal.SizeOf<SurveyRepeaters>();
            Assert.That(sizeOfStruct, Is.EqualTo(1));

            SurveyRepeaters testStruct = new();

            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<SurveyRepeaters>(byteArray);

            Assert.That(roundTrip, Is.Not.Null);
            Assert.That(roundTrip, Is.TypeOf<SurveyRepeaters>());
        }

        [Test]
        public void RepeatersSurveyAnswer()
        {
            int sizeOfStruct = Marshal.SizeOf<RepeatersSurveyAnswer>();
            Assert.That(sizeOfStruct, Is.EqualTo(14));

            RepeatersSurveyAnswer testStruct = new()
            {
                NodeId = 0x42,
                IPAddressBytes = 0x12345678,
                LastReceivedFrameIndex = 0x4567890110987654,
                StillUseNetworkSync = true
            };

            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<RepeatersSurveyAnswer>(byteArray);

            Assert.That(roundTrip, Is.Not.Null);
            Assert.That(roundTrip, Is.TypeOf<RepeatersSurveyAnswer>());
            Assert.That(roundTrip.NodeId, Is.EqualTo(testStruct.NodeId));
            Assert.That(roundTrip.IPAddressBytes, Is.EqualTo(testStruct.IPAddressBytes));
            Assert.That(roundTrip.LastReceivedFrameIndex, Is.EqualTo(testStruct.LastReceivedFrameIndex));
            Assert.That(roundTrip.StillUseNetworkSync, Is.True);
        }

        [Test]
        public void RetransmitReceivedFrameData()
        {
            int sizeOfStruct = Marshal.SizeOf<RetransmitReceivedFrameData>();
            Assert.That(sizeOfStruct, Is.EqualTo(9));

            RetransmitReceivedFrameData testStruct = new()
            {
                NodeId = 0x42,
                FrameIndex = 0x4567890110987654
            };

            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<RetransmitReceivedFrameData>(byteArray);

            Assert.That(roundTrip, Is.Not.Null);
            Assert.That(roundTrip, Is.TypeOf<RetransmitReceivedFrameData>());
            Assert.That(roundTrip.NodeId, Is.EqualTo(testStruct.NodeId));
            Assert.That(roundTrip.FrameIndex, Is.EqualTo(testStruct.FrameIndex));
        }

        [Test]
        public void RetransmittedFrameData()
        {
            int sizeOfStruct = Marshal.SizeOf<RetransmittedReceivedFrameData>();
            Assert.That(sizeOfStruct, Is.EqualTo(20));

            RetransmittedReceivedFrameData testStruct = new()
            {
                FrameIndex = 0x1234567887654321,
                DataLength = 0x76544367,
                DatagramIndex = 0x65433456,
                DatagramDataOffset = 0x54322345
            };

            var byteArray = ToByteArray(testStruct, sizeOfStruct);
            var roundTrip = FromByteArray<RetransmittedReceivedFrameData>(byteArray);

            Assert.That(roundTrip, Is.Not.Null);
            Assert.That(roundTrip, Is.TypeOf<RetransmittedReceivedFrameData>());
            Assert.That(roundTrip.FrameIndex, Is.EqualTo(testStruct.FrameIndex));
            Assert.That(roundTrip.DataLength, Is.EqualTo(testStruct.DataLength));
            Assert.That(roundTrip.DatagramIndex, Is.EqualTo(testStruct.DatagramIndex));
            Assert.That(roundTrip.DatagramDataOffset, Is.EqualTo(testStruct.DatagramDataOffset));
        }

        static byte[] ToByteArray<T>(T toSerialize, int expectedSize) where T: unmanaged
        {
            var ret = new byte[expectedSize];
            var storedSize = toSerialize.StoreInBuffer(ret);
            Assert.That(storedSize, Is.EqualTo(expectedSize));
            return ret;
        }

        static T FromByteArray<T>(byte[] byteArray) where T : unmanaged
        {
            return byteArray.LoadStruct<T>();
        }
    }
}
