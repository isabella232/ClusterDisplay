﻿using System;

namespace Unity.ClusterDisplay.Tests
{
    /// <summary>
    /// IClusterSyncState implementation that stands up a dummy
    /// LocalNode and a functioning UDPAgent. Used for testing
    /// NodeStates.
    /// </summary>
    class MockClusterSync : IClusterSyncState
    {
        public enum NodeType
        {
            Emitter,
            Repeater
        }

        public const int rxPort = 12345;
        public const int txPort = 12346;
        public const string multicastAddress = "224.0.1.0";
        public const int timeoutSeconds = NetworkingUtils.receiveTimeout * 1000;
        public const int maxRetries = 20;

        public static readonly string adapterName = NetworkingUtils.SelectNic().Name;

        public static readonly UDPAgent.Config udpConfig = new()
        {
            ip = multicastAddress,
            rxPort = rxPort,
            txPort = txPort,
            timeOut = timeoutSeconds,
            adapterName = adapterName
        };

        public MockClusterSync(NodeType nodeType, byte nodeId, bool delayRepeaters = false, int numRepeaters = 2, bool headlessEmitter = false)
        {
            var udpConfig = MockClusterSync.udpConfig;
            udpConfig.nodeId = nodeId;
            var emitterConfig = new EmitterNode.Config
            {
                headlessEmitter = headlessEmitter,
                repeatersDelayed = delayRepeaters,
                udpAgentConfig = udpConfig,
                repeaterCount = numRepeaters
            };
            LocalNode = nodeType switch
            {
                NodeType.Emitter => new MockEmitterNode(this, emitterConfig),
                NodeType.Repeater => new MockRepeaterNode(this, false, udpConfig),
                _ => throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null)
            };
        }

        public ulong CurrentFrameID { get; set; } = 0;
        public ClusterNode LocalNode { get; }
    }

    /// <summary>
    /// A dummy NodeState: when we require a state that does nothing.
    /// </summary>
    class NullState : NodeState
    {
        public NullState(IClusterSyncState clusterSync)
            : base(clusterSync) { }
    }

    /// <summary>
    /// A RepeaterNode that defaults to NullState.
    /// </summary>
    class MockRepeaterNode : RepeaterNode
    {
        public MockRepeaterNode(IClusterSyncState clusterSync, bool delayRepeater, UDPAgent.Config config)
            : base(clusterSync, delayRepeater, config)
        {
            // Exit the starting state immediately
            var oldState = m_CurrentState;
            m_CurrentState = new NullState(clusterSync);
            m_CurrentState.EnterState(oldState);
        }
    }

    /// <summary>
    /// An EmitterNode that defaults to NullState.
    /// </summary>
    class MockEmitterNode : EmitterNode
    {
        public MockEmitterNode(IClusterSyncState clusterSync, Config config)
            : base(clusterSync, config)
        {
            // Exit the starting state immediately
            var oldState = m_CurrentState;
            m_CurrentState = new NullState(clusterSync);
            m_CurrentState.EnterState(oldState);
        }
    }

    static class NodeTestUtils
    {
        public static bool RunStateUtil<T>(T state,
            Func<T, bool> pred,
            int maxRetries = MockClusterSync.maxRetries) where T : NodeState =>
            TestUtils.LoopUntil(() => pred(state) || state != state.ProcessFrame(false), MockClusterSync.maxRetries);

        public static NodeState RunStateUntilTransition(NodeState state, int maxRetries = MockClusterSync.maxRetries)
        {
            NodeState nextState = state;
            TestUtils.LoopUntil(() =>
            {
                nextState = state.ProcessFrame(false);
                return nextState != state;
            }, maxRetries);
            
            return nextState;
        }

        public static bool RunStateUntilReady(NodeState state, int maxRetries = MockClusterSync.maxRetries) =>
            RunStateUtil(state, nodeState => nodeState.ReadyToProceed, maxRetries);
    }
}