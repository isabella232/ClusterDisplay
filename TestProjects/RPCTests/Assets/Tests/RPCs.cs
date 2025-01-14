using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;

using UnityEngine;
using Unity.Collections;
using UnityEngine.TestTools;

using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay.Tests
{
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 20)]
    public struct StructA
    {
        [FieldOffset(0)]
        public float floatValue;
        [FieldOffset(4)]
        public int intValue;
        [FieldOffset(8)]
        public StructB structB;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 12)]
    public struct StructB
    {
        [FieldOffset(0)]
        public bool booleanValue;
        [FieldOffset(4)]
        public long longValue;
    }

    public static class RPCs
    {
        const int k_BufferSize = ushort.MaxValue;

        static bool m_SendFlag = true;
        public static bool Sending => m_SendFlag;
        public static bool Receiving => !m_SendFlag;

        public static void FlagSending () => m_SendFlag = true;
        public static void FlagReceiving () => m_SendFlag = false;

        public static void Initialize ()
        {
            RPCRegistry.Initialize();
            RPCBufferIO.Initialize(overrideCaptureExecution: true);
            RPCExecutor.TrySetup();
        }

        public static void Dispose()
        {
            RPCExecutor.RemovePlayerLoops();
            RPCBufferIO.Dispose();
        }

        public static void EmulateFlight ()
        {
            NativeArray<byte> buffer = new NativeArray<byte>(k_BufferSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int endPos = RPCBufferIO.Latch(buffer);
            RPCBufferIO.Unlatch(buffer.GetSubArray(0, (int)endPos));

            buffer.Dispose();
        }

        static IRPCTestRecorder m_RPCTestRecorder;
        public static void PushRPCTestRecorder (IRPCTestRecorder rpcTestRecorder) => m_RPCTestRecorder = rpcTestRecorder;

        #region FloatTest
        public static void FloatTest (float floatValue)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(floatValue, Is.AtLeast(1.4f));
        }

        #endregion

        #region StringTest
        public static void StringTest(string stringValue)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            Assert.That(stringValue, Is.EqualTo("Hello, World!"));
            m_RPCTestRecorder.RecordExecution();
        }
        #endregion

        #region MultiStringTest
        public static void MultiStringTest(string stringAValue, string stringBValue)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(stringAValue, Is.EqualTo("Hello"));
            Assert.That(stringBValue, Is.EqualTo("World"));
        }
        #endregion

        #region PrimitivesTest
        public static void PrimitivesTest(
            byte byteValue,
            sbyte sbyteValue,
            bool booleanValue,
            char charValue,
            string stringValue,
            ushort ushortValue,
            short shortValue,
            uint uintValue,
            int intValue,
            ulong ulongValue,
            long longValue,
            float floatValue,
            double doubleValue)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(byteValue, Is.EqualTo(128));
            Assert.That(sbyteValue, Is.EqualTo(-128));
            Assert.That(booleanValue, Is.EqualTo(true));
            Assert.That(charValue, Is.EqualTo('@'));
            Assert.That(stringValue, Is.EqualTo("Hello, World!"));
            Assert.That(ushortValue, Is.EqualTo(12345));
            Assert.That(shortValue, Is.EqualTo(-12345));
            Assert.That(uintValue, Is.EqualTo(123456));
            Assert.That(intValue, Is.EqualTo(-123456));
            Assert.That(ulongValue, Is.EqualTo(12345678912));
            Assert.That(longValue, Is.EqualTo(-12345678912));
            Assert.That(floatValue, Is.AtLeast(-123.456f));
            Assert.That(doubleValue, Is.AtLeast(-123456.78912));
        }

        #endregion

        #region Vector3Test
        public static void Vector3Test(Vector3 vector3Value)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(vector3Value.x, Is.AtLeast(vector3Value.x));
            Assert.That(vector3Value.y, Is.AtLeast(vector3Value.y));
            Assert.That(vector3Value.z, Is.AtLeast(vector3Value.z));
        }
        #endregion

        #region DoubleArrayTest
        public static void DoubleArrayTest(double[] doubleArray)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(doubleArray[0], Is.AtLeast(3.14159265358979323846));
            Assert.That(doubleArray[1], Is.AtLeast(6.28318530717958647692));
            Assert.That(doubleArray[2], Is.AtLeast(2.71828182845904523536));
        }
        #endregion

        #region Vector3ArrayTest
        public static void Vector3ArrayTest(Vector3[] vectorArray)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(vectorArray[0].x, Is.AtLeast(Vector3.right.x));
            Assert.That(vectorArray[0].y, Is.AtLeast(Vector3.right.y));
            Assert.That(vectorArray[0].z, Is.AtLeast(Vector3.right.z));

            Assert.That(vectorArray[1].x, Is.AtLeast(Vector3.up.x));
            Assert.That(vectorArray[1].y, Is.AtLeast(Vector3.up.y));
            Assert.That(vectorArray[1].z, Is.AtLeast(Vector3.up.z));

            Assert.That(vectorArray[2].x, Is.AtLeast(Vector3.forward.x));
            Assert.That(vectorArray[2].y, Is.AtLeast(Vector3.forward.y));
            Assert.That(vectorArray[2].z, Is.AtLeast(Vector3.forward.z));
        }
        #endregion

        #region StructATest
        public static void StructATest(StructA structA)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(structA.floatValue, Is.AtLeast(3.1415926f));
            Assert.That(structA.intValue, Is.AtLeast(42));
            Assert.That(structA.structB.booleanValue, Is.AtLeast(true));
            Assert.That(structA.structB.longValue, Is.AtLeast(3141592653589793238));
        }
        #endregion

        #region StructBTest
        public static void StructBTest(StructB structB)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(structB.booleanValue, Is.AtLeast(true));
            Assert.That(structB.longValue, Is.AtLeast(3141592653589793238));
        }
        #endregion

        public static StructA[] GenerateStructArray () =>
            new[]
            {
                new StructA
                {
                    floatValue = 1.1f,
                    intValue = 42,
                    structB = new StructB
                    {
                        booleanValue = true,
                        longValue = 6283185307179586476
                    }
                },

                new StructA
                {
                    floatValue = 1.2f,
                    intValue = 43,
                    structB = new StructB
                    {
                        booleanValue = false,
                        longValue = 2718281828459045235
                    }
                },

                new StructA
                {
                    floatValue = 1.3f,
                    intValue = 44,
                    structB = new StructB
                    {
                        booleanValue = true,
                        longValue = 1
                    }
                },
            };

        #region StructAArrayTest
        public static void StructAArrayTest(StructA[] structAArray)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(structAArray[0].floatValue, Is.AtLeast(1.1f));
            Assert.That(structAArray[0].intValue, Is.AtLeast(42));
            Assert.That(structAArray[0].structB.booleanValue, Is.AtLeast(true));
            Assert.That(structAArray[0].structB.longValue, Is.AtLeast(6283185307179586476));

            Assert.That(structAArray[1].floatValue, Is.AtLeast(1.2f));
            Assert.That(structAArray[1].intValue, Is.AtLeast(43));
            Assert.That(structAArray[1].structB.booleanValue, Is.AtLeast(false));
            Assert.That(structAArray[1].structB.longValue, Is.AtLeast(2718281828459045235));

            Assert.That(structAArray[2].floatValue, Is.AtLeast(1.3f));
            Assert.That(structAArray[2].intValue, Is.AtLeast(44));
            Assert.That(structAArray[2].structB.booleanValue, Is.AtLeast(true));
            Assert.That(structAArray[2].structB.longValue, Is.AtLeast(1));
        }

        #endregion

        #region StructANativeArrayTest
        public static void StructANativeArrayTest(NativeArray<StructA> structANativeArray)
        {
            if (Sending)
            {
                m_RPCTestRecorder.RecordPropagation();
                return;
            }

            m_RPCTestRecorder.RecordExecution();
            Assert.That(structANativeArray[0].floatValue, Is.AtLeast(1.1f));
            Assert.That(structANativeArray[0].intValue, Is.AtLeast(42));
            Assert.That(structANativeArray[0].structB.booleanValue, Is.AtLeast(true));
            Assert.That(structANativeArray[0].structB.longValue, Is.AtLeast(6283185307179586476));

            Assert.That(structANativeArray[1].floatValue, Is.AtLeast(1.2f));
            Assert.That(structANativeArray[1].intValue, Is.AtLeast(43));
            Assert.That(structANativeArray[1].structB.booleanValue, Is.AtLeast(false));
            Assert.That(structANativeArray[1].structB.longValue, Is.AtLeast(2718281828459045235));

            Assert.That(structANativeArray[2].floatValue, Is.AtLeast(1.3f));
            Assert.That(structANativeArray[2].intValue, Is.AtLeast(44));
            Assert.That(structANativeArray[2].structB.booleanValue, Is.AtLeast(true));
            Assert.That(structANativeArray[2].structB.longValue, Is.AtLeast(1));

            structANativeArray.Dispose();
        }
        #endregion
    }
}
