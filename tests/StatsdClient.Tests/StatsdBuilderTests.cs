using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mono.Unix;
using Moq;
using NUnit.Framework;
using StatsdClient.Bufferize;

namespace StatsdClient.Tests
{
    [TestFixture]
    public class StatsdBuilderTests
    {
        Mock<IStatsBufferizeFactory> _mock;
        StatsdBuilder _statsdBuilder;
        readonly Dictionary<string, string> _envVarsToRestore = new Dictionary<string, string>();
        readonly List<string> _envVarsKeyToRestore = new List<string>{
            StatsdConfig.DD_DOGSTATSD_PORT_ENV_VAR,
            StatsdConfig.DD_AGENT_HOST_ENV_VAR};

        [SetUp]
        public void Init()
        {
            _mock = new Mock<IStatsBufferizeFactory>(MockBehavior.Loose);
            _statsdBuilder = new StatsdBuilder(_mock.Object);

            foreach (var key in _envVarsKeyToRestore)
                _envVarsToRestore[key] = Environment.GetEnvironmentVariable(key);

            // Set default hostname
            Environment.SetEnvironmentVariable(StatsdConfig.DD_AGENT_HOST_ENV_VAR, "0.0.0.0");
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var env in _envVarsToRestore)
                Environment.SetEnvironmentVariable(env.Key, env.Value);

            _statsdBuilder.Dispose();
        }

 [Test]
        public void StatsdServerName42()
        {
            int expected = 1000 * 1000 * 1000;
            int v = 0;
            var t = Task.Run( () => {
                    for (int i = 0; i < expected; ++i)
                        Interlocked.Increment(ref v);
            });
            int total = 0;
            for (int i = 0; i < 1000; ++i)
            {
                total += Interlocked.Exchange(ref v, 0);
                Task.Delay(1).Wait();
            }
            t.Wait();
            total += Interlocked.Exchange(ref v, 0);
            Assert.AreEqual(expected, total);
        }

        [Test]
        public void StatsdServerName()
        {
            Environment.SetEnvironmentVariable(StatsdConfig.DD_AGENT_HOST_ENV_VAR, null);
            Assert.Throws<ArgumentNullException>(() => GetStatsdServerName(new StatsdConfig { }));

            Assert.AreEqual("0.0.0.1", GetStatsdServerName(new StatsdConfig { StatsdServerName = "0.0.0.1" }));

            Environment.SetEnvironmentVariable(StatsdConfig.DD_AGENT_HOST_ENV_VAR, "0.0.0.2");
            Assert.AreEqual("0.0.0.2", GetStatsdServerName(new StatsdConfig { }));

            Assert.AreEqual("0.0.0.3", GetStatsdServerName(new StatsdConfig { StatsdServerName = "0.0.0.3" }));
        }

        [Test]
        public void UDPPort()
        {
            Assert.AreEqual(StatsdConfig.DefaultStatsdPort, GetUDPPort(new StatsdConfig { }));

            Assert.AreEqual(1, GetUDPPort(new StatsdConfig { StatsdPort = 1 }));

            Environment.SetEnvironmentVariable(StatsdConfig.DD_DOGSTATSD_PORT_ENV_VAR, "2");
            Assert.AreEqual(2, GetUDPPort(new StatsdConfig { }));

            Assert.AreEqual(3, GetUDPPort(new StatsdConfig { StatsdPort = 3 }));
        }

        [Test]
        public void UDSStatsdServerName()
        {
            Environment.SetEnvironmentVariable(StatsdConfig.DD_AGENT_HOST_ENV_VAR, null);
            Assert.AreEqual("server1", GetUDSStatsdServerName(CreateUDSConfig("server1")));

            Environment.SetEnvironmentVariable(StatsdConfig.DD_AGENT_HOST_ENV_VAR,
                StatsdBuilder.UnixDomainSocketPrefix + "server2");
            Assert.AreEqual("server2", GetUDSStatsdServerName(new StatsdConfig { }));

            Assert.AreEqual("server3", GetUDSStatsdServerName(CreateUDSConfig("server3")));
        }

        [Test]
        public void CreateStatsBufferizeUDP()
        {
            var config = new StatsdConfig { };
            var conf = config.Advanced;

            config.StatsdMaxUDPPacketSize = 10;
            conf.MaxMetricsInAsyncQueue = 2;
            conf.MaxBlockDuration = TimeSpan.FromMilliseconds(3);
            conf.DurationBeforeSendingNotFullBuffer = TimeSpan.FromMilliseconds(4);

            _statsdBuilder.BuildStats(config);
            _mock.Verify(m => m.CreateStatsBufferize(It.IsAny<Telemetry>(),
                                                     It.Is<BufferBuilder>(b => b.Capacity == config.StatsdMaxUDPPacketSize),
                                                     conf.MaxMetricsInAsyncQueue,
                                                     conf.MaxBlockDuration,
                                                     conf.DurationBeforeSendingNotFullBuffer));
        }

        [Test]
        public void CreateStatsBufferizeUDS()
        {
            var config = CreateUDSConfig("server1");
            config.StatsdMaxUnixDomainSocketPacketSize = 20;

            _statsdBuilder.BuildStats(config);
            _mock.Verify(m => m.CreateStatsBufferize(It.IsAny<Telemetry>(),
                                                     It.Is<BufferBuilder>(b => b.Capacity == config.StatsdMaxUnixDomainSocketPacketSize),
                                                     It.IsAny<int>(),
                                                     null,
                                                     It.IsAny<TimeSpan>()));
        }

        static StatsdConfig CreateUDSConfig(string server)
        {
            return new StatsdConfig { StatsdServerName = StatsdBuilder.UnixDomainSocketPrefix + server };
        }

        int GetUDPPort(StatsdConfig config)
        {
            var endPoint = GetUDPIPEndPoint(config);
            return endPoint.Port;
        }

        string GetStatsdServerName(StatsdConfig config)
        {
            var endPoint = GetUDPIPEndPoint(config);
            return endPoint.Address.ToString();
        }

        string GetUDSStatsdServerName(StatsdConfig config)
        {
            UnixEndPoint endPoint = null;

            _mock.Setup(m => m.CreateUnixDomainSocketStatsSender(It.IsAny<UnixEndPoint>(),
                                                                 It.IsAny<TimeSpan?>()))
                .Callback<UnixEndPoint, TimeSpan?>((e, d) => endPoint = e);
            _statsdBuilder.BuildStats(config);
            Assert.NotNull(endPoint);

            return endPoint.Filename;
        }

        IPEndPoint GetUDPIPEndPoint(StatsdConfig config)
        {
            IPEndPoint endPoint = null;

            _mock.Setup(m => m.CreateUDPStatsSender(It.IsAny<IPEndPoint>()))
                .Callback< IPEndPoint>( e => endPoint = e);
            _statsdBuilder.BuildStats(config);
            Assert.NotNull(endPoint);
            return endPoint;
        }
    }
}