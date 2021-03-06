using System.Text;
using NUnit.Framework;
using StatsdClient;

namespace Tests
{
    [TestFixture]
    public class BufferBuilderTests
    {
        BufferBuilder _bufferBuilder;

        BufferBuilderHandlerMock _handler;

        [SetUp]
        public void Init()
        {
            _handler = new BufferBuilderHandlerMock();
            _bufferBuilder = new BufferBuilder(_handler, 12, "\n");
        }

        [Test]
        public void Add()
        {
            Assert.AreEqual(12, _bufferBuilder.Capacity);
            Assert.True(_bufferBuilder.Add(new string('1', 3)));
            Assert.True(_bufferBuilder.Add(new string('2', 3)));
            Assert.True(_bufferBuilder.Add(new string('3', 3)));
            Assert.Null(_handler.Buffer);
            Assert.True(_bufferBuilder.Add(new string('4', 3)));
            Assert.AreEqual(3, _bufferBuilder.Length);
            Assert.AreEqual(_handler.Buffer, Encoding.UTF8.GetBytes("111\n222\n333"));
        }

        [Test]
        public void HandleBufferAndReset()
        {
            Assert.Less(4, _bufferBuilder.Capacity);
            Assert.True(_bufferBuilder.Add(new string('1', 2)));
            _bufferBuilder.HandleBufferAndReset();
            Assert.AreEqual(_handler.Buffer, Encoding.UTF8.GetBytes("11"));

            Assert.True(_bufferBuilder.Add(new string('3', 4)));
            _bufferBuilder.HandleBufferAndReset();
            Assert.AreEqual(_handler.Buffer, Encoding.UTF8.GetBytes("3333"));
        }

        [Test]
        public void AddReturnedValue()
        {
            Assert.False(_bufferBuilder.Add(new string('1', _bufferBuilder.Capacity + 1)));
            Assert.AreEqual(0, _bufferBuilder.Length);

            Assert.True(_bufferBuilder.Add(new string('1', _bufferBuilder.Capacity)));
            Assert.AreEqual(_bufferBuilder.Capacity, _bufferBuilder.Length);
        }
    }
}