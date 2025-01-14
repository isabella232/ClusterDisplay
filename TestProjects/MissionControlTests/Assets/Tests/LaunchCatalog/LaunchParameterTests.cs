﻿using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    public class LaunchParameterTests
    {
        [Test]
        public void Equal()
        {
            LaunchParameter parameterA = new();
            LaunchParameter parameterB = new();
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Name = "UDP port";
            parameterB.Name = "TCP port";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Name = "UDP port";
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Id = "udpPort";
            parameterB.Id = "tcpPort";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Id = "udpPort";
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Description = "TCP port number used for communication between emitter and repeater nodes.";
            parameterB.Description = "UDP port number used for communication between emitter and repeater nodes.";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.Description = parameterB.Description;
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Type = LaunchParameterType.Integer;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.Type = null;
            parameterB.Type = LaunchParameterType.Integer;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.Type = LaunchParameterType.String;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Type = LaunchParameterType.String;
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Constraint = new ListConstraint();
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.Constraint = null;
            parameterB.Constraint = new ListConstraint();
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.Constraint = new RegularExpressionConstraint();
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Constraint = new RegularExpressionConstraint();
            Assert.That(parameterA, Is.EqualTo(parameterB));

            // Test with string default value
            parameterA.DefaultValue = "Quarante deux";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.DefaultValue = null;
            parameterB.DefaultValue = "Vingt huit";
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.DefaultValue = "Vingt huit";
            Assert.That(parameterA, Is.EqualTo(parameterB));

            // Test with int default value
            parameterA.DefaultValue = null;
            parameterB.DefaultValue = null;
            parameterA.Type = LaunchParameterType.Integer;
            parameterB.Type = LaunchParameterType.Integer;
            parameterA.DefaultValue = 42;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.DefaultValue = null;
            parameterB.DefaultValue = 28;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.DefaultValue = 28;
            Assert.That(parameterA, Is.EqualTo(parameterB));

            // Test with float default value
            parameterA.DefaultValue = null;
            parameterB.DefaultValue = null;
            parameterA.Type = LaunchParameterType.Float;
            parameterB.Type = LaunchParameterType.Float;
            parameterA.DefaultValue = 42.0f;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.DefaultValue = null;
            parameterB.DefaultValue = 28.0f;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterA.DefaultValue = 28.0f;
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.ToBeRevisedByCapcom = true;
            parameterB.ToBeRevisedByCapcom = false;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.ToBeRevisedByCapcom = true;
            Assert.That(parameterA, Is.EqualTo(parameterB));

            parameterA.Hidden = true;
            parameterB.Hidden = false;
            Assert.That(parameterA, Is.Not.EqualTo(parameterB));
            parameterB.Hidden = true;
            Assert.That(parameterA, Is.EqualTo(parameterB));
        }

        [Test]
        public void SerializeDefault()
        {
            LaunchParameter toSerialize = new();
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameter>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void SerializeFull()
        {
            LaunchParameter toSerialize = new();
            toSerialize.Name = "UDP port";
            toSerialize.Group = "Networking";
            toSerialize.Id = "udpPort";
            toSerialize.Description = "UDP port number used for communication between emitter and repeater nodes.";
            toSerialize.Type = LaunchParameterType.Integer;
            toSerialize.Constraint = new RangeConstraint() { Min = 0, Max = ushort.MaxValue };
            toSerialize.DefaultValue = 25690;
            toSerialize.ToBeRevisedByCapcom = true;
            toSerialize.Hidden = true;
            var serializedParameter = JsonConvert.SerializeObject(toSerialize, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameter>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(toSerialize));
        }

        [Test]
        public void BooleanDefaultValueBad()
        {
            LaunchParameter launchParameter = new();
            launchParameter.Type = LaunchParameterType.Boolean;
            Assert.Throws<FormatException>(() => launchParameter.DefaultValue = "Quarante deux" );
            Assert.Throws<FormatException>(() => launchParameter.DefaultValue = "vrai" );
            Assert.Throws<InvalidCastException>(() => launchParameter.DefaultValue = Guid.NewGuid() );
            Assert.Throws<InvalidCastException>(() => launchParameter.DefaultValue = TimeSpan.FromSeconds(42) );
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"boolean\", \"defaultValue\":{\"property\":42}}",
                    Json.SerializerOptions));
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"integer\", \"defaultValue\":\"Quarante deux\"}",
                    Json.SerializerOptions));
            Assert.That(launchParameter.DefaultValue, Is.Null);
        }

        [Test]
        public void IntDefaultValueBad()
        {
            LaunchParameter launchParameter = new();
            launchParameter.Type = LaunchParameterType.Integer;
            Assert.Throws<FormatException>(() => launchParameter.DefaultValue = "Quarante deux" );
            Assert.Throws<FormatException>(() => launchParameter.DefaultValue = 42.28 );
            Assert.Throws<OverflowException>(() => launchParameter.DefaultValue = ulong.MaxValue );
            Assert.That(launchParameter.DefaultValue, Is.Null);
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"integer\", \"defaultValue\":42.28}",
                    Json.SerializerOptions));
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"integer\", \"defaultValue\":\"Quarante deux\"}",
                    Json.SerializerOptions));
        }

        [Test]
        public void FloatDefaultValueBad()
        {
            LaunchParameter launchParameter = new();
            launchParameter.Type = LaunchParameterType.Float;
            Assert.Throws<FormatException>(() => launchParameter.DefaultValue = "Quarante deux" );
            Assert.Throws<InvalidCastException>(() => launchParameter.DefaultValue = double.MaxValue );
            Assert.Throws<InvalidCastException>(() => launchParameter.DefaultValue = float.NaN );
            Assert.That(launchParameter.DefaultValue, Is.Null);
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"float\", \"defaultValue\":\"Quarante deux\"}",
                    Json.SerializerOptions));
        }

        [Test]
        public void StringDefaultValueBad()
        {
            // A lot of things can be converted to string...
            var launchParameter = JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"string\", \"defaultValue\":42}",
                Json.SerializerOptions)!;
            Assert.That(launchParameter.DefaultValue, Is.EqualTo("42"));
            launchParameter = JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"string\", \"defaultValue\":true}",
                Json.SerializerOptions)!;
            Assert.That(launchParameter.DefaultValue, Is.EqualTo("True"));
            launchParameter = JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"string\", \"defaultValue\":false}",
                Json.SerializerOptions)!;
            Assert.That(launchParameter.DefaultValue, Is.EqualTo("False"));

            // We have to be really nasty to get it to fail
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"string\", \"defaultValue\":{\"propert\":\"of an object\"}",
                    Json.SerializerOptions));
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<LaunchParameter>("{\"type\":\"string\", \"defaultValue\":[1,2,3]}",
                    Json.SerializerOptions));
        }

        [Test]
        [TestCase(LaunchParameterType.Boolean, 42,              true)]
        [TestCase(LaunchParameterType.Boolean, 42.28,           true)]
        [TestCase(LaunchParameterType.Boolean, 0,               false)]
        [TestCase(LaunchParameterType.Boolean, true,            true)]
        [TestCase(LaunchParameterType.Boolean, false,           false)]
        [TestCase(LaunchParameterType.Boolean, "true",          true)]
        [TestCase(LaunchParameterType.Boolean, "false",         false)]
        [TestCase(LaunchParameterType.Integer, 42.0,            42)]
        [TestCase(LaunchParameterType.Integer, 42.0f,           42)]
        [TestCase(LaunchParameterType.Integer, 42,              42)]
        [TestCase(LaunchParameterType.Integer, 42L,             42)]
        [TestCase(LaunchParameterType.Integer, "42",            42)]
        [TestCase(LaunchParameterType.Float,   42,              42.0f)]
        [TestCase(LaunchParameterType.Float,   42.0,            42.0f)]
        [TestCase(LaunchParameterType.Float,   42.0f,           42.0f)]
        [TestCase(LaunchParameterType.Float,   42.28,           42.28f)]
        [TestCase(LaunchParameterType.Float,   "42.28",         42.28f)]
        [TestCase(LaunchParameterType.Float,   0,               0.0f)]
        [TestCase(LaunchParameterType.Float,   0.0,             0.0f)]
        [TestCase(LaunchParameterType.Float,   0.0f,            0.0f)]
        [TestCase(LaunchParameterType.String,  "Quarante deux", "Quarante deux")]
        [TestCase(LaunchParameterType.String,  42,              "42")]
        [TestCase(LaunchParameterType.String,  42.28,           "42.28")]
        public void DefaultValueGood(LaunchParameterType type, object defaultValue, object expectedValue)
        {
            LaunchParameter launchParameter = new();
            launchParameter.Type = type;
            launchParameter.DefaultValue = defaultValue;
            Assert.That(launchParameter.DefaultValue, Is.TypeOf(expectedValue.GetType()));
            Assert.That(launchParameter.DefaultValue, Is.EqualTo(expectedValue));
            var serializedParameter = JsonConvert.SerializeObject(launchParameter, Json.SerializerOptions);
            var deserialized = JsonConvert.DeserializeObject<LaunchParameter>(serializedParameter, Json.SerializerOptions);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized, Is.EqualTo(launchParameter));
        }
    }
}
