using System;
using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.ClusterDisplay.EmitterStateMachine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class EmitterNodeTests
    {
        MockClusterSync m_ClusterSync;
        UDPAgent[] m_TestAgents;
        EmitterNode m_Node;
        const byte k_EmitterId = 0;
        static readonly byte[] k_RepeaterIds = {1, 2};
        NodeState m_State;

        [SetUp]
        public void SetUp()
        {
            m_ClusterSync = new MockClusterSync(MockClusterSync.NodeType.Emitter, k_EmitterId, numRepeaters: k_RepeaterIds.Length);

            m_TestAgents = k_RepeaterIds.Select(id =>
            {
                var testConfig = MockClusterSync.udpConfig;
                testConfig.nodeId = id;
                testConfig.rxPort = MockClusterSync.udpConfig.txPort;
                testConfig.txPort = MockClusterSync.udpConfig.rxPort;
                return new UDPAgent(testConfig);
            }).ToArray();

            m_Node = m_ClusterSync.LocalNode as EmitterNode;
            Assert.IsNotNull(m_Node);

            // Watch out for static global states!
            // Potential refactoring target.
            EmitterStateWriter.UnregisterOnStoreCustomDataDelegates();
        }

        [Test]
        public void TestWaitForClients()
        {
            Assert.That(m_Node.m_RemoteNodes, Is.Empty);

            var registerState = new WaitingForAllClients(m_ClusterSync);
            registerState.EnterState(null);

            m_State = registerState;

            Assert.AreSame(registerState, registerState.ProcessFrame(false));
            Assert.IsFalse(registerState.ReadyToProceed);

            var helloMsgs = k_RepeaterIds.Select(id => GenerateMessage(id,
                new byte[] {k_EmitterId},
                EMessageType.HelloEmitter,
                new RolePublication {NodeRole = ENodeRole.Repeater}));

            foreach (var (msgTuple, udpAgent) in helloMsgs.Zip(m_TestAgents, Tuple.Create))
            {
                udpAgent.PublishMessage(msgTuple.header, msgTuple.rawMsg);
            }

            m_State = RunStateUntilTransition(registerState);
            Assert.That(m_State, Is.TypeOf<EmitterSynchronization>());

            // Check that we registered all the nodes
            Assert.That(m_Node.m_RemoteNodes, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestNodeRegistration()
        {
            var node = m_ClusterSync.LocalNode as EmitterNode;
            Assert.IsNotNull(node);

            node.RegisterNode(new RemoteNodeComContext() {ID = 3, Role = ENodeRole.Repeater});
            node.RegisterNode(new RemoteNodeComContext() {ID = 5, Role = ENodeRole.Repeater});
            Assert.NotZero(node.UdpAgent.AllNodesMask & (1 << 3));
            Assert.NotZero(node.UdpAgent.AllNodesMask & (1 << 5));
            Assert.Zero(node.UdpAgent.AllNodesMask & (1 << 2));

            Assert.That(node.m_RemoteNodes, Has.Count.EqualTo(2));
            var nodeIds = node.m_RemoteNodes.Select(x => x.ID).ToArray();
            var roles = node.m_RemoteNodes.Select(x => x.Role).ToArray();
            Assert.That(nodeIds, Has.Some.EqualTo(3));
            Assert.That(nodeIds, Has.Some.EqualTo(5));
            Assert.That(roles, Is.All.EqualTo(ENodeRole.Repeater));
        }

        [UnityTest]
        public IEnumerator TestEmitterSynchronization()
        {
            var emitterSync = new EmitterSynchronization(m_ClusterSync)
            {
                MaxTimeOut = TimeSpan.FromSeconds(MockClusterSync.timeoutSeconds)
            };
            emitterSync.EnterState(null);

            m_State = emitterSync;

            foreach (var repeaterId in k_RepeaterIds)
            {
                m_Node.RegisterNode(new RemoteNodeComContext {ID = repeaterId, Role = ENodeRole.Repeater});
            }

            using var rawStateBuffer = new NativeArray<byte>(ushort.MaxValue, Allocator.Persistent);

            // Make the emitter attach some custom data to the frame.
            byte[] GetCustomData() => Encoding.ASCII.GetBytes($"Hello World {m_ClusterSync.CurrentFrameID}");
            EmitterStateWriter.RegisterOnStoreCustomDataDelegate((NativeArray<byte> buffer, ref uint pos) =>
            {
                pos = (uint) buffer.AppendCustomData((int) pos, GetCustomData());
                return true;
            });

            // Simulate several frames
            const int kNumFrames = 4;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                // ======= Start of Emitter frame ===========
                m_ClusterSync.CurrentFrameID = frameNum;

                // At the beginning of the frame, wait until all nodes have arrived at the new frame
                Assert.IsTrue(RunStateUtil(emitterSync,
                    state => state.Stage == EmitterSynchronization.EStage.WaitOnRepeatersNextFrame));

                // Next send the current frame data to the repeaters.
                Assert.IsTrue(RunStateUtil(emitterSync,
                    state => state.Stage == EmitterSynchronization.EStage.EmitLastFrameData));

                emitterSync.ProcessFrame(false);

                // The is the current state data, with custom data appended
                var stateSize = EmitterStateWriter.StoreFrameState(rawStateBuffer, false);
                stateSize = (uint) rawStateBuffer.AppendCustomData((int) stateSize, GetCustomData());
                EmitterStateWriter.MarkStatesEnd(rawStateBuffer, ref stateSize);
                var stateBuffer = rawStateBuffer.GetSubArray(0, (int) stateSize);

                // ======== Nodes receive FrameData packet ======
                foreach (var testAgent in m_TestAgents)
                {
                    var (header, message, frameData) = testAgent.ReceiveMessageWithData<EmitterLastFrameData>();
                    Assert.That(header.MessageType, Is.EqualTo(EMessageType.LastFrameData));
                    Assert.That(message.FrameNumber == frameNum);

                    // Check that state data is received and it corresponds to the current frame.
                    // The received data should also contain custom data at the end.
                    Assert.That(frameData, Is.EqualTo(stateBuffer));
                }

                // Once repeaters have ACK'ed, emitter should be ready to continue this frame
                Assert.IsTrue(RunStateUntilReady(emitterSync));

                // ======= End of Frame ===========
                yield return null;
                emitterSync.ProcessFrame(true);

                // ======= Repeaters start new frame =========
                foreach (var (id, agent) in k_RepeaterIds.Zip(m_TestAgents, Tuple.Create))
                {
                    var (header, rawMsg) = GenerateMessage(id,
                        new[]
                        {
                            k_EmitterId
                        },
                        EMessageType.EnterNextFrame,
                        new RepeaterEnteredNextFrame
                        {
                            FrameNumber = frameNum + 1
                        });

                    agent.PublishMessage(header, rawMsg);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TestEmitterSynchronizationDelayRepeaters()
        {
            m_Node.RepeatersDelayed = true;

            var emitterSync = new EmitterSynchronization(m_ClusterSync)
            {
                MaxTimeOut = TimeSpan.FromSeconds(MockClusterSync.timeoutSeconds)
            };
            emitterSync.EnterState(null);

            m_State = emitterSync;

            foreach (var repeaterId in k_RepeaterIds)
            {
                m_Node.RegisterNode(new RemoteNodeComContext {ID = repeaterId, Role = ENodeRole.Repeater});
            }

            // Simulate several frames
            const int kNumFrames = 3;
            using var rawStateBuffer = new NativeArray<byte>(ushort.MaxValue, Allocator.Persistent);
            NativeArray<byte> lastFrameState = default;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                // ======= Start of Emitter frame =========
                m_ClusterSync.CurrentFrameID = frameNum;

                // The emitter is going to run a frame ahead, so on frame 0 it's going to start right away without
                // waiting for all the nodes to report in.
                if (m_ClusterSync.CurrentFrameID == 0)
                {
                    Assert.That(emitterSync.ReadyToProceed, Is.True);
                }

                if (m_ClusterSync.CurrentFrameID > 0)
                {
                    Assert.That(RunStateUtil(emitterSync,
                            state => state.Stage == EmitterSynchronization.EStage.EmitLastFrameData),
                        Is.True);
                    emitterSync.ProcessFrame(false);
                }

                // ======== Nodes receive FrameData packet (maybe) ======
                foreach (var testAgent in m_TestAgents)
                {
                    var (rxHeader, message, frameData) = testAgent.ReceiveMessageWithData<EmitterLastFrameData>(2000);

                    if (m_ClusterSync.CurrentFrameID > 0)
                    {
                        // Emitter starts publishing LastFrameData on frame 1
                        Assert.That(rxHeader.MessageType, Is.EqualTo(EMessageType.LastFrameData));

                        // FrameID (frame number) should be behind the emitter by 1
                        Assert.That(message.FrameNumber, Is.EqualTo(m_ClusterSync.CurrentFrameID - 1));

                        // Check that the frame state is from the previous frame
                        Assert.IsNotNull(lastFrameState);
                        Assert.That(frameData, Is.EqualTo(lastFrameState));
                    }
                    else
                    {
                        // Repeaters should not be receiving anything on frame 0
                        Assert.That(rxHeader.MessageType, Is.EqualTo(default(EMessageType)));
                    }
                }

                if (m_ClusterSync.CurrentFrameID > 0)
                {
                    Assert.IsTrue(RunStateUntilReady(emitterSync));
                }

                // Store the state data for the current frame
                var stateSize = (int) EmitterStateWriter.StoreFrameState(rawStateBuffer, true);
                lastFrameState = rawStateBuffer.GetSubArray(0, stateSize);

                // ======= End of Frame ===========
                yield return null;

                emitterSync.ProcessFrame(true);

                // ======= Repeaters start new frame =========
                m_ClusterSync.CurrentFrameID = frameNum + 1;
                foreach (var (id, agent) in k_RepeaterIds.Zip(m_TestAgents, Tuple.Create))
                {
                    var (header, rawMsg) = GenerateMessage(id,
                        new[]
                        {
                            k_EmitterId
                        },
                        EMessageType.EnterNextFrame,
                        new RepeaterEnteredNextFrame
                        {
                            FrameNumber = m_ClusterSync.CurrentFrameID - 1
                        });

                    agent.PublishMessage(header, rawMsg);
                }
            }

            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            // Awkward way to invoke m_State.ExitState
            new NullState(m_ClusterSync).EnterState(m_State);

            m_Node.Exit();
            foreach (var testAgent in m_TestAgents)
            {
                testAgent.Stop();
            }
        }
    }
}