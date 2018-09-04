using System;
using Xunit;
using eventphone.yate;

namespace eventphone.yate.test
{
    public class SerializerTest
    {
        private readonly YateSerializer _serializer;

        public SerializerTest()
        {
            _serializer = new YateSerializer();
        }

        [Fact]
        public void NullEncode()
        {
            var message = "test";
            var encoded = _serializer.Encode(message);
            Assert.Equal(message, encoded);
        }

        [Fact]
        public void PercentEncode()
        {
            var message = "test%test";
            var encoded = _serializer.Encode(message);
            Assert.Equal("test%%test", encoded);
        }

        [Fact]
        public void NewlineEncode()
        {
            var message = "test\n";
            var encoded = _serializer.Encode(message);
            Assert.Equal("test%J", encoded);
        }

        [Fact]
        public void SpaceEncode()
        {
            var message = "test test";
            var encoded = _serializer.Encode(message);
            Assert.Equal(message, encoded);
        }

        [Fact]
        public void InitialCharEncode()
        {
            var message = "%>message";
            var encoded = _serializer.Encode(message);
            Assert.Equal("%%>message", encoded);
            encoded = _serializer.Encode("\tmessage");
            Assert.Equal("%Imessage", encoded);
        }

        [Fact]
        public void NullDecode()
        {
            var message = "test";
            var encoded = _serializer.Decode(message);
            Assert.Equal(message, encoded);
        }

        [Fact]
        public void PercentDecode()
        {
            var message = "test%%test";
            var encoded = _serializer.Decode(message);
            Assert.Equal("test%test", encoded);
        }

        [Fact]
        public void NewlineDecode()
        {
            var message = "test%J";
            var encoded = _serializer.Decode(message);
            Assert.Equal("test\n", encoded);
        }

        [Fact]
        public void SpaceDecode()
        {
            var message = "test test";
            var encoded = _serializer.Decode(message);
            Assert.Equal(message, encoded);
        }

        [Fact]
        public void InitialCharDecode()
        {
            var message = "%%>message";
            var encoded = _serializer.Decode(message);
            Assert.Equal("%>message", encoded);
            encoded = _serializer.Decode("%Imessage");
            Assert.Equal("\tmessage", encoded);
        }

        [Fact]
        public void CanSerializeParameter()
        {
            var param = new Tuple<string, string>("a%a%=a\nb", "foo\tvoid");
            var encoded = _serializer.Encode(param);
            Assert.Equal("a%%a%%%}a%Jb=foo%Ivoid", encoded);
        }

        [Fact]
        public void CanDeserializeParameter()
        {
            var param = "a%%a%%%}a%Jb=foo%Ivoid";
            var encoded = _serializer.DecodeParameter(param);
            Assert.Equal("a%a%=a\nb", encoded.Item1);
            Assert.Equal("foo\tvoid", encoded.Item2);
        }

        [Fact]
        public void MultipleEncode()
        {
            var message = "a%a%=a\nb";
            var encoded = _serializer.Encode(message);
            Assert.Equal("a%%a%%%}a%Jb", encoded);
        }

        [Fact]
        public void MultipleDecode()
        {
            var message = "a%%a%%%}a%Jb";
            var encoded = _serializer.Decode(message);
            Assert.Equal("a%a%=a\nb", encoded);
        }

        [Fact]
        public void EqualAndColonEncode()
        {
            var message = "a=a:b==bb";
            var encoded = _serializer.Encode(message);
            Assert.Equal("a%}a%zb%}%}bb", encoded);
        }

        [Fact]
        public void EqualAndColonDecode()
        {
            var message = "a%}a%zb%}%}bb";
            var encoded = _serializer.Decode(message);
            Assert.Equal("a=a:b==bb", encoded);
        }
    }
}
